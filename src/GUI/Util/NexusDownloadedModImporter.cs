using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.Health;

using SharpCompress.Archives;
using SharpCompress.Readers;

using System.Runtime.ExceptionServices;

namespace DivinityModManager.Util;

public sealed class NexusDownloadedModValidationException : IOException
{
	public string ErrorCode { get; }
	public IReadOnlyList<DivinityModData> InspectedMods { get; internal set; }
	internal NexusDownloadedModValidationException(string code, string details, IReadOnlyList<DivinityModData> inspectedMods = null) : base(details)
	{
		ErrorCode = code;
		InspectedMods = inspectedMods ?? [];
	}

	public static void ThrowIfBlocked(PackagePreflightReport report)
	{
		var errors = report.Findings.Where(finding => finding.Severity == ModHealthSeverity.Error).ToArray();
		if (errors.Length == 0) return;
		var code = errors.All(error => error.Title == "Missing dependency") ? "missing-dependencies" : "invalid-package";
		var lines = errors.Take(12).Select(error => $"{PublicText(error.Title)}: {PublicText(error.Message)}");
		throw new NexusDownloadedModValidationException(code,
			String.Join("\n", lines) + (code == "missing-dependencies"
				? "\n\nInstall the listed requirements, refresh the mod library, then retry installation. Downloading this archive again will not supply its dependencies."
				: "\n\nReview the package requirements and the author's installation instructions before retrying."), report.Mod == null ? [] : [report.Mod]);
	}

	private static string PublicText(string value)
	{
		// Only structured local validation text is retained, never raw exception messages
		// from HTTP clients. Metadata itself may contain URLs or capability-like text.
		var text = System.Text.RegularExpressions.Regex.Replace(value ?? String.Empty,
			@"(?i)\b(?:https?|nxm)://\S+|\b(?:api[_-]?key|authorization|key|expires|user_id)\s*[=:]\s*\S+", "[redacted]");
		text = new String(text.Select(c => Char.IsControl(c) ? ' ' : c).ToArray());
		return text.Length > 400 ? text[..400] + "..." : text;
	}

	public static (string Code, string Details) Describe(Exception error) => error switch
	{
		NexusDownloadedModValidationException validation => (validation.ErrorCode, validation.Message),
		NexusDownloadedModRollbackException => ("rollback-failed", "Some installed files could not be restored. Do not retry installation until the files in the recovery dialog have been recovered."),
		FileNotFoundException or DirectoryNotFoundException => ("missing-file", "A required file or folder could not be found. Check that the downloaded archive still exists and that the configured Mods folder is available."),
		UnauthorizedAccessException => ("install-access-denied", "Redux could not access a required file or folder. Check Mods-folder permissions and security-software restrictions, then retry installation."),
		IOException => ("install-io", "Redux could not finish a local file operation. Check available disk space, folder access, and whether another process has locked the files. The downloaded archive was retained."),
		_ => ("install-failed", $"Installation stopped because of {error.GetType().Name}. The downloaded archive was retained. Inspect its contents and installation requirements before retrying.")
	};
}

public sealed class NexusDownloadedModRollbackException : IOException
{
	public string RecoveryDirectory { get; }
	public IReadOnlyList<string> AffectedFiles { get; }

	internal NexusDownloadedModRollbackException(string recoveryDirectory, IEnumerable<string> affectedFiles,
		IEnumerable<Exception> errors) : base(
		"One or more installed files could not be restored. Recovery copies were retained.",
		new AggregateException(errors))
	{
		RecoveryDirectory = recoveryDirectory;
		AffectedFiles = affectedFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
	}
}

public sealed class StagedNexusPak
{
	public string FileName { get; }
	public string StagedPath { get; }
	public string DestinationPath { get; }
	public DivinityModData Mod { get; }

	internal StagedNexusPak(string fileName, string stagedPath, string destinationPath, DivinityModData mod)
	{
		FileName = fileName;
		StagedPath = stagedPath;
		DestinationPath = destinationPath;
		Mod = mod;
	}
}

public sealed class NexusDownloadedModImporter
{
	private const int MaximumArchiveEntries = 4096;
	private const int MaximumPakCount = 256;
	private const long MaximumExpandedBytes = 8L * 1024 * 1024 * 1024;
	private readonly string _modsDirectory;
	private readonly string _recoveryDirectory;
	private readonly Func<string, CancellationToken, Task<DivinityModData>> _validator;
	private readonly Func<string, IReadOnlyList<DivinityModData>, CancellationToken, Task> _preflight;

	public NexusDownloadedModImporter(
		string modsDirectory,
		string recoveryDirectory,
		Func<string, CancellationToken, Task<DivinityModData>> validator,
		Func<string, CancellationToken, Task> preflight)
		: this(modsDirectory, recoveryDirectory, validator, (path, _, token) => preflight(path, token))
	{
		ArgumentNullException.ThrowIfNull(preflight);
	}

	public NexusDownloadedModImporter(
		string modsDirectory,
		string recoveryDirectory,
		Func<string, CancellationToken, Task<DivinityModData>> validator,
		Func<string, IReadOnlyList<DivinityModData>, CancellationToken, Task> preflight)
	{
		_modsDirectory = Path.GetFullPath(modsDirectory ?? throw new ArgumentNullException(nameof(modsDirectory)));
		_recoveryDirectory = Path.GetFullPath(recoveryDirectory ?? throw new ArgumentNullException(nameof(recoveryDirectory)));
		_validator = validator ?? throw new ArgumentNullException(nameof(validator));
		_preflight = preflight ?? throw new ArgumentNullException(nameof(preflight));
	}

	public static NexusDownloadedModImporter Create(
		string modsDirectory,
		string recoveryDirectory,
		IEnumerable<DivinityModData> installedMods,
		Dictionary<string, DivinityModData> builtinMods)
	{
		var availableMods = (installedMods ?? Enumerable.Empty<DivinityModData>()).ToArray();
		return new NexusDownloadedModImporter(
			modsDirectory,
			recoveryDirectory,
			(path, token) => DivinityModDataLoader.LoadModDataFromPakAsync(path, builtinMods, token),
			async (path, otherPackageMods, token) =>
			{
				var report = await PackagePreflightService.AnalyzeAsync(path, availableMods.Concat(otherPackageMods), token);
				NexusDownloadedModValidationException.ThrowIfBlocked(report);
			});
	}

	public async Task<NexusDownloadedModImportTransaction> StageAsync(string packagePath, CancellationToken cancellationToken)
	{
		if (String.IsNullOrWhiteSpace(packagePath)) throw new ArgumentException("A package path is required.", nameof(packagePath));
		var sourcePath = Path.GetFullPath(packagePath);
		cancellationToken.ThrowIfCancellationRequested();

		Directory.CreateDirectory(_modsDirectory);
		var stagingRoot = Path.Combine(_modsDirectory, $".redux-nxm-{Guid.NewGuid():N}");
		Directory.CreateDirectory(stagingRoot);
		try
		{
			List<(string Name, string Path)> stagedFiles;
			try
			{
				stagedFiles = Path.GetExtension(sourcePath).Equals(".pak", StringComparison.OrdinalIgnoreCase)
					? await StageSinglePakAsync(sourcePath, stagingRoot, cancellationToken)
					: await StageArchiveAsync(sourcePath, stagingRoot, cancellationToken);
			}
			catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException
				or SharpCompress.Common.ArchiveException or SharpCompress.Common.ArchiveOperationException)
			{
				throw new NexusDownloadedModValidationException("archive-unreadable",
					"The archive could not be fully read. It may be incomplete, damaged, or use an unsupported format. Try Download Again; if a fresh copy fails too, check the author's archive and installation instructions.");
			}
			var stagedPaks = new List<StagedNexusPak>(stagedFiles.Count);
			var hasUnreadablePackage = false;
			foreach (var staged in stagedFiles)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var mod = await _validator(staged.Path, cancellationToken);
				if (mod == null) { hasUnreadablePackage = true; continue; }
				stagedPaks.Add(new StagedNexusPak(staged.Name, staged.Path, Path.Combine(_modsDirectory, staged.Name), mod));
			}
			var inspectedMods = stagedPaks.Select(package => package.Mod).ToArray();
			if (hasUnreadablePackage)
				throw new NexusDownloadedModValidationException("invalid-package", "A PAK file could not be read as a supported BG3 package. Check the author's installation instructions.", inspectedMods);
			if (stagedPaks.Where(package => !String.IsNullOrWhiteSpace(package.Mod.UUID))
				.GroupBy(package => package.Mod.UUID, StringComparer.OrdinalIgnoreCase)
				.Any(group => group.Count() > 1))
				throw new InvalidDataException("The archive contains multiple PAK files with the same mod UUID.");
			// A dependency may be another PAK in this archive. Identify every package
			// before preflight, and retain the full readable set when validation blocks.
			foreach (var package in stagedPaks)
			{
				try
				{
					await _preflight(package.StagedPath, stagedPaks.Where(other => other != package).Select(other => other.Mod).ToArray(), cancellationToken);
				}
				catch (NexusDownloadedModValidationException ex)
				{
					ex.InspectedMods = inspectedMods;
					throw;
				}
			}
			return new NexusDownloadedModImportTransaction(stagingRoot, _recoveryDirectory, stagedPaks);
		}
		catch
		{
			DeleteDirectory(stagingRoot);
			throw;
		}
	}

	private static async Task<List<(string Name, string Path)>> StageSinglePakAsync(
		string sourcePath, string stagingRoot, CancellationToken cancellationToken)
	{
		var name = Path.GetFileName(sourcePath);
		var stagedPath = Path.Combine(stagingRoot, name);
		await CopyBoundedAsync(File.OpenRead(sourcePath), stagedPath, MaximumExpandedBytes, cancellationToken);
		return [(name, stagedPath)];
	}

	private static async Task<List<(string Name, string Path)>> StageArchiveAsync(
		string sourcePath, string stagingRoot, CancellationToken cancellationToken)
	{
		await using var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536,
			FileOptions.Asynchronous | FileOptions.SequentialScan);
		using var archive = ArchiveFactory.OpenArchive(stream, new ReaderOptions());
		var entries = archive.Entries.Where(entry => !entry.IsDirectory).ToArray();
		if (entries.Length > MaximumArchiveEntries) throw new NexusDownloadedModValidationException("archive-layout", "The archive exceeds the safety limit of 4096 entries.");
		var pakEntries = entries.Where(entry => entry.Key.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)).ToArray();
		if (pakEntries.Length == 0 || pakEntries.Length > MaximumPakCount)
			throw new NexusDownloadedModValidationException("archive-layout", "The archive must contain between 1 and 256 PAK files. Archives containing only loose files or nested archives require a different installation method; check the author's instructions.");
		var names = pakEntries.Select(entry => Path.GetFileName(entry.Key)).ToArray();
		if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
			throw new NexusDownloadedModValidationException("archive-layout", "The archive contains PAK files with duplicate filenames. These may be alternative versions; select the intended package using the author's instructions instead of installing every variant.");
		if (pakEntries.Any(entry => entry.Size < 0) || pakEntries.Sum(entry => entry.Size) > MaximumExpandedBytes)
			throw new InvalidDataException("The expanded PAK data exceeds the safety limit.");

		var result = new List<(string Name, string Path)>(pakEntries.Length);
		long totalBytes = 0;
		for (var index = 0; index < pakEntries.Length; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var name = names[index];
			var path = Path.Combine(stagingRoot, name);
			await using var entryStream = pakEntries[index].OpenEntryStream();
			totalBytes += await CopyBoundedAsync(entryStream, path, MaximumExpandedBytes - totalBytes, cancellationToken);
			result.Add((name, path));
		}
		return result;
	}

	private static async Task<long> CopyBoundedAsync(Stream source, string destination, long maximumBytes, CancellationToken cancellationToken)
	{
		await using (source)
		await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536,
			FileOptions.Asynchronous | FileOptions.SequentialScan))
		{
			var buffer = new byte[65536];
			long total = 0;
			while (true)
			{
				var read = await source.ReadAsync(buffer, cancellationToken);
				if (read == 0) break;
				total += read;
				if (total > maximumBytes) throw new InvalidDataException("The expanded PAK data exceeds the safety limit.");
				await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
			}
			await output.FlushAsync(cancellationToken);
			return total;
		}
	}

	internal static void DeleteDirectory(string path)
	{
		if (!String.IsNullOrWhiteSpace(path) && Directory.Exists(path)) Directory.Delete(path, true);
	}
}

public sealed class NexusDownloadedModImportTransaction : IAsyncDisposable
{
	private readonly string _stagingRoot;
	private readonly string _recoveryDirectory;
	private readonly IReadOnlyList<StagedNexusPak> _packages;
	private List<(StagedNexusPak Pak, string BackupPath)> _committed;
	private bool _completed;

	internal NexusDownloadedModImportTransaction(string stagingRoot, string recoveryDirectory, IReadOnlyList<StagedNexusPak> packages)
	{
		_stagingRoot = stagingRoot;
		_recoveryDirectory = recoveryDirectory;
		_packages = packages;
	}

	public IReadOnlyList<StagedNexusPak> Packages => _packages;

	public async Task<IReadOnlyList<DivinityModData>> CommitAsync(
		Func<StagedNexusPak, Task<DivinityModData>> register, CancellationToken cancellationToken)
	{
		if (_completed) throw new InvalidOperationException("This import transaction has already completed.");
		if (register == null) throw new ArgumentNullException(nameof(register));
		Directory.CreateDirectory(_recoveryDirectory);
		var committed = new List<(StagedNexusPak Pak, string BackupPath)>();
		try
		{
			foreach (var package in _packages)
			{
				cancellationToken.ThrowIfCancellationRequested();
				string backupPath = null;
				if (File.Exists(package.DestinationPath))
				{
					backupPath = GetUniqueBackupPath(_recoveryDirectory, package.FileName);
					File.Replace(package.StagedPath, package.DestinationPath, backupPath, true);
				}
				else
				{
					File.Move(package.StagedPath, package.DestinationPath);
				}
				if (!package.Mod.HasMetadata && package.Mod.IsForceLoaded)
				{
					package.Mod.Name = Path.GetFileNameWithoutExtension(package.DestinationPath);
					package.Mod.UUID = package.DestinationPath;
				}
				committed.Add((package, backupPath));
			}

			var models = new List<DivinityModData>(_packages.Count);
			foreach (var package in _packages)
			{
				cancellationToken.ThrowIfCancellationRequested();
				package.Mod.FilePath = package.DestinationPath;
				models.Add(await register(package) ?? package.Mod);
			}
			_committed = committed;
			return models;
		}
		catch (Exception originalError)
		{
			var rollbackErrors = RollbackFiles(committed);
			_completed = true;
			NexusDownloadedModImporter.DeleteDirectory(_stagingRoot);
			if (rollbackErrors.Count > 0)
				throw new NexusDownloadedModRollbackException(_recoveryDirectory,
					rollbackErrors.Select(error => error.FileName),
					new[] { originalError }.Concat(rollbackErrors.Select(error => error.Error)));
			ExceptionDispatchInfo.Capture(originalError).Throw();
			throw;
		}
	}

	public void Complete()
	{
		if (_completed) return;
		NexusDownloadedModImporter.DeleteDirectory(_stagingRoot);
		_completed = true;
	}

	public Task RollbackAsync()
	{
		if (_completed) return Task.CompletedTask;
		var rollbackErrors = RollbackFiles(_committed ?? []);
		_completed = true;
		NexusDownloadedModImporter.DeleteDirectory(_stagingRoot);
		if (rollbackErrors.Count > 0)
			throw new NexusDownloadedModRollbackException(_recoveryDirectory,
				rollbackErrors.Select(error => error.FileName), rollbackErrors.Select(error => error.Error));
		return Task.CompletedTask;
	}

	public ValueTask DisposeAsync()
	{
		if (!_completed) RollbackAsync().GetAwaiter().GetResult();
		return ValueTask.CompletedTask;
	}

	private static List<(string FileName, Exception Error)> RollbackFiles(
		IReadOnlyList<(StagedNexusPak Pak, string BackupPath)> committed)
	{
		var errors = new List<(string FileName, Exception Error)>();
		for (var index = committed.Count - 1; index >= 0; index--)
		{
			var item = committed[index];
			try
			{
				if (item.BackupPath != null && File.Exists(item.BackupPath))
				{
					if (File.Exists(item.Pak.DestinationPath)) File.Replace(item.BackupPath, item.Pak.DestinationPath, null, true);
					else File.Move(item.BackupPath, item.Pak.DestinationPath);
				}
				else if (File.Exists(item.Pak.DestinationPath)) File.Delete(item.Pak.DestinationPath);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				errors.Add((item.Pak.FileName, new IOException($"Could not restore '{item.Pak.FileName}'.", ex)));
			}
		}
		return errors;
	}

	private static string GetUniqueBackupPath(string directory, string fileName)
	{
		var name = Path.GetFileNameWithoutExtension(fileName);
		var extension = Path.GetExtension(fileName);
		var candidate = Path.Combine(directory, fileName);
		for (var suffix = 1; File.Exists(candidate); suffix++)
			candidate = Path.Combine(directory, $"{name}_{DateTime.Now:yyyyMMdd-HHmmss}_{suffix}{extension}");
		return candidate;
	}
}
