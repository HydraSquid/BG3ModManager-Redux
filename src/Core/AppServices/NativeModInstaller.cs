using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace DivinityModManager.AppServices;

public sealed record NativeModDefinition(
	long NexusModId,
	string Name,
	bool RequiresLoader,
	IReadOnlyList<string> RelativeFiles);

public static class NativeModCatalog
{
	public static IReadOnlyList<NativeModDefinition> All { get; } = Array.AsReadOnly(new[]
	{
		new NativeModDefinition(944, "Native Mod Loader", false,
			Array.AsReadOnly(new[] { "bin/bink2w64.dll", "bin/bink2w64_original.dll" })),
		new NativeModDefinition(781, "WASD Character Movement", true,
			Array.AsReadOnly(new[] { "bin/NativeMods/BG3WASD.dll", "bin/NativeMods/BG3WASD.toml" })),
		new NativeModDefinition(945, "Native Camera Tweaks", true,
			Array.AsReadOnly(new[] { "bin/NativeMods/BG3NativeCameraTweaks.dll", "bin/NativeMods/BG3NativeCameraTweaks.toml" }))
	});

	public static NativeModDefinition? Find(long id) => All.FirstOrDefault(definition => definition.NexusModId == id);
}

public sealed record NativeLoaderStatus(bool IsPresent, bool IsVerified, string Description);

public sealed class NativeModRecoveryException(string message, Exception? innerException = null) : IOException(message, innerException) { }

/// <summary>
/// Installs only the explicitly reviewed native-mod archives. Archive structure and PE headers are
/// validated locally; this is not a provenance or universal game-compatibility assertion.
/// </summary>
public sealed class NativeModInstaller
{
	private const int ManifestVersion = 1;
	private const long MaximumArchiveBytes = 128L * 1024 * 1024;
	private const long MaximumEntryBytes = 32L * 1024 * 1024;
	private const long MaximumExpandedBytes = 64L * 1024 * 1024;
	private const long MaximumManifestBytes = 256 * 1024;
	private const int MaximumArchiveEntries = 16;
	private const int MaximumCompressionRatio = 200;
	private const long CompressionRatioMinimumBytes = 1024 * 1024;
	private static readonly Version MinimumPluginGameVersion = new(4, 1, 1, 6931813);
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = false,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
		WriteIndented = false
	};

	private readonly string _gameBin;
	private readonly string _stateRoot;
	private readonly string _manifestPath;
	private readonly string _journalDirectory;
	private readonly string _backupDirectory;
	private readonly string _stagingDirectory;
	private readonly string _stateLockPath;
	private readonly string _gameBinIdentity;
	private readonly Version? _gameVersion;
	private readonly SemaphoreSlim _operationGate = new(1, 1);
	public string GameBin => _gameBin;

	public NativeModInstaller(string gameBin, string stateDirectory, Version gameVersion)
	{
		_gameBin = NormalizeExistingDirectory(gameBin, nameof(gameBin));
		ValidateGameBin();
		_gameVersion = gameVersion;
		_gameBinIdentity = HashText(_gameBin.ToUpperInvariant());

		var root = NormalizeOrCreateDirectory(stateDirectory, nameof(stateDirectory));
		_stateRoot = Path.Combine(root, "native-mods", _gameBinIdentity);
		EnsureSafeDirectoryTree(_stateRoot, create: true);
		_manifestPath = Path.Combine(_stateRoot, "manifest.json");
		_journalDirectory = Path.Combine(_stateRoot, "journals");
		_backupDirectory = Path.Combine(_stateRoot, "backups");
		_stagingDirectory = Path.Combine(_stateRoot, "staging");
		_stateLockPath = Path.Combine(_stateRoot, "operation.lock");
		EnsureSafeDirectoryTree(_journalDirectory, create: true);
		EnsureSafeDirectoryTree(_backupDirectory, create: true);
		EnsureSafeDirectoryTree(_stagingDirectory, create: true);
	}

	public NativeLoaderStatus DetectLoader()
	{
		try
		{
			ValidateGameBin();
			return DetectLoader(ReadManifest());
		}
		catch (Exception ex) when (IsValidationException(ex))
		{
			return new NativeLoaderStatus(false, false,
				"Native Mod Loader status is unavailable because Redux ownership records or game files could not be safely verified.");
		}
	}

	public async Task InspectArchiveAsync(long projectId, string archivePath, CancellationToken cancellationToken = default)
	{
		var definition = NativeModCatalog.Find(projectId)
			?? throw new InvalidDataException("This Nexus project is not approved for native installation.");
		await _operationGate.WaitAsync(cancellationToken);
		try
		{
			using var stateLock = AcquireStateLock();
			cancellationToken.ThrowIfCancellationRequested();
			ValidateGameBin();
			EnsurePluginVersion(definition);
			var archive = ValidateArchivePath(archivePath);
			var stageDirectory = Path.Combine(_stagingDirectory, Guid.NewGuid().ToString("N"));
			EnsureStageDirectory(stageDirectory);
			try
			{
				await ExtractValidatedArchiveAsync(definition, archive, stageDirectory, cancellationToken);
			}
			finally
			{
				DeleteStageDirectory(stageDirectory);
			}
		}
		finally
		{
			_operationGate.Release();
		}
	}

	public async Task<NativeModInstallTransaction> StageAsync(long projectId, string archivePath,
		CancellationToken cancellationToken = default)
	{
		var definition = NativeModCatalog.Find(projectId)
			?? throw new InvalidDataException("This Nexus project is not approved for native installation.");
		await _operationGate.WaitAsync(cancellationToken);
		try
		{
			using var stateLock = AcquireStateLock();
			cancellationToken.ThrowIfCancellationRequested();
			ValidateGameBin();
			ThrowIfGameRunning();
			EnsureNoPendingJournals();
			EnsurePluginVersion(definition);

			var manifest = ReadManifest();
			var loaderStatus = DetectLoader(manifest);
			if (definition.RequiresLoader && !loaderStatus.IsPresent)
				throw new InvalidOperationException($"{definition.Name} requires Native Mod Loader. {loaderStatus.Description}");

			var archive = ValidateArchivePath(archivePath);
			var archiveFingerprint = CaptureFingerprint(archive);
			var transactionId = Guid.NewGuid().ToString("N");
			var stageDirectory = Path.Combine(_stagingDirectory, transactionId);
			EnsureStageDirectory(stageDirectory);
			try
			{
				var stagedFiles = await ExtractValidatedArchiveAsync(definition, archive, stageDirectory, cancellationToken);
				var plan = BuildInstallPlan(definition, manifest, FingerprintManifest(manifest), archive, archiveFingerprint, stageDirectory,
					transactionId, stagedFiles, loaderStatus);
				return new NativeModInstallTransaction(this, plan, plan.ReviewText);
			}
			catch
			{
				DeleteStageDirectory(stageDirectory);
				throw;
			}
		}
		finally
		{
			_operationGate.Release();
		}
	}

	public async Task RestoreAsync(long projectId, CancellationToken cancellationToken = default)
	{
		var definition = NativeModCatalog.Find(projectId)
			?? throw new InvalidDataException("This Nexus project is not managed by the native installer.");
		await _operationGate.WaitAsync(cancellationToken);
		try
		{
			using var stateLock = AcquireStateLock();
			ValidateGameBin();
			ThrowIfGameRunning();
			EnsureNoPendingJournals();
			var manifest = ReadManifest() ?? throw new InvalidOperationException("Redux has no native installation record for this game.");
			var installation = FindInstallation(manifest, projectId)
				?? throw new InvalidOperationException("Redux does not own this native installation.");

			if (projectId == 944) EnsureNoNativePluginDllsRemain();
			var writes = new List<PlannedWrite>();
			foreach (var ownedFile in installation.Files)
			{
				var snapshot = CaptureDestination(ownedFile.RelativePath);
				if (!snapshot.Exists || !String.Equals(snapshot.Hash, ownedFile.InstalledHash, StringComparison.Ordinal))
					throw new InvalidOperationException($"Redux will not restore {definition.Name} because '{ownedFile.RelativePath}' was changed outside Redux.");
				if (!ownedFile.Created)
				{
					var backupPath = GetBackupPath(ownedFile.OriginalBackupId!);
					EnsureSafeRegularFile(backupPath);
					if (!String.Equals(HashFile(backupPath), ownedFile.OriginalHash, StringComparison.Ordinal))
						throw new InvalidDataException("Redux's original native-file backup no longer matches its ownership record.");
				}
				writes.Add(new PlannedWrite(ownedFile.RelativePath, ownedFile.InstalledHash, snapshot,
					CloneOwnedFile(ownedFile)));
			}

			var transactionId = Guid.NewGuid().ToString("N");
			var transactionBackups = await CreateTransactionBackupsAsync(writes, transactionId, cancellationToken);
			var journalPath = GetJournalPath(transactionId);
			var journalWritten = false;
			var manifestSaved = false;
			var completedWrites = new List<PlannedWrite>();
			try
			{
				await WriteJournalAsync(journalPath, "restore", projectId, transactionId, writes, transactionBackups, cancellationToken);
				journalWritten = true;
				foreach (var write in writes)
				{
					cancellationToken.ThrowIfCancellationRequested();
					ThrowIfGameRunning();
					var current = CaptureDestination(write.RelativePath);
					if (!SameSnapshot(write.Before, current))
						throw new InvalidOperationException("A native destination changed after restore review.");
					if (write.Ownership.Created)
					{
						File.Delete(ResolveTargetPath(write.RelativePath, createParent: false));
					}
					else
					{
						await CopyToTargetAtomicallyAsync(GetBackupPath(write.Ownership.OriginalBackupId!),
							ResolveTargetPath(write.RelativePath, createParent: true), write.Ownership.OriginalHash!, write.Before, cancellationToken);
					}
					completedWrites.Add(write);
				}

				manifest.Installations.RemoveAll(item => item.ProjectId == projectId);
				await WriteManifestAsync(manifest, cancellationToken);
				manifestSaved = true;
				DeleteJournal(journalPath);
			}
			catch
			{
				if (manifestSaved) throw;
				var rollbackError = await RollbackRestoreAsync(completedWrites, transactionBackups);
				if (rollbackError == null && journalWritten) DeleteJournal(journalPath);
				if (rollbackError != null)
					throw new NativeModRecoveryException("Native restore failed and Redux could not safely roll back every changed file. Do not launch the game; inspect the native installer journal and backups.", rollbackError);
				throw;
			}
		}
		finally
		{
			_operationGate.Release();
		}
	}

	internal async Task CommitAsync(NativeInstallPlan plan, CancellationToken cancellationToken)
	{
		await _operationGate.WaitAsync(cancellationToken);
		try
		{
			using var stateLock = AcquireStateLock();
			ValidateGameBin();
			ThrowIfGameRunning();
			EnsureNoPendingJournals();
			var currentManifest = ReadMatchingManifest(plan);
			EnsureCommitPrerequisites(plan.Definition, currentManifest);
			EnsureArchiveUnchanged(plan.ArchivePath, plan.ArchiveFingerprint);
			foreach (var guard in plan.Guards)
			{
				if (!SameSnapshot(guard, CaptureDestination(guard.RelativePath)))
					throw new InvalidOperationException("A native destination changed after the installation review.");
			}
			foreach (var write in plan.Writes)
			{
				var stagedPath = GetStagedPath(plan.StageDirectory, write.RelativePath);
				EnsureSafeRegularFile(stagedPath);
				if (!String.Equals(HashFile(stagedPath), write.StagedHash, StringComparison.Ordinal))
					throw new InvalidDataException("The staged native file changed after archive validation.");
			}

			var transactionBackups = await CreateTransactionBackupsAsync(plan.Writes, plan.TransactionId, cancellationToken);
			foreach (var write in plan.Writes)
			{
				if (write.Ownership.Created) continue;
				if (String.IsNullOrWhiteSpace(write.Ownership.OriginalBackupId))
				{
					var backup = transactionBackups.Single(item => item.RelativePath == write.RelativePath);
					write.Ownership.OriginalBackupId = backup.BackupId;
					write.Ownership.OriginalHash = write.Before.Hash;
				}
			}

			var journalPath = GetJournalPath(plan.TransactionId);
			var journalWritten = false;
			var manifestSaved = false;
			var completedWrites = new List<PlannedWrite>();
			try
			{
				await WriteJournalAsync(journalPath, "install", plan.Definition.NexusModId, plan.TransactionId,
					plan.Writes, transactionBackups, cancellationToken);
				journalWritten = true;
				currentManifest = ReadMatchingManifest(plan);
				EnsureCommitPrerequisites(plan.Definition, currentManifest);
				foreach (var guard in plan.Guards)
				{
					if (!SameSnapshot(guard, CaptureDestination(guard.RelativePath)))
						throw new InvalidOperationException("A native destination changed after the installation review.");
				}
				foreach (var write in plan.Writes)
				{
					cancellationToken.ThrowIfCancellationRequested();
					ThrowIfGameRunning();
					var current = CaptureDestination(write.RelativePath);
					if (!SameSnapshot(write.Before, current))
						throw new InvalidOperationException("A native destination changed after the installation review.");
					await CopyToTargetAtomicallyAsync(GetStagedPath(plan.StageDirectory, write.RelativePath),
						ResolveTargetPath(write.RelativePath, createParent: true), write.StagedHash, write.Before, cancellationToken);
					completedWrites.Add(write);
				}

				var nextManifest = CreateOrCloneManifest(currentManifest);
				nextManifest.Installations.RemoveAll(item => item.ProjectId == plan.Definition.NexusModId);
				nextManifest.Installations.Add(CloneInstallation(plan.InstallationAfter));
				await WriteManifestAsync(nextManifest, cancellationToken);
				manifestSaved = true;
				DeleteJournal(journalPath);
			}
			catch
			{
				if (manifestSaved) throw;
				var rollbackError = await RollbackAsync(completedWrites, transactionBackups);
				if (rollbackError == null && journalWritten) DeleteJournal(journalPath);
				if (rollbackError != null)
					throw new NativeModRecoveryException("Native installation failed and Redux could not safely roll back every changed file. Do not launch the game; inspect the native installer journal and backups.", rollbackError);
				throw;
			}
			finally
			{
				DeleteStageDirectory(plan.StageDirectory);
			}
		}
		finally
		{
			_operationGate.Release();
		}
	}

	internal ValueTask DiscardStageAsync(NativeInstallPlan plan)
	{
		DeleteStageDirectory(plan.StageDirectory);
		return ValueTask.CompletedTask;
	}

	private NativeInstallPlan BuildInstallPlan(NativeModDefinition definition, NativeInstallManifest? manifest,
		string manifestFingerprint, string archivePath, FileFingerprint archiveFingerprint, string stageDirectory, string transactionId,
		IReadOnlyDictionary<string, string> stagedFiles, NativeLoaderStatus loaderStatus)
	{
		var existing = manifest == null ? null : FindInstallation(manifest, definition.NexusModId);
		var snapshots = definition.RelativeFiles
			.Select(path => CaptureDestination(ToTargetRelative(path)))
			.ToDictionary(snapshot => snapshot.RelativePath, StringComparer.Ordinal);
		var guards = snapshots.Values.ToList();
		if (definition.RequiresLoader)
		{
			foreach (var loaderFile in NativeModCatalog.Find(944)!.RelativeFiles)
				guards.Add(CaptureDestination(ToTargetRelative(loaderFile)));
		}
		var stagedHashes = stagedFiles.ToDictionary(pair => pair.Key, pair => HashFile(pair.Value), StringComparer.Ordinal);
		var installation = new NativeOwnedInstallation { ProjectId = definition.NexusModId };
		var writes = new List<PlannedWrite>();

		if (definition.NexusModId == 944)
		{
			BuildLoaderPlan(existing, snapshots, stagedHashes, installation, writes);
		}
		else
		{
			BuildPluginPlan(definition, existing, snapshots, stagedHashes, installation, writes);
		}

		var review = new StringBuilder();
		review.Append(definition.Name).Append(" is staged for explicit confirmation.\n\nGame directory: ").Append(_gameBin)
			.Append("\n\nRedux will change: ")
			.Append(String.Join(", ", writes.Select(write => write.RelativePath))).Append('.');
		if (definition.RequiresLoader && !loaderStatus.IsVerified)
			review.Append(" Native Mod Loader is external and unverified; review plugin compatibility before continuing.");
		review.Append(" Archive structure and AMD64 PE headers were checked locally; this does not prove publisher provenance or universal game compatibility.");

		return new NativeInstallPlan(definition, manifestFingerprint, archivePath, archiveFingerprint,
			stageDirectory, transactionId, guards, writes, installation, review.ToString());
	}

	private static void BuildLoaderPlan(NativeOwnedInstallation? existing,
		IReadOnlyDictionary<string, DestinationSnapshot> snapshots, IReadOnlyDictionary<string, string> stagedHashes,
		NativeOwnedInstallation installation, ICollection<PlannedWrite> writes)
	{
		const string loader = "bink2w64.dll";
		const string original = "bink2w64_original.dll";
		var loaderSnapshot = snapshots[loader];
		var originalSnapshot = snapshots[original];
		var loaderHash = stagedHashes[loader];
		var packagedOriginalHash = stagedHashes[original];

		if (existing == null)
		{
			if (originalSnapshot.Exists)
				throw new InvalidOperationException("Native Mod Loader cannot replace an unmanaged bink2w64_original.dll. Recover or review it manually first.");
			if (!loaderSnapshot.Exists || !String.Equals(loaderSnapshot.Hash, packagedOriginalHash, StringComparison.Ordinal))
				throw new InvalidOperationException("Native Mod Loader cannot establish that the current bink2w64.dll matches this archive's packaged original. Do not overwrite it.");
			installation.Files.Add(NewOwnership(loaderSnapshot, loaderHash));
			installation.Files.Add(NewOwnership(originalSnapshot, packagedOriginalHash));
			writes.Add(new PlannedWrite(loader, loaderHash, loaderSnapshot, installation.Files[0]));
			writes.Add(new PlannedWrite(original, packagedOriginalHash, originalSnapshot, installation.Files[1]));
			return;
		}

		var priorLoader = FindOwnedFile(existing, loader)
			?? throw new InvalidDataException("Redux's Native Mod Loader ownership record is incomplete.");
		var priorOriginal = FindOwnedFile(existing, original)
			?? throw new InvalidDataException("Redux's Native Mod Loader ownership record is incomplete.");
		EnsureOwnedFileUnchanged(priorLoader, loaderSnapshot);
		EnsureOwnedFileUnchanged(priorOriginal, originalSnapshot);
		if (!String.Equals(originalSnapshot.Hash, packagedOriginalHash, StringComparison.Ordinal))
			throw new InvalidOperationException("The loader archive's packaged original does not match the verified current game original. Do not apply an old loader archive after a game update.");

		var nextLoader = CloneOwnedFile(priorLoader);
		nextLoader.InstalledHash = loaderHash;
		installation.Files.Add(nextLoader);
		installation.Files.Add(CloneOwnedFile(priorOriginal));
		writes.Add(new PlannedWrite(loader, loaderHash, loaderSnapshot, nextLoader));
	}

	private static void BuildPluginPlan(NativeModDefinition definition, NativeOwnedInstallation? existing,
		IReadOnlyDictionary<string, DestinationSnapshot> snapshots, IReadOnlyDictionary<string, string> stagedHashes,
		NativeOwnedInstallation installation, ICollection<PlannedWrite> writes)
	{
		var dllRelative = ToTargetRelative(definition.RelativeFiles.Single(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)));
		var tomlRelative = ToTargetRelative(definition.RelativeFiles.Single(path => path.EndsWith(".toml", StringComparison.OrdinalIgnoreCase)));
		var dllSnapshot = snapshots[dllRelative];
		var priorDll = existing == null ? null : FindOwnedFile(existing, dllRelative);
		if (dllSnapshot.Exists)
		{
			if (priorDll == null)
				throw new InvalidOperationException($"Redux will not replace the unmanaged native DLL '{dllRelative}'.");
			EnsureOwnedFileUnchanged(priorDll, dllSnapshot);
		}
		else if (priorDll != null)
		{
			throw new InvalidOperationException($"Redux will not recreate the Redux-owned DLL '{dllRelative}' after it was removed outside Redux.");
		}

		var nextDll = priorDll == null ? NewOwnership(dllSnapshot, stagedHashes[dllRelative]) : CloneOwnedFile(priorDll);
		nextDll.InstalledHash = stagedHashes[dllRelative];
		installation.Files.Add(nextDll);
		writes.Add(new PlannedWrite(dllRelative, stagedHashes[dllRelative], dllSnapshot, nextDll));

		var tomlSnapshot = snapshots[tomlRelative];
		var priorToml = existing == null ? null : FindOwnedFile(existing, tomlRelative);
		if (tomlSnapshot.Exists)
		{
			// Existing configuration belongs to the player. Retain ownership only when it remains unchanged.
			if (priorToml != null && String.Equals(priorToml.InstalledHash, tomlSnapshot.Hash, StringComparison.Ordinal))
				installation.Files.Add(CloneOwnedFile(priorToml));
			return;
		}
		if (priorToml != null) return;

		var nextToml = NewOwnership(tomlSnapshot, stagedHashes[tomlRelative]);
		installation.Files.Add(nextToml);
		writes.Add(new PlannedWrite(tomlRelative, stagedHashes[tomlRelative], tomlSnapshot, nextToml));
	}

	private async Task<IReadOnlyDictionary<string, string>> ExtractValidatedArchiveAsync(NativeModDefinition definition,
		string archivePath, string stageDirectory, CancellationToken cancellationToken)
	{
		using var source = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read,
			128000, FileOptions.SequentialScan);
		using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: false);
		var entries = ValidateArchiveEntries(definition, archive);
		var stagedFiles = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var relativeFile in definition.RelativeFiles)
		{
			var targetRelative = ToTargetRelative(relativeFile);
			var stagedPath = GetStagedPath(stageDirectory, targetRelative);
			await CopyZipEntryAsync(entries[relativeFile], stagedPath, cancellationToken);
			if (relativeFile.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) ValidateAmd64PeDll(stagedPath);
			stagedFiles.Add(targetRelative, stagedPath);
		}
		return stagedFiles;
	}

	private static IReadOnlyDictionary<string, ZipArchiveEntry> ValidateArchiveEntries(NativeModDefinition definition,
		ZipArchive archive)
	{
		if (archive.Entries.Count > MaximumArchiveEntries)
			throw new InvalidDataException("The native archive contains too many entries.");
		var expected = definition.RelativeFiles.ToHashSet(StringComparer.Ordinal);
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var found = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
		long expandedBytes = 0;
		foreach (var entry in archive.Entries)
		{
			ValidateZipEntryPath(entry);
			if (!seen.Add(entry.FullName.TrimEnd('/')))
				throw new InvalidDataException("The native archive contains paths that differ only by case.");
			if (IsDirectoryEntry(entry)) continue;
			if (!expected.Contains(entry.FullName))
				throw new InvalidDataException("The native archive contains a file outside the explicit allowlist.");
			if (entry.Length <= 0 || entry.Length > MaximumEntryBytes)
				throw new InvalidDataException("The native archive contains an empty or oversized file.");
			if (entry.CompressedLength <= 0
				|| (entry.Length >= CompressionRatioMinimumBytes && entry.Length / entry.CompressedLength > MaximumCompressionRatio))
				throw new InvalidDataException("The native archive exceeds safe expansion limits.");
			expandedBytes = checked(expandedBytes + entry.Length);
			if (expandedBytes > MaximumExpandedBytes)
				throw new InvalidDataException("The native archive expands beyond the allowed size.");
			found.Add(entry.FullName, entry);
		}
		if (found.Count != expected.Count || expected.Any(path => !found.ContainsKey(path)))
			throw new InvalidDataException("The native archive is missing one or more required files.");
		return found;
	}

	private static async Task CopyZipEntryAsync(ZipArchiveEntry entry, string destinationPath,
		CancellationToken cancellationToken)
	{
		EnsureSafeDirectoryTree(Path.GetDirectoryName(destinationPath)!, create: true);
		using var input = entry.Open();
		await using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
			128000, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough);
		var buffer = new byte[128000];
		long written = 0;
		while (true)
		{
			var read = await input.ReadAsync(buffer, cancellationToken);
			if (read == 0) break;
			written = checked(written + read);
			if (written > entry.Length || written > MaximumEntryBytes)
				throw new InvalidDataException("The native archive entry expanded beyond its declared safe size.");
			await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
		}
		if (written != entry.Length) throw new InvalidDataException("The native archive entry was truncated while staging.");
		await output.FlushAsync(cancellationToken);
		output.Flush(true);
	}

	private async Task<IReadOnlyList<TransactionBackup>> CreateTransactionBackupsAsync(
		IReadOnlyCollection<PlannedWrite> writes, string transactionId, CancellationToken cancellationToken)
	{
		var backups = new List<TransactionBackup>();
		foreach (var write in writes.Where(write => write.Before.Exists))
		{
			cancellationToken.ThrowIfCancellationRequested();
			var current = CaptureDestination(write.RelativePath);
			if (!SameSnapshot(write.Before, current))
				throw new InvalidOperationException("A native destination changed before Redux could create its transaction backup.");
			var backupId = Guid.NewGuid().ToString("N");
			var backupPath = GetBackupPath(backupId);
			await CopyFileAsync(ResolveTargetPath(write.RelativePath, createParent: false), backupPath, write.Before.Hash!, cancellationToken);
			backups.Add(new TransactionBackup(write.RelativePath, backupId, backupPath, write.Before));
		}
		return backups;
	}

	private async Task<Exception?> RollbackAsync(IReadOnlyList<PlannedWrite> completedWrites,
		IReadOnlyList<TransactionBackup> transactionBackups)
	{
		Exception? failure = null;
		foreach (var write in completedWrites.Reverse())
		{
			try
			{
				var current = CaptureDestination(write.RelativePath);
				if (!current.Exists || !String.Equals(current.Hash, write.StagedHash, StringComparison.Ordinal))
					throw new InvalidOperationException($"'{write.RelativePath}' changed before Redux could roll it back.");
				if (write.Before.Exists)
				{
					var backup = transactionBackups.Single(item => item.RelativePath == write.RelativePath);
					await CopyToTargetAtomicallyAsync(backup.BackupPath,
						ResolveTargetPath(write.RelativePath, createParent: true), backup.Before.Hash!, current, CancellationToken.None);
				}
				else
				{
					File.Delete(ResolveTargetPath(write.RelativePath, createParent: false));
				}
			}
			catch (Exception error)
			{
				failure ??= error;
			}
		}
		return failure;
	}

	private async Task<Exception?> RollbackRestoreAsync(IReadOnlyList<PlannedWrite> completedWrites,
		IReadOnlyList<TransactionBackup> transactionBackups)
	{
		Exception? failure = null;
		foreach (var write in completedWrites.Reverse())
		{
			try
			{
				var current = CaptureDestination(write.RelativePath);
				if (write.Ownership.Created)
				{
					if (current.Exists)
						throw new InvalidOperationException($"'{write.RelativePath}' changed before Redux could roll back its restore.");
				}
				else if (!current.Exists || !String.Equals(current.Hash, write.Ownership.OriginalHash, StringComparison.Ordinal))
				{
					throw new InvalidOperationException($"'{write.RelativePath}' changed before Redux could roll back its restore.");
				}
				var backup = transactionBackups.Single(item => item.RelativePath == write.RelativePath);
				await CopyToTargetAtomicallyAsync(backup.BackupPath,
					ResolveTargetPath(write.RelativePath, createParent: true), backup.Before.Hash!, current, CancellationToken.None);
			}
			catch (Exception error)
			{
				failure ??= error;
			}
		}
		return failure;
	}

	private async Task CopyToTargetAtomicallyAsync(string sourcePath, string destinationPath,
		string expectedSourceHash, DestinationSnapshot expectedDestination, CancellationToken cancellationToken)
	{
		EnsureSafeRegularFile(sourcePath);
		if (!SameSnapshot(expectedDestination, CaptureDestination(expectedDestination.RelativePath)))
			throw new InvalidOperationException("A native destination changed immediately before replacement.");
		EnsureSafeDirectoryTree(Path.GetDirectoryName(destinationPath)!, create: true);
		var temporaryPath = destinationPath + ".redux-native-" + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			await CopyFileAsync(sourcePath, temporaryPath, expectedSourceHash, cancellationToken);
			if (!SameSnapshot(expectedDestination, CaptureDestination(expectedDestination.RelativePath)))
				throw new InvalidOperationException("A native destination changed during replacement preparation.");
			if (expectedDestination.Exists) File.Replace(temporaryPath, destinationPath, null, true);
			else File.Move(temporaryPath, destinationPath);
		}
		finally
		{
			if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
		}
	}

	private static async Task CopyFileAsync(string sourcePath, string destinationPath, string expectedHash,
		CancellationToken cancellationToken)
	{
		using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
			128000, FileOptions.Asynchronous | FileOptions.SequentialScan);
		await using (var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
			128000, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough))
		{
			await input.CopyToAsync(output, 128000, cancellationToken);
			await output.FlushAsync(cancellationToken);
			output.Flush(true);
		}
		if (!String.Equals(HashFile(destinationPath), expectedHash, StringComparison.Ordinal))
			throw new InvalidDataException("A native installer copy did not match its validated source hash.");
	}

	private async Task WriteManifestAsync(NativeInstallManifest manifest, CancellationToken cancellationToken)
	{
		ValidateManifest(manifest);
		await WriteJsonAtomicallyAsync(_manifestPath, manifest, cancellationToken);
	}

	private async Task WriteJournalAsync(string journalPath, string operation, long projectId, string transactionId,
		IReadOnlyCollection<PlannedWrite> writes, IReadOnlyList<TransactionBackup> backups,
		CancellationToken cancellationToken)
	{
		var journal = new NativeJournal
		{
			Version = ManifestVersion,
			GameBinIdentity = _gameBinIdentity,
			Operation = operation,
			ProjectId = projectId,
			TransactionId = transactionId,
			Files = writes.Select(write => new NativeJournalFile
			{
				RelativePath = write.RelativePath,
				BeforeExists = write.Before.Exists,
				BeforeHash = write.Before.Hash,
				StagedHash = write.StagedHash,
				BackupId = backups.FirstOrDefault(backup => backup.RelativePath == write.RelativePath)?.BackupId
			}).ToList()
		};
		ValidateJournal(journal);
		var bytes = JsonSerializer.SerializeToUtf8Bytes(journal, JsonOptions);
		EnsureSafeFileOrMissing(journalPath);
		await using var stream = new FileStream(journalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
			4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
		await stream.WriteAsync(bytes, cancellationToken);
		await stream.FlushAsync(cancellationToken);
		stream.Flush(true);
	}

	private async Task WriteJsonAtomicallyAsync<T>(string path, T value, CancellationToken cancellationToken)
	{
		EnsureSafeFileOrMissing(path);
		var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
		try
		{
			await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
				4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
			{
				await stream.WriteAsync(bytes, cancellationToken);
				await stream.FlushAsync(cancellationToken);
				stream.Flush(true);
			}
			if (File.Exists(path)) File.Replace(temporaryPath, path, null, true);
			else File.Move(temporaryPath, path);
		}
		finally
		{
			if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
		}
	}

	private NativeInstallManifest? ReadManifest()
	{
		if (!PathEntryExists(_manifestPath)) return null;
		EnsureSafeRegularFile(_manifestPath);
		if (new FileInfo(_manifestPath).Length > MaximumManifestBytes)
			throw new InvalidDataException("Redux's native ownership manifest is too large.");
		var json = File.ReadAllText(_manifestPath, Encoding.UTF8);
		using var document = JsonDocument.Parse(json);
		EnsureNoDuplicateJsonProperties(document.RootElement);
		var manifest = JsonSerializer.Deserialize<NativeInstallManifest>(json, JsonOptions)
			?? throw new InvalidDataException("Redux's native ownership manifest is empty.");
		ValidateManifest(manifest);
		return manifest;
	}

	private void EnsureNoPendingJournals()
	{
		EnsureSafeDirectoryTree(_journalDirectory, create: false);
		var journals = Directory.EnumerateFiles(_journalDirectory, "*.json", SearchOption.TopDirectoryOnly).ToArray();
		if (journals.Length == 0) return;
		foreach (var path in journals)
		{
			EnsureSafeRegularFile(path);
			if (new FileInfo(path).Length > MaximumManifestBytes) throw new InvalidDataException("A native installer journal is too large.");
			var json = File.ReadAllText(path, Encoding.UTF8);
			using var document = JsonDocument.Parse(json);
			EnsureNoDuplicateJsonProperties(document.RootElement);
			var journal = JsonSerializer.Deserialize<NativeJournal>(json, JsonOptions)
				?? throw new InvalidDataException("A native installer journal is empty.");
			ValidateJournal(journal);
		}
		throw new NativeModRecoveryException("A previous native installation did not finish. Do not launch the game; inspect the native installer journal and immutable backups before continuing.");
	}

	private void DeleteJournal(string path)
	{
		EnsureSafeRegularFile(path);
		File.Delete(path);
	}

	private NativeLoaderStatus DetectLoader(NativeInstallManifest? manifest)
	{
		const string loader = "bink2w64.dll";
		const string original = "bink2w64_original.dll";
		var owned = manifest == null ? null : FindInstallation(manifest, 944);
		var loaderSnapshot = CaptureDestination(loader);
		var originalSnapshot = CaptureDestination(original);
		if (owned != null)
		{
			var ownedLoader = FindOwnedFile(owned, loader);
			var ownedOriginal = FindOwnedFile(owned, original);
			if (ownedLoader == null || ownedOriginal == null
				|| !loaderSnapshot.Exists || !originalSnapshot.Exists
				|| !String.Equals(ownedLoader.InstalledHash, loaderSnapshot.Hash, StringComparison.Ordinal)
				|| !String.Equals(ownedOriginal.InstalledHash, originalSnapshot.Hash, StringComparison.Ordinal))
			{
				return new NativeLoaderStatus(false, false,
					"Native Mod Loader changed since Redux installed it; dependent native plugins are blocked.");
			}
			return new NativeLoaderStatus(true, true, "Native Mod Loader is verified as installed by Redux.");
		}

		if (!loaderSnapshot.Exists && !originalSnapshot.Exists)
			return new NativeLoaderStatus(false, false, "Native Mod Loader is missing.");
		if (!loaderSnapshot.Exists || !originalSnapshot.Exists
			|| !IsAmd64PeDll(ResolveTargetPath(loader, createParent: false))
			|| !IsAmd64PeDll(ResolveTargetPath(original, createParent: false)))
		{
			return new NativeLoaderStatus(false, false, "Native Mod Loader is incomplete or has invalid DLL files.");
		}
		return new NativeLoaderStatus(true, false,
			"Native Mod Loader is present but external and unverified. Review compatibility before installing a dependent plugin.");
	}

	private void EnsureNoNativePluginDllsRemain()
	{
		var root = Path.Combine(_gameBin, "NativeMods");
		if (!PathEntryExists(root)) return;
		var directories = new Stack<string>();
		directories.Push(root);
		var inspected = 0;
		while (directories.Count > 0)
		{
			var directory = directories.Pop();
			EnsureSafeDirectoryTree(directory, create: false);
			foreach (var path in Directory.EnumerateFileSystemEntries(directory))
			{
				if (++inspected > 4096 || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
					throw new InvalidOperationException("NativeMods could not be fully inspected safely. Review it manually before restoring the loader.");
				if (Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
					throw new InvalidOperationException("A native plugin DLL remains in NativeMods, possibly installed by another tool. Restore or remove dependent plugins before restoring Native Mod Loader.");
				if (Directory.Exists(path)) directories.Push(path);
			}
		}
	}

	private void EnsurePluginVersion(NativeModDefinition definition)
	{
		if (!definition.RequiresLoader) return;
		if (_gameVersion == null)
			throw new InvalidOperationException($"{definition.Name} requires a known BG3 game version of {MinimumPluginGameVersion} or newer.");
		if (_gameVersion.CompareTo(MinimumPluginGameVersion) < 0)
			throw new InvalidOperationException($"{definition.Name} requires BG3 {MinimumPluginGameVersion} (Hotfix 34) or newer.");
	}

	private NativeInstallManifest? ReadMatchingManifest(NativeInstallPlan plan)
	{
		var manifest = ReadManifest();
		if (!String.Equals(plan.ManifestFingerprint, FingerprintManifest(manifest), StringComparison.Ordinal))
			throw new InvalidOperationException("Native ownership changed after the installation review. Review and stage the archive again.");
		return manifest;
	}

	private void EnsureCommitPrerequisites(NativeModDefinition definition, NativeInstallManifest? manifest)
	{
		EnsurePluginVersion(definition);
		if (!definition.RequiresLoader) return;
		var loaderStatus = DetectLoader(manifest);
		if (!loaderStatus.IsPresent)
			throw new InvalidOperationException($"{definition.Name} requires Native Mod Loader. {loaderStatus.Description}");
	}

	private string ValidateArchivePath(string archivePath)
	{
		if (String.IsNullOrWhiteSpace(archivePath)) throw new InvalidDataException("A native archive path is required.");
		var path = Path.GetFullPath(archivePath);
		if (!String.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase)
			|| Path.GetFileName(path).Contains(':'))
			throw new InvalidDataException("Only verified ZIP native archives are supported.");
		EnsureSafeRegularFile(path);
		if (new FileInfo(path).Length <= 0 || new FileInfo(path).Length > MaximumArchiveBytes)
			throw new InvalidDataException("The native archive is empty or exceeds the safe size limit.");
		return path;
	}

	private void EnsureArchiveUnchanged(string archivePath, FileFingerprint expected)
	{
		EnsureSafeRegularFile(archivePath);
		if (!expected.Equals(CaptureFingerprint(archivePath)))
			throw new InvalidOperationException("The native archive changed after review. Download it again before continuing.");
	}

	private DestinationSnapshot CaptureDestination(string relativePath)
	{
		var path = ResolveTargetPath(relativePath, createParent: false);
		if (Directory.Exists(path)) throw new InvalidDataException($"Native target '{relativePath}' is a directory.");
		if (!PathEntryExists(path)) return new DestinationSnapshot(relativePath, false, null, 0, 0);
		if (!File.Exists(path)) throw new InvalidDataException($"Native target '{relativePath}' is not a safe regular file.");
		EnsureSafeRegularFile(path);
		var info = new FileInfo(path);
		return new DestinationSnapshot(relativePath, true, HashFile(path), info.Length, info.LastWriteTimeUtc.Ticks);
	}

	private string ResolveTargetPath(string relativePath, bool createParent)
	{
		if (!IsTargetRelativePath(relativePath)) throw new InvalidDataException("A native target path is invalid.");
		var parts = relativePath.Split('/');
		var directory = _gameBin;
		for (var index = 0; index < parts.Length - 1; index++)
		{
			directory = Path.Combine(directory, parts[index]);
			if (PathEntryExists(directory) && !Directory.Exists(directory))
				throw new InvalidDataException("A native target parent is not a safe directory.");
			if (PathEntryExists(directory)) EnsureSafeDirectoryTree(directory, create: false);
			else if (createParent) EnsureSafeDirectoryTree(directory, create: true);
		}
		var target = Path.GetFullPath(Path.Combine(directory, parts[^1]));
		if (!target.StartsWith(_gameBin + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("A native target escapes the game bin directory.");
		return target;
	}

	private string GetStagedPath(string stageDirectory, string relativePath)
	{
		if (!IsTargetRelativePath(relativePath)) throw new InvalidDataException("A staged native path is invalid.");
		var path = Path.GetFullPath(Path.Combine(stageDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
		if (!path.StartsWith(stageDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("A staged native path escapes its transaction directory.");
		return path;
	}

	private void EnsureStageDirectory(string path)
	{
		if (!path.StartsWith(_stagingDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
			|| !Guid.TryParseExact(Path.GetFileName(path), "N", out _))
			throw new InvalidDataException("A native staging directory is invalid.");
		EnsureSafeDirectoryTree(path, create: true);
	}

	private void DeleteStageDirectory(string path)
	{
		if (String.IsNullOrWhiteSpace(path)
			|| !path.StartsWith(_stagingDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
			|| !Guid.TryParseExact(Path.GetFileName(path), "N", out _)
			|| !Directory.Exists(path)) return;
		EnsureSafeDirectoryTree(path, create: false);
		Directory.Delete(path, true);
	}

	private FileStream AcquireStateLock()
	{
		EnsureSafeFileOrMissing(_stateLockPath);
		return new FileStream(_stateLockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
			1, FileOptions.WriteThrough);
	}

	private string GetBackupPath(string backupId)
	{
		if (!Guid.TryParseExact(backupId, "N", out _)) throw new InvalidDataException("A native backup identifier is invalid.");
		return Path.Combine(_backupDirectory, backupId + ".bin");
	}

	private string GetJournalPath(string transactionId)
	{
		if (!Guid.TryParseExact(transactionId, "N", out _)) throw new InvalidDataException("A native journal identifier is invalid.");
		return Path.Combine(_journalDirectory, transactionId + ".json");
	}

	private void ValidateGameBin()
	{
		EnsureSafeDirectoryTree(_gameBin, create: false);
		var executables = new[] { "bg3.exe", "bg3_dx11.exe" }
			.Select(name => Path.Combine(_gameBin, name))
			.Where(PathEntryExists)
			.ToArray();
		if (executables.Length == 0)
			throw new InvalidDataException("The selected game bin directory does not contain bg3.exe or bg3_dx11.exe.");
		foreach (var executable in executables) EnsureSafeRegularFile(executable);
	}

	private static void ThrowIfGameRunning()
	{
		foreach (var processName in new[] { "bg3", "bg3_dx11" })
		{
			foreach (var process in Process.GetProcessesByName(processName))
			{
				using (process)
				{
					try
					{
						if (!process.HasExited)
							throw new InvalidOperationException("Close Baldur's Gate 3 before changing native game files.");
					}
					catch (InvalidOperationException) { throw; }
					catch (Exception) { }
				}
			}
		}
	}

	private static void ValidateAmd64PeDll(string path)
	{
		using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
		if (stream.Length < 0x9a) throw new InvalidDataException("A native DLL is too small to be a valid AMD64 PE file.");
		Span<byte> header = stackalloc byte[0x9a];
		stream.ReadExactly(header);
		if (header[0] != (byte)'M' || header[1] != (byte)'Z')
			throw new InvalidDataException("A native DLL does not have a valid DOS header.");
		var peOffset = BitConverter.ToInt32(header.Slice(0x3c, 4));
		if (peOffset < 0x40 || peOffset > stream.Length - 26)
			throw new InvalidDataException("A native DLL has an invalid PE header offset.");
		stream.Position = peOffset;
		Span<byte> peHeader = stackalloc byte[26];
		stream.ReadExactly(peHeader);
		if (peHeader[0] != (byte)'P' || peHeader[1] != (byte)'E' || peHeader[2] != 0 || peHeader[3] != 0)
			throw new InvalidDataException("A native DLL does not have a valid PE signature.");
		if (BitConverter.ToUInt16(peHeader.Slice(4, 2)) != 0x8664
			|| (BitConverter.ToUInt16(peHeader.Slice(22, 2)) & 0x2000) == 0
			|| BitConverter.ToUInt16(peHeader.Slice(24, 2)) != 0x20b)
		{
			throw new InvalidDataException("A native DLL is not an AMD64 PE DLL.");
		}
	}

	private static bool IsAmd64PeDll(string path)
	{
		try
		{
			ValidateAmd64PeDll(path);
			return true;
		}
		catch (Exception ex) when (IsValidationException(ex)) { return false; }
	}

	private static void ValidateZipEntryPath(ZipArchiveEntry entry)
	{
		var path = entry.FullName;
		if (String.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.StartsWith("/", StringComparison.Ordinal)
			|| Path.IsPathRooted(path) || path.Contains(':') || IsReparseZipEntry(entry))
			throw new InvalidDataException("The native archive contains an unsafe entry path or link.");
		var parts = path.TrimEnd('/').Split('/');
		if (parts.Length == 0 || parts.Any(part => String.IsNullOrWhiteSpace(part) || part is "." or ".."))
			throw new InvalidDataException("The native archive contains a traversal entry path.");
	}

	private static bool IsDirectoryEntry(ZipArchiveEntry entry) => entry.FullName.EndsWith("/", StringComparison.Ordinal);

	private static bool IsReparseZipEntry(ZipArchiveEntry entry)
	{
		var unixType = (entry.ExternalAttributes >> 16) & 0xf000;
		return unixType == 0xa000 || (entry.ExternalAttributes & 0x400) != 0;
	}

	private static string ToTargetRelative(string archiveRelativePath)
	{
		if (String.IsNullOrWhiteSpace(archiveRelativePath)
			|| !archiveRelativePath.StartsWith("bin/", StringComparison.Ordinal)
			|| !IsTargetRelativePath(archiveRelativePath[4..]))
		{
			throw new InvalidDataException("The native catalog has an invalid game-bin-relative path.");
		}
		return archiveRelativePath[4..];
	}

	private static bool IsTargetRelativePath(string path)
	{
		if (String.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.StartsWith("/", StringComparison.Ordinal)
			|| path.Contains(':') || Path.IsPathRooted(path)) return false;
		var parts = path.Split('/');
		return parts.Length > 0 && parts.All(part => !String.IsNullOrWhiteSpace(part) && part is not "." and not "..");
	}

	private static NativeOwnedFile NewOwnership(DestinationSnapshot snapshot, string installedHash) => new()
	{
		RelativePath = snapshot.RelativePath,
		InstalledHash = installedHash,
		Created = !snapshot.Exists,
		OriginalHash = snapshot.Exists ? snapshot.Hash : null,
		OriginalBackupId = null
	};

	private static void EnsureOwnedFileUnchanged(NativeOwnedFile ownedFile, DestinationSnapshot snapshot)
	{
		if (!snapshot.Exists || !String.Equals(ownedFile.InstalledHash, snapshot.Hash, StringComparison.Ordinal))
			throw new InvalidOperationException($"Redux will not overwrite '{ownedFile.RelativePath}' because it changed outside Redux.");
	}

	private static bool SameSnapshot(DestinationSnapshot expected, DestinationSnapshot actual) =>
		expected.Exists == actual.Exists
		&& expected.Length == actual.Length
		&& expected.LastWriteTicks == actual.LastWriteTicks
		&& String.Equals(expected.Hash, actual.Hash, StringComparison.Ordinal);

	private static NativeOwnedInstallation? FindInstallation(NativeInstallManifest manifest, long projectId) =>
		manifest.Installations.SingleOrDefault(installation => installation.ProjectId == projectId);

	private static NativeOwnedFile? FindOwnedFile(NativeOwnedInstallation installation, string relativePath) =>
		installation.Files.SingleOrDefault(file => String.Equals(file.RelativePath, relativePath, StringComparison.Ordinal));

	private void ValidateManifest(NativeInstallManifest manifest)
	{
		if (manifest.Version != ManifestVersion || !String.Equals(manifest.GameBinIdentity, _gameBinIdentity, StringComparison.Ordinal)
			|| manifest.Installations == null || manifest.Installations.Count > NativeModCatalog.All.Count)
		{
			throw new InvalidDataException("Redux's native ownership manifest is foreign or invalid.");
		}
		var ids = new HashSet<long>();
		foreach (var installation in manifest.Installations)
		{
			if (installation == null) throw new InvalidDataException("Redux's native ownership manifest has a null installation entry.");
			var definition = NativeModCatalog.Find(installation.ProjectId);
			if (definition == null || !ids.Add(installation.ProjectId) || installation.Files == null || installation.Files.Count == 0)
				throw new InvalidDataException("Redux's native ownership manifest has an invalid installation entry.");
			var allowed = definition.RelativeFiles.Select(ToTargetRelative).ToHashSet(StringComparer.Ordinal);
			var paths = new HashSet<string>(StringComparer.Ordinal);
			foreach (var file in installation.Files)
			{
				if (file == null || !allowed.Contains(file.RelativePath) || !paths.Add(file.RelativePath)
					|| !IsHash(file.InstalledHash) || (file.Created && (!String.IsNullOrEmpty(file.OriginalHash) || !String.IsNullOrEmpty(file.OriginalBackupId)))
					|| (!file.Created && (!IsHash(file.OriginalHash) || !IsIdentifier(file.OriginalBackupId))))
				{
					throw new InvalidDataException("Redux's native ownership manifest has an unsafe file record.");
				}
			}
			var dlls = definition.RelativeFiles.Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).Select(ToTargetRelative);
			if (dlls.Any(path => !paths.Contains(path)))
				throw new InvalidDataException("Redux's native ownership manifest is missing a managed native DLL.");
			if (installation.ProjectId == 944 && paths.Count != allowed.Count)
				throw new InvalidDataException("Redux's Native Mod Loader ownership record is incomplete.");
		}
	}

	private void ValidateJournal(NativeJournal journal)
	{
		if (journal.Version != ManifestVersion || !String.Equals(journal.GameBinIdentity, _gameBinIdentity, StringComparison.Ordinal)
			|| journal.Operation is not ("install" or "restore") || NativeModCatalog.Find(journal.ProjectId) == null
			|| !IsIdentifier(journal.TransactionId) || journal.Files == null || journal.Files.Count == 0)
		{
			throw new InvalidDataException("A native installer journal is foreign or invalid.");
		}
		var definition = NativeModCatalog.Find(journal.ProjectId)!;
		var allowed = definition.RelativeFiles.Select(ToTargetRelative).ToHashSet(StringComparer.Ordinal);
		var paths = new HashSet<string>(StringComparer.Ordinal);
		foreach (var file in journal.Files)
		{
			if (file == null || !allowed.Contains(file.RelativePath) || !paths.Add(file.RelativePath) || !IsHash(file.StagedHash)
				|| (file.BeforeExists && (!IsHash(file.BeforeHash) || !IsIdentifier(file.BackupId)))
				|| (!file.BeforeExists && (!String.IsNullOrEmpty(file.BeforeHash) || !String.IsNullOrEmpty(file.BackupId))))
			{
				throw new InvalidDataException("A native installer journal contains an unsafe file record.");
			}
		}
	}

	private static NativeInstallManifest CloneManifest(NativeInstallManifest? manifest) => new()
	{
		Version = ManifestVersion,
		GameBinIdentity = manifest?.GameBinIdentity ?? String.Empty,
		Installations = manifest?.Installations.Select(CloneInstallation).ToList() ?? new List<NativeOwnedInstallation>()
	};

	private NativeInstallManifest CreateOrCloneManifest(NativeInstallManifest? manifest)
	{
		var clone = CloneManifest(manifest);
		clone.GameBinIdentity = _gameBinIdentity;
		return clone;
	}

	private static NativeOwnedInstallation CloneInstallation(NativeOwnedInstallation installation) => new()
	{
		ProjectId = installation.ProjectId,
		Files = installation.Files.Select(CloneOwnedFile).ToList()
	};

	private static NativeOwnedFile CloneOwnedFile(NativeOwnedFile file) => new()
	{
		RelativePath = file.RelativePath,
		InstalledHash = file.InstalledHash,
		Created = file.Created,
		OriginalHash = file.OriginalHash,
		OriginalBackupId = file.OriginalBackupId
	};

	private static bool IsHash(string? value)
	{
		return value != null && value.Length == 64 && value.All(character =>
			(character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'));
	}

	private static bool IsIdentifier(string? value) => value != null && Guid.TryParseExact(value, "N", out _);

	private static void EnsureNoDuplicateJsonProperties(JsonElement element)
	{
		if (element.ValueKind == JsonValueKind.Object)
		{
			var names = new HashSet<string>(StringComparer.Ordinal);
			foreach (var property in element.EnumerateObject())
			{
				if (!names.Add(property.Name)) throw new InvalidDataException("A native installer JSON record has duplicate properties.");
				EnsureNoDuplicateJsonProperties(property.Value);
			}
		}
		else if (element.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in element.EnumerateArray()) EnsureNoDuplicateJsonProperties(item);
		}
	}

	private static FileFingerprint CaptureFingerprint(string path)
	{
		EnsureSafeRegularFile(path);
		var info = new FileInfo(path);
		return new FileFingerprint(info.Length, info.LastWriteTimeUtc.Ticks, HashFile(path));
	}

	private static string HashFile(string path)
	{
		using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128000, FileOptions.SequentialScan);
		return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
	}

	private static string HashText(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

	private static string FingerprintManifest(NativeInstallManifest? manifest) => manifest == null
		? "absent"
		: HashText(JsonSerializer.Serialize(manifest, JsonOptions));

	private static string NormalizeExistingDirectory(string path, string parameterName)
	{
		if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A directory is required.", parameterName);
		var fullPath = Path.GetFullPath(path);
		EnsureSafeDirectoryTree(fullPath, create: false);
		return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
	}

	private static string NormalizeOrCreateDirectory(string path, string parameterName)
	{
		if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A directory is required.", parameterName);
		var fullPath = Path.GetFullPath(path);
		EnsureSafeDirectoryTree(fullPath, create: true);
		return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
	}

	private static void EnsureSafeDirectoryTree(string directory, bool create)
	{
		var fullPath = Path.GetFullPath(directory);
		if (create) Directory.CreateDirectory(fullPath);
		if (!Directory.Exists(fullPath)) throw new DirectoryNotFoundException("A required native installer directory does not exist.");
		for (var current = new DirectoryInfo(fullPath); current != null; current = current.Parent)
		{
			var attributes = File.GetAttributes(current.FullName);
			if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) == 0)
				throw new InvalidDataException("Native installer paths cannot traverse symlinks, junctions, or non-directory ancestors.");
		}
	}

	private static void EnsureSafeRegularFile(string path)
	{
		if (!PathEntryExists(path) || Directory.Exists(path) || !File.Exists(path)) throw new FileNotFoundException("A required native installer file is missing or unsafe.", path);
		var attributes = File.GetAttributes(path);
		if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
			throw new InvalidDataException("Native installer files cannot be symlinks, junctions, or directories.");
		EnsureSafeDirectoryTree(Path.GetDirectoryName(Path.GetFullPath(path))!, create: false);
	}

	private static void EnsureSafeFileOrMissing(string path)
	{
		if (PathEntryExists(path) && Directory.Exists(path))
			throw new InvalidDataException("A native installer file path is occupied by a directory.");
		if (PathEntryExists(path)) EnsureSafeRegularFile(path);
		else EnsureSafeDirectoryTree(Path.GetDirectoryName(Path.GetFullPath(path))!, create: false);
	}

	private static bool PathEntryExists(string path)
	{
		try
		{
			_ = File.GetAttributes(path);
			return true;
		}
		catch (FileNotFoundException) { return false; }
		catch (DirectoryNotFoundException) { return false; }
	}

	private static bool IsValidationException(Exception exception) => exception is
		IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException or JsonException;

	internal sealed record FileFingerprint(long Length, long LastWriteTicks, string Hash);
	internal sealed record DestinationSnapshot(string RelativePath, bool Exists, string? Hash, long Length, long LastWriteTicks);
	internal sealed record TransactionBackup(string RelativePath, string BackupId, string BackupPath, DestinationSnapshot Before);

	internal sealed class NativeInstallPlan
	{
		public NativeModDefinition Definition { get; }
		public string ManifestFingerprint { get; }
		public string ArchivePath { get; }
		public FileFingerprint ArchiveFingerprint { get; }
		public string StageDirectory { get; }
		public string TransactionId { get; }
		public IReadOnlyList<DestinationSnapshot> Guards { get; }
		public IReadOnlyList<PlannedWrite> Writes { get; }
		public NativeOwnedInstallation InstallationAfter { get; }
		public string ReviewText { get; }

		public NativeInstallPlan(NativeModDefinition definition, string manifestFingerprint, string archivePath,
			FileFingerprint archiveFingerprint, string stageDirectory, string transactionId,
			IReadOnlyList<DestinationSnapshot> guards, IReadOnlyList<PlannedWrite> writes,
			NativeOwnedInstallation installationAfter, string reviewText)
		{
			Definition = definition;
			ManifestFingerprint = manifestFingerprint;
			ArchivePath = archivePath;
			ArchiveFingerprint = archiveFingerprint;
			StageDirectory = stageDirectory;
			TransactionId = transactionId;
			Guards = guards;
			Writes = writes;
			InstallationAfter = installationAfter;
			ReviewText = reviewText;
		}
	}

	internal sealed class PlannedWrite
	{
		public string RelativePath { get; }
		public string? StagedHash { get; }
		public DestinationSnapshot Before { get; }
		public NativeOwnedFile Ownership { get; }

		public PlannedWrite(string relativePath, string? stagedHash, DestinationSnapshot before, NativeOwnedFile ownership)
		{
			RelativePath = relativePath;
			StagedHash = stagedHash;
			Before = before;
			Ownership = ownership;
		}
	}

	internal sealed class NativeInstallManifest
	{
		public int Version { get; set; }
		public string GameBinIdentity { get; set; } = String.Empty;
		public List<NativeOwnedInstallation> Installations { get; set; } = new();
	}

	internal sealed class NativeOwnedInstallation
	{
		public long ProjectId { get; set; }
		public List<NativeOwnedFile> Files { get; set; } = new();
	}

	internal sealed class NativeOwnedFile
	{
		public string RelativePath { get; set; } = String.Empty;
		public string InstalledHash { get; set; } = String.Empty;
		public bool Created { get; set; }
		public string? OriginalHash { get; set; }
		public string? OriginalBackupId { get; set; }
	}

	private sealed class NativeJournal
	{
		public int Version { get; set; }
		public string GameBinIdentity { get; set; } = String.Empty;
		public string Operation { get; set; } = String.Empty;
		public long ProjectId { get; set; }
		public string TransactionId { get; set; } = String.Empty;
		public List<NativeJournalFile> Files { get; set; } = new();
	}

	private sealed class NativeJournalFile
	{
		public string RelativePath { get; set; } = String.Empty;
		public bool BeforeExists { get; set; }
		public string? BeforeHash { get; set; }
		public string? StagedHash { get; set; }
		public string? BackupId { get; set; }
	}
}

public sealed class NativeModInstallTransaction : IAsyncDisposable
{
	private NativeModInstaller? _installer;
	private NativeModInstaller.NativeInstallPlan? _plan;
	private bool _committed;

	internal NativeModInstallTransaction(NativeModInstaller installer, NativeModInstaller.NativeInstallPlan plan, string reviewText)
	{
		_installer = installer;
		_plan = plan;
		ReviewText = reviewText;
	}

	public string ReviewText { get; }

	public async Task CommitAsync(CancellationToken cancellationToken = default)
	{
		if (_committed) throw new InvalidOperationException("This native installation transaction was already committed.");
		var installer = _installer ?? throw new ObjectDisposedException(nameof(NativeModInstallTransaction));
		var plan = _plan ?? throw new ObjectDisposedException(nameof(NativeModInstallTransaction));
		await installer.CommitAsync(plan, cancellationToken);
		_committed = true;
	}

	public async ValueTask DisposeAsync()
	{
		var installer = Interlocked.Exchange(ref _installer, null);
		var plan = Interlocked.Exchange(ref _plan, null);
		if (installer != null && plan != null) await installer.DiscardStageAsync(plan);
	}
}
