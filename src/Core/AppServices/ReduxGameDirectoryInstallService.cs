using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

using DivinityModManager.Models.NexusMods;

using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace DivinityModManager.AppServices;

public enum ReduxGameDirectoryModKind
{
	NativeLoader,
	NativePlugin,
	ExistingReduxWorkflow
}

public sealed record ReduxGameDirectoryModLayout(
	string Name,
	IReadOnlyDictionary<string, string> ManagedEntries,
	IReadOnlyList<string> PackageEntries,
	IReadOnlyList<string> IgnoredEntries);

public sealed record ReduxGameDirectoryModDefinition(
	long NexusModId,
	string PackageId,
	string Name,
	ReduxGameDirectoryModKind Kind,
	bool RequiresLoader,
	IReadOnlyList<string> RelativeFiles,
	IReadOnlySet<string> PreserveExistingFiles,
	IReadOnlyList<ReduxGameDirectoryModLayout> Layouts,
	string SourceUrl,
	string Requirements)
{
	public bool SupportsGuardedInstall => Kind != ReduxGameDirectoryModKind.ExistingReduxWorkflow;
}

public static class ReduxGameDirectoryModCatalog
{
	private static IReadOnlyDictionary<string, string> Map(params (string Archive, string Canonical)[] entries) =>
		new Dictionary<string, string>(entries.ToDictionary(item => item.Archive, item => item.Canonical), StringComparer.OrdinalIgnoreCase);
	private static IReadOnlyList<string> Files(params string[] entries) => Array.AsReadOnly(entries);
	private static IReadOnlySet<string> Preserve(params string[] entries) => new HashSet<string>(entries, StringComparer.OrdinalIgnoreCase);
	private static ReduxGameDirectoryModLayout Layout(string name, IReadOnlyDictionary<string, string> managed,
		IReadOnlyList<string>? packages = null, IReadOnlyList<string>? ignored = null) =>
		new(name, managed, packages ?? Array.Empty<string>(), ignored ?? Array.Empty<string>());

	public static IReadOnlyList<ReduxGameDirectoryModDefinition> All { get; } = Array.AsReadOnly(new[]
	{
		new ReduxGameDirectoryModDefinition(944, "native-mod-loader", "Native Mod Loader", ReduxGameDirectoryModKind.NativeLoader, false,
			Files("bin/bink2w64.dll", "bin/bink2w64_original.dll"), Preserve(),
			[Layout("Standard", Map(("bin/bink2w64.dll", "bin/bink2w64.dll"), ("bin/bink2w64_original.dll", "bin/bink2w64_original.dll")))],
			"https://www.nexusmods.com/baldursgate3/mods/944", "Replaces the game's bink2w64 loader and retains the packaged original."),
		new ReduxGameDirectoryModDefinition(781, "bg3-wasd", "WASD Character Movement", ReduxGameDirectoryModKind.NativePlugin, true,
			Files("bin/NativeMods/BG3WASD.dll", "bin/NativeMods/BG3WASD.toml"), Preserve("bin/NativeMods/BG3WASD.toml"),
			[Layout("Standard", Map(("bin/NativeMods/BG3WASD.dll", "bin/NativeMods/BG3WASD.dll"), ("bin/NativeMods/BG3WASD.toml", "bin/NativeMods/BG3WASD.toml")))],
			"https://www.nexusmods.com/baldursgate3/mods/781", "Requires Native Mod Loader."),
		new ReduxGameDirectoryModDefinition(945, "native-camera-tweaks", "Native Camera Tweaks", ReduxGameDirectoryModKind.NativePlugin, true,
			Files("bin/NativeMods/BG3NativeCameraTweaks.dll", "bin/NativeMods/BG3NativeCameraTweaks.toml"), Preserve("bin/NativeMods/BG3NativeCameraTweaks.toml"),
			[Layout("Standard", Map(("bin/NativeMods/BG3NativeCameraTweaks.dll", "bin/NativeMods/BG3NativeCameraTweaks.dll"), ("bin/NativeMods/BG3NativeCameraTweaks.toml", "bin/NativeMods/BG3NativeCameraTweaks.toml")))],
			"https://www.nexusmods.com/baldursgate3/mods/945", "Requires Native Mod Loader."),
		new ReduxGameDirectoryModDefinition(22892, "native-camera-tweaks", "Native Camera Tweaks with GUI", ReduxGameDirectoryModKind.NativePlugin, true,
			Files("bin/NativeMods/BG3NativeCameraTweaks.dll", "bin/NativeMods/BG3NativeCameraTweaks.toml"), Preserve("bin/NativeMods/BG3NativeCameraTweaks.toml"),
			[Layout("Standard", Map(("bin/NativeMods/BG3NativeCameraTweaks.dll", "bin/NativeMods/BG3NativeCameraTweaks.dll"), ("bin/NativeMods/BG3NativeCameraTweaks.toml", "bin/NativeMods/BG3NativeCameraTweaks.toml")))],
			"https://www.nexusmods.com/baldursgate3/mods/22892", "Requires Native Mod Loader; supersedes the earlier Native Camera Tweaks project."),
		new ReduxGameDirectoryModDefinition(668, "achievement-enabler", "Achievement Enabler", ReduxGameDirectoryModKind.NativePlugin, true,
			Files("bin/NativeMods/BG3AchievementEnabler.dll"), Preserve(),
			[
				Layout("Game root", Map(("bin/NativeMods/BG3AchievementEnabler.dll", "bin/NativeMods/BG3AchievementEnabler.dll"))),
				Layout("Bin contents", Map(("NativeMods/BG3AchievementEnabler.dll", "bin/NativeMods/BG3AchievementEnabler.dll")))
			],
			"https://www.nexusmods.com/baldursgate3/mods/668", "Requires Native Mod Loader."),
		new ReduxGameDirectoryModDefinition(1326, "baldurs-priority", "Baldur's Priority", ReduxGameDirectoryModKind.NativePlugin, true,
			Files("bin/NativeMods/CpuOptimizer.dll", "bin/NativeMods/CpuOptimizer.ini"), Preserve("bin/NativeMods/CpuOptimizer.ini"),
			[Layout("Standard", Map(("bin/NativeMods/CpuOptimizer.dll", "bin/NativeMods/CpuOptimizer.dll"), ("bin/NativeMods/CpuOptimizer.ini", "bin/NativeMods/CpuOptimizer.ini")))],
			"https://www.nexusmods.com/baldursgate3/mods/1326", "Requires Native Mod Loader."),
		new ReduxGameDirectoryModDefinition(742, "improved-camera", "Improved Camera", ReduxGameDirectoryModKind.NativePlugin, true,
			Files("bin/NativeMods/BGIII_ImprovedCamera.dll", "bin/NativeMods/BGIII_ImprovedCamera.toml"), Preserve("bin/NativeMods/BGIII_ImprovedCamera.toml"),
			[Layout("Legacy wrapper", Map(("BGIII_ImprovedCamera - DLL/BGIII_ImprovedCamera.dll", "bin/NativeMods/BGIII_ImprovedCamera.dll"), ("BGIII_ImprovedCamera - DLL/BGIII_ImprovedCamera.toml", "bin/NativeMods/BGIII_ImprovedCamera.toml")))],
			"https://www.nexusmods.com/baldursgate3/mods/742", "Requires Native Mod Loader."),
		new ReduxGameDirectoryModDefinition(23881, "best-of-hands", "Best of Hands", ReduxGameDirectoryModKind.NativePlugin, true,
			Files("bin/NativeMods/BestofHands.dll"), Preserve(),
			[Layout("Native and PAK", Map(("bin/NativeMods/BestofHands.dll", "bin/NativeMods/BestofHands.dll")), Files("BestofHands.pak"), Files("info.json"))],
			"https://www.nexusmods.com/baldursgate3/mods/23881", "Requires Native Mod Loader; its PAK must be reviewed by Redux's normal package installer."),
		new ReduxGameDirectoryModDefinition(23413, "bg3-wasd", "BG3WASD Camera Follow", ReduxGameDirectoryModKind.NativePlugin, true,
			Files("bin/NativeMods/BG3WASD.dll", "bin/NativeMods/BG3WASD.toml"), Preserve("bin/NativeMods/BG3WASD.toml"),
			[Layout("Native and PAK", Map(("bin/NativeMods/BG3WASD.dll", "bin/NativeMods/BG3WASD.dll"), ("bin/NativeMods/BG3WASD.toml", "bin/NativeMods/BG3WASD.toml")), Files("Mods/BG3YawBridge.pak"), Files("CREDITS.txt", "LICENSE.txt", "README.txt"))],
			"https://www.nexusmods.com/baldursgate3/mods/23413", "Requires Native Mod Loader; its PAK must be reviewed by Redux's normal package installer."),
		new ReduxGameDirectoryModDefinition(23959, "true-third-person-camera", "True Third-Person Camera", ReduxGameDirectoryModKind.NativePlugin, true,
			Files("bin/NativeMods/TrueThirdPersonCamera.dll"), Preserve(),
			[Layout("Guided wrapper", Map(("1 - Main Game Folder Files/bin/NativeMods/TrueThirdPersonCamera.dll", "bin/NativeMods/TrueThirdPersonCamera.dll")), Files("2 - BG3 Mod Manager File/TrueThirdPersonCamera.pak"), Files("README.txt"))],
			"https://www.nexusmods.com/baldursgate3/mods/23959", "Requires Native Mod Loader and removal of legacy camera files before first installation; its PAK must use Redux's normal package installer."),
		new ReduxGameDirectoryModDefinition(24804, "bg3fgvk", "bg3fgvk", ReduxGameDirectoryModKind.NativePlugin, true,
			Files(
				"bin/NativeMods/fgvk.dll",
				"bin/NativeMods/Streamline/NvLowLatencyVk.dll",
				"bin/NativeMods/Streamline/nvngx_dlssg.dll",
				"bin/NativeMods/Streamline/README-STREAMLINE.txt",
				"bin/NativeMods/Streamline/sl.common.dll",
				"bin/NativeMods/Streamline/sl.dlss_g.dll",
				"bin/NativeMods/Streamline/sl.interposer.dll",
				"bin/NativeMods/Streamline/sl.pcl.dll",
				"bin/NativeMods/Streamline/sl.reflex.dll",
				"bin/NativeMods/Streamline/STREAMLINE-LICENSE.txt"), Preserve(),
			[Layout("Bin contents", Map(
				("NativeMods/fgvk.dll", "bin/NativeMods/fgvk.dll"),
				("NativeMods/Streamline/NvLowLatencyVk.dll", "bin/NativeMods/Streamline/NvLowLatencyVk.dll"),
				("NativeMods/Streamline/nvngx_dlssg.dll", "bin/NativeMods/Streamline/nvngx_dlssg.dll"),
				("NativeMods/Streamline/README-STREAMLINE.txt", "bin/NativeMods/Streamline/README-STREAMLINE.txt"),
				("NativeMods/Streamline/sl.common.dll", "bin/NativeMods/Streamline/sl.common.dll"),
				("NativeMods/Streamline/sl.dlss_g.dll", "bin/NativeMods/Streamline/sl.dlss_g.dll"),
				("NativeMods/Streamline/sl.interposer.dll", "bin/NativeMods/Streamline/sl.interposer.dll"),
				("NativeMods/Streamline/sl.pcl.dll", "bin/NativeMods/Streamline/sl.pcl.dll"),
				("NativeMods/Streamline/sl.reflex.dll", "bin/NativeMods/Streamline/sl.reflex.dll"),
				("NativeMods/Streamline/STREAMLINE-LICENSE.txt", "bin/NativeMods/Streamline/STREAMLINE-LICENSE.txt")),
				ignored: Files("INSTALL.txt", "LICENSE.txt", "README.md"))],
			"https://www.nexusmods.com/baldursgate3/mods/24804", "Requires Native Mod Loader, Vulkan, supported NVIDIA hardware, and hardware-accelerated GPU scheduling."),
		new ReduxGameDirectoryModDefinition(2172, "script-extender", "Baldur's Gate 3 Script Extender", ReduxGameDirectoryModKind.ExistingReduxWorkflow, false,
			Files("bin/DWrite.dll"), Preserve(),
			[Layout("Bin contents", Map(("DWrite.dll", "bin/DWrite.dll")))],
			"https://www.nexusmods.com/baldursgate3/mods/2172", "Handled by Redux's existing Script Extender workflow rather than the native-mod manager.")
	});

	public static ReduxGameDirectoryModDefinition? Find(long id) => All.FirstOrDefault(definition => definition.NexusModId == id);
	public static IReadOnlyList<ReduxGameDirectoryModDefinition> FindByPackageId(string packageId) => All
		.Where(definition => definition.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase))
		.ToArray();
}

public sealed record ReduxNativeLoaderStatus(bool IsPresent, bool IsVerified, string Description);

public sealed record ReduxGameDirectoryArchiveFile(
	string ArchivePath,
	string DestinationPath,
	bool PreserveExisting);

public sealed record ReduxGameDirectoryArchiveInspection(
	ReduxGameDirectoryModDefinition Definition,
	string LayoutName,
	string ArchivePath,
	string ArchiveHash,
	int FileCount,
	long ExpandedBytes,
	IReadOnlyList<ReduxGameDirectoryArchiveFile> ManagedFiles,
	IReadOnlyList<string> PackageEntries,
	IReadOnlyList<string> IgnoredEntries);

public enum ReduxGameDirectoryModStatus
{
	Managed,
	Changed,
	Missing,
	External,
	RecoveryRequired
}

public sealed record ReduxGameDirectoryModEntry(
	string PackageId,
	long NexusModId,
	string Name,
	string SourceUrl,
	string ArchiveName,
	ReduxGameDirectoryModStatus Status,
	string StatusText,
	IReadOnlyList<string> Files,
	bool CanRestore);

public sealed class ReduxGameDirectoryRecoveryException(string message, Exception? innerException = null) : IOException(message, innerException) { }
public sealed class ReduxUnsupportedGameDirectoryArchiveException(string message) : IOException(message) { }

/// <summary>
/// Installs only the explicitly reviewed native-mod archives. Archive structure and PE headers are
/// validated locally; this is not a provenance or universal game-compatibility assertion.
/// </summary>
public sealed class ReduxGameDirectoryInstallService
{
	private const int ManifestVersion = 2;
	private const long MaximumArchiveBytes = 128L * 1024 * 1024;
	private const long MaximumEntryBytes = 32L * 1024 * 1024;
	private const long MaximumExpandedBytes = 64L * 1024 * 1024;
	private const long MaximumManifestBytes = 256 * 1024;
	private const int MaximumArchiveEntries = 128;
	private const int MaximumCompressionRatio = 200;
	private const long CompressionRatioMinimumBytes = 1024 * 1024;
	private static readonly HashSet<string> SupportedArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".7z", ".7zip", ".rar", ".zip"
	};
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
	public string RecoveryDirectory => _stateRoot;

	public ReduxGameDirectoryInstallService(string gameBin, string stateDirectory, Version gameVersion)
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

	public ReduxNativeLoaderStatus DetectLoader()
	{
		try
		{
			ValidateGameBin();
			return DetectLoader(ReadManifest());
		}
		catch (Exception ex) when (IsValidationException(ex))
		{
			return new ReduxNativeLoaderStatus(false, false,
				"Native Mod Loader status is unavailable because Redux ownership records or game files could not be safely verified.");
		}
	}

	public IReadOnlyList<ReduxGameDirectoryModEntry> GetInstalledMods()
	{
		ValidateGameBin();
		var manifest = ReadManifest();
		var results = new List<ReduxGameDirectoryModEntry>();
		if (manifest != null)
		{
			foreach (var installation in manifest.Installations)
			{
				var definition = ReduxGameDirectoryModCatalog.Find(installation.NexusModId)!;
				var snapshots = installation.Files.Select(file => (File: file, Current: CaptureDestination(file.RelativePath))).ToArray();
				var missing = snapshots.Any(item => !item.Current.Exists);
				var changed = !missing && snapshots.Any(item =>
					!String.Equals(item.File.InstalledHash, item.Current.Hash, StringComparison.Ordinal));
				var status = missing ? ReduxGameDirectoryModStatus.Missing
					: changed ? ReduxGameDirectoryModStatus.Changed : ReduxGameDirectoryModStatus.Managed;
				var statusText = missing ? "Managed files are missing"
					: changed ? "Changed outside Redux" : "Managed by Redux";
				results.Add(new ReduxGameDirectoryModEntry(
					installation.PackageId, installation.NexusModId, installation.Name, installation.SourceUrl,
					installation.ArchiveName,
					status, statusText, installation.Files.Select(file => file.RelativePath).ToArray(),
					status == ReduxGameDirectoryModStatus.Managed));
			}
		}

		var managedPackages = results.Select(result => result.PackageId).ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (var package in ReduxGameDirectoryModCatalog.All.Where(definition => definition.SupportsGuardedInstall)
			.GroupBy(definition => definition.PackageId, StringComparer.OrdinalIgnoreCase))
		{
			if (managedPackages.Contains(package.Key)) continue;
			var existingPaths = package.SelectMany(definition => definition.RelativeFiles)
				.Select(ToTargetRelative).Distinct(StringComparer.Ordinal)
				.Where(relativePath => CaptureDestination(relativePath).Exists).ToArray();
			if (existingPaths.Length == 0) continue;
			var representative = package.OrderByDescending(definition => definition.NexusModId).First();
			var externalName = package.Key switch
			{
				"native-camera-tweaks" => "Native Camera Tweaks",
				"bg3-wasd" => "BG3WASD",
				_ => representative.Name
			};
			results.Add(new ReduxGameDirectoryModEntry(
				package.Key, representative.NexusModId,
				externalName,
				String.Empty, String.Empty, ReduxGameDirectoryModStatus.External, "Installed outside Redux",
				existingPaths, false));
		}

		var nativeModsDirectory = Path.Combine(_gameBin, "NativeMods");
		if (Directory.Exists(nativeModsDirectory))
		{
			EnsureSafeDirectoryContents(nativeModsDirectory);
			var catalogPaths = ReduxGameDirectoryModCatalog.All.SelectMany(definition => definition.RelativeFiles)
				.Select(ToTargetRelative).ToHashSet(StringComparer.Ordinal);
			var otherDlls = Directory.EnumerateFiles(nativeModsDirectory, "*.dll", SearchOption.AllDirectories)
				.Select(path => Path.GetRelativePath(_gameBin, path).Replace('\\', '/'))
				.Where(path => !catalogPaths.Contains(path))
				.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
				.Take(65).ToArray();
			if (otherDlls.Length > 0)
			{
				var displayed = otherDlls.Take(64).ToArray();
				results.Add(new ReduxGameDirectoryModEntry(
					"external-native-files", -1, "Other native files", String.Empty, String.Empty,
					ReduxGameDirectoryModStatus.External,
					otherDlls.Length > 64 ? "Installed outside Redux · more than 64 DLLs" : "Installed outside Redux",
					displayed, false));
			}
		}

		if (Directory.EnumerateFiles(_journalDirectory, "*.json", SearchOption.TopDirectoryOnly).Any())
		{
			results.Insert(0, new ReduxGameDirectoryModEntry(
				"redux-recovery", -1, "Incomplete game-directory change", String.Empty, String.Empty,
				ReduxGameDirectoryModStatus.RecoveryRequired,
				"Recovery review required before the game is launched", Array.Empty<string>(), false));
		}

		return results.OrderBy(entry => entry.Status == ReduxGameDirectoryModStatus.RecoveryRequired ? 0 : 1)
			.ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToArray();
	}

	/// <summary>
	/// Conservatively identifies a reviewed game-directory archive without changing either Redux state
	/// or the game. Layout alone is sufficient only when it identifies one catalog project; overlapping
	/// layouts additionally require a Nexus-generated filename carrying the matching project id.
	/// </summary>
	public static ReduxGameDirectoryArchiveInspection? TryInspectKnownArchive(string archivePath, bool computeArchiveHash = true)
	{
		var normalizedArchive = ValidateArchivePath(archivePath);
		using var source = new FileStream(normalizedArchive, FileMode.Open, FileAccess.Read, FileShare.Read,
			128000, FileOptions.SequentialScan);
		try
		{
			using var archive = ArchiveFactory.OpenArchive(source, new ReaderOptions());
			var entries = archive.Entries.ToArray();
			ValidateArchiveEntrySet(entries);
			var matching = new List<(ReduxGameDirectoryModDefinition Definition, ReduxGameDirectoryModLayout Layout)>();
			foreach (var definition in ReduxGameDirectoryModCatalog.All)
			{
				foreach (var layout in FindMatchingLayouts(definition, entries))
					matching.Add((definition, layout));
			}

			if (matching.Count == 0)
			{
				if (entries.Any(entry => !entry.IsDirectory
					&& NormalizeArchivePath(entry.Key).EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
				{
					throw new ReduxUnsupportedGameDirectoryArchiveException(
						"This archive contains native DLLs but does not match a reviewed Redux game-directory layout. No files were changed.");
				}
				return null;
			}
			var nexusIdentity = NexusModFileVersionData.FromFilePath(normalizedArchive);
			var projectMatches = nexusIdentity.Success
				? matching.Where(match => match.Definition.NexusModId == nexusIdentity.ModId).ToArray()
				: Array.Empty<(ReduxGameDirectoryModDefinition Definition, ReduxGameDirectoryModLayout Layout)>();
			var selected = projectMatches.Length == 1
				? projectMatches[0]
				: matching.Count == 1 ? matching[0] : default;
			if (selected.Definition == null)
				throw new ReduxUnsupportedGameDirectoryArchiveException(
					"This native archive matches more than one reviewed package. Keep its Nexus-generated filename so Redux can identify the intended project safely.");

			var files = entries.Where(entry => !entry.IsDirectory).ToArray();
			var managed = selected.Layout.ManagedEntries.Select(mapping => new ReduxGameDirectoryArchiveFile(
				NormalizeArchivePath(mapping.Key), mapping.Value,
				selected.Definition.PreserveExistingFiles.Contains(mapping.Value))).ToArray();
			return new ReduxGameDirectoryArchiveInspection(
				selected.Definition,
				selected.Layout.Name,
				normalizedArchive,
				computeArchiveHash ? HashFile(normalizedArchive) : String.Empty,
				files.Length,
				files.Sum(entry => entry.Size),
				managed,
				selected.Layout.PackageEntries.Select(NormalizeArchivePath).ToArray(),
				selected.Layout.IgnoredEntries.Select(NormalizeArchivePath).ToArray());
		}
		catch (Exception ex) when (ex is SharpCompressException or InvalidOperationException or OverflowException)
		{
			throw new InvalidDataException("Redux could not inspect this game-directory archive safely.", ex);
		}
	}

	public async Task InspectArchiveAsync(long projectId, string archivePath, CancellationToken cancellationToken = default)
	{
		var definition = ReduxGameDirectoryModCatalog.Find(projectId)
			?? throw new InvalidDataException("This Nexus project is not approved for native installation.");
		if (!definition.SupportsGuardedInstall)
			throw new InvalidOperationException($"{definition.Name} uses an existing Redux installation workflow.");
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

	public async Task<ReduxGameDirectoryInstallTransaction> StageAsync(long projectId, string archivePath,
		CancellationToken cancellationToken = default)
	{
		var definition = ReduxGameDirectoryModCatalog.Find(projectId)
			?? throw new InvalidDataException("This Nexus project is not approved for native installation.");
		if (!definition.SupportsGuardedInstall)
			throw new InvalidOperationException($"{definition.Name} uses an existing Redux installation workflow.");
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
			EnsurePackageSpecificPrerequisites(definition);
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
				return new ReduxGameDirectoryInstallTransaction(this, plan, plan.ReviewText);
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
		var definition = ReduxGameDirectoryModCatalog.Find(projectId)
			?? throw new InvalidDataException("This Nexus project is not managed by the native installer.");
		if (!definition.SupportsGuardedInstall)
			throw new InvalidOperationException($"{definition.Name} uses an existing Redux installation workflow.");
		await _operationGate.WaitAsync(cancellationToken);
		try
		{
			using var stateLock = AcquireStateLock();
			ValidateGameBin();
			ThrowIfGameRunning();
			EnsureNoPendingJournals();
			var manifest = ReadManifest() ?? throw new InvalidOperationException("Redux has no native installation record for this game.");
			var installation = FindInstallation(manifest, definition.PackageId)
				?? throw new InvalidOperationException("Redux does not own this native installation.");

			if (definition.Kind == ReduxGameDirectoryModKind.NativeLoader) EnsureNoNativePluginDllsRemain();
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

				manifest.Installations.RemoveAll(item => PackageIdsEqual(item.PackageId, definition.PackageId));
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
					throw new ReduxGameDirectoryRecoveryException("Game-directory restore failed and Redux could not safely roll back every changed file. Do not launch the game; inspect the recovery journal and backups.", rollbackError);
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
				nextManifest.Installations.RemoveAll(item => PackageIdsEqual(item.PackageId, plan.Definition.PackageId));
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
					throw new ReduxGameDirectoryRecoveryException("Game-directory installation failed and Redux could not safely roll back every changed file. Do not launch the game; inspect the recovery journal and backups.", rollbackError);
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

	private NativeInstallPlan BuildInstallPlan(ReduxGameDirectoryModDefinition definition, NativeInstallManifest? manifest,
		string manifestFingerprint, string archivePath, FileFingerprint archiveFingerprint, string stageDirectory, string transactionId,
		IReadOnlyDictionary<string, string> stagedFiles, ReduxNativeLoaderStatus loaderStatus)
	{
		var existing = manifest == null ? null : FindInstallation(manifest, definition.PackageId);
		var snapshots = definition.RelativeFiles
			.Select(path => CaptureDestination(ToTargetRelative(path)))
			.ToDictionary(snapshot => snapshot.RelativePath, StringComparer.Ordinal);
		var guards = snapshots.Values.ToList();
		if (definition.RequiresLoader)
		{
			foreach (var loaderFile in ReduxGameDirectoryModCatalog.Find(944)!.RelativeFiles)
				guards.Add(CaptureDestination(ToTargetRelative(loaderFile)));
		}
		var stagedHashes = stagedFiles.ToDictionary(pair => pair.Key, pair => HashFile(pair.Value), StringComparer.Ordinal);
		var installation = new NativeOwnedInstallation
		{
			PackageId = definition.PackageId,
			NexusModId = definition.NexusModId,
			Name = definition.Name,
			SourceUrl = definition.SourceUrl,
			ArchiveName = Path.GetFileName(archivePath),
			ArchiveHash = archiveFingerprint.Hash,
			InstalledAtUtc = DateTimeOffset.UtcNow
		};
		var writes = new List<PlannedWrite>();

		if (definition.Kind == ReduxGameDirectoryModKind.NativeLoader)
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

	private static void BuildPluginPlan(ReduxGameDirectoryModDefinition definition, NativeOwnedInstallation? existing,
		IReadOnlyDictionary<string, DestinationSnapshot> snapshots, IReadOnlyDictionary<string, string> stagedHashes,
		NativeOwnedInstallation installation, ICollection<PlannedWrite> writes)
	{
		foreach (var canonicalPath in definition.RelativeFiles)
		{
			var relativePath = ToTargetRelative(canonicalPath);
			var snapshot = snapshots[relativePath];
			var prior = existing == null ? null : FindOwnedFile(existing, relativePath);
			if (definition.PreserveExistingFiles.Contains(canonicalPath))
			{
				// Configuration belongs to the player. Place a default only when none exists,
				// then leave it unowned so ordinary edits never block update or restore.
				if (!snapshot.Exists)
				{
					var defaultConfiguration = NewOwnership(snapshot, stagedHashes[relativePath]);
					writes.Add(new PlannedWrite(relativePath, stagedHashes[relativePath], snapshot, defaultConfiguration));
				}
				continue;
			}

			if (snapshot.Exists)
			{
				if (prior == null)
					throw new InvalidOperationException($"Redux will not replace the unmanaged native file '{relativePath}'.");
				EnsureOwnedFileUnchanged(prior, snapshot);
			}
			else if (prior != null)
			{
				throw new InvalidOperationException($"Redux will not recreate the Redux-owned file '{relativePath}' after it was removed outside Redux.");
			}

			var next = prior == null ? NewOwnership(snapshot, stagedHashes[relativePath]) : CloneOwnedFile(prior);
			next.InstalledHash = stagedHashes[relativePath];
			installation.Files.Add(next);
			writes.Add(new PlannedWrite(relativePath, stagedHashes[relativePath], snapshot, next));
		}
	}

	private async Task<IReadOnlyDictionary<string, string>> ExtractValidatedArchiveAsync(ReduxGameDirectoryModDefinition definition,
		string archivePath, string stageDirectory, CancellationToken cancellationToken)
	{
		try
		{
			using var source = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read,
				128000, FileOptions.SequentialScan);
			using var archive = ArchiveFactory.OpenArchive(source, new ReaderOptions());
			var entries = archive.Entries.ToArray();
			var layout = ValidateArchiveEntries(definition, entries);
			var filesByPath = entries.Where(entry => !entry.IsDirectory)
				.ToDictionary(entry => NormalizeArchivePath(entry.Key), StringComparer.OrdinalIgnoreCase);
			var stagedFiles = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (var mapping in layout.ManagedEntries)
			{
				var targetRelative = ToTargetRelative(mapping.Value);
				var stagedPath = GetStagedPath(stageDirectory, targetRelative);
				await CopyArchiveEntryAsync(filesByPath[mapping.Key], stagedPath, cancellationToken);
				if (mapping.Value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) ValidateAmd64PeDll(stagedPath);
				stagedFiles.Add(targetRelative, stagedPath);
			}
			return stagedFiles;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex) when (ex is SharpCompressException or InvalidOperationException)
		{
			throw new InvalidDataException("Redux could not read this native archive safely.", ex);
		}
	}

	private static ReduxGameDirectoryModLayout ValidateArchiveEntries(ReduxGameDirectoryModDefinition definition,
		IReadOnlyList<IArchiveEntry> archiveEntries)
	{
		ValidateArchiveEntrySet(archiveEntries);
		var matches = FindMatchingLayouts(definition, archiveEntries);
		if (matches.Count == 0)
			throw new InvalidDataException("The native archive does not match a reviewed package layout or contains unexpected files.");
		if (matches.Count > 1)
			throw new InvalidDataException("The native archive matches more than one package layout and cannot be selected safely.");
		return matches[0];
	}

	private static void ValidateArchiveEntrySet(IReadOnlyList<IArchiveEntry> archiveEntries)
	{
		if (archiveEntries.Count(entry => !entry.IsDirectory) > MaximumArchiveEntries)
			throw new InvalidDataException("The native archive contains too many entries.");
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		long expandedBytes = 0;
		foreach (var entry in archiveEntries)
		{
			ValidateArchiveEntryPath(entry);
			var normalized = NormalizeArchivePath(entry.Key).TrimEnd('/');
			if (!seen.Add(normalized))
				throw new InvalidDataException("The native archive contains paths that differ only by case.");
			if (entry.IsDirectory) continue;
			files.Add(normalized);
			if (entry.IsEncrypted)
				throw new InvalidDataException("Encrypted native archives are not supported.");
			if (entry.Size <= 0 || entry.Size > MaximumEntryBytes)
				throw new InvalidDataException("The native archive contains an empty or oversized file.");
			// Solid 7z/RAR readers may not expose a meaningful compressed size for each
			// member. The absolute per-entry and total expansion ceilings still apply.
			if (entry.CompressedSize > 0 && entry.Size >= CompressionRatioMinimumBytes
				&& entry.Size / entry.CompressedSize > MaximumCompressionRatio)
				throw new InvalidDataException("The native archive exceeds safe expansion limits.");
			expandedBytes = checked(expandedBytes + entry.Size);
			if (expandedBytes > MaximumExpandedBytes)
				throw new InvalidDataException("The native archive expands beyond the allowed size.");
		}
	}

	private static IReadOnlyList<ReduxGameDirectoryModLayout> FindMatchingLayouts(ReduxGameDirectoryModDefinition definition,
		IReadOnlyList<IArchiveEntry> archiveEntries)
	{
		var files = archiveEntries.Where(entry => !entry.IsDirectory)
			.Select(entry => NormalizeArchivePath(entry.Key))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		return definition.Layouts.Where(layout =>
		{
			var allowed = layout.ManagedEntries.Keys
				.Concat(layout.PackageEntries)
				.Concat(layout.IgnoredEntries)
				.Select(NormalizeArchivePath)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);
			return files.SetEquals(allowed) && layout.ManagedEntries.Keys.All(path => files.Contains(NormalizeArchivePath(path)));
		}).ToArray();
	}

	private static async Task CopyArchiveEntryAsync(IArchiveEntry entry, string destinationPath,
		CancellationToken cancellationToken)
	{
		EnsureSafeDirectoryTree(Path.GetDirectoryName(destinationPath)!, create: true);
		using var input = entry.OpenEntryStream();
		await using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
			128000, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough);
		var buffer = new byte[128000];
		long written = 0;
		while (true)
		{
			var read = await input.ReadAsync(buffer, cancellationToken);
			if (read == 0) break;
			written = checked(written + read);
			if (written > entry.Size || written > MaximumEntryBytes)
				throw new InvalidDataException("The native archive entry expanded beyond its declared safe size.");
			await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
		}
		if (written != entry.Size) throw new InvalidDataException("The native archive entry was truncated while staging.");
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
		throw new ReduxGameDirectoryRecoveryException("A previous game-directory installation did not finish. Do not launch the game; inspect the recovery journal and immutable backups before continuing.");
	}

	private void DeleteJournal(string path)
	{
		EnsureSafeRegularFile(path);
		File.Delete(path);
	}

	private ReduxNativeLoaderStatus DetectLoader(NativeInstallManifest? manifest)
	{
		const string loader = "bink2w64.dll";
		const string original = "bink2w64_original.dll";
		var owned = manifest == null ? null : FindInstallation(manifest, ReduxGameDirectoryModCatalog.Find(944)!.PackageId);
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
				return new ReduxNativeLoaderStatus(false, false,
					"Native Mod Loader changed since Redux installed it; dependent native plugins are blocked.");
			}
			return new ReduxNativeLoaderStatus(true, true, "Native Mod Loader is verified as installed by Redux.");
		}

		if (!loaderSnapshot.Exists && !originalSnapshot.Exists)
			return new ReduxNativeLoaderStatus(false, false, "Native Mod Loader is missing.");
		if (!loaderSnapshot.Exists || !originalSnapshot.Exists
			|| !IsAmd64PeDll(ResolveTargetPath(loader, createParent: false))
			|| !IsAmd64PeDll(ResolveTargetPath(original, createParent: false)))
		{
			return new ReduxNativeLoaderStatus(false, false, "Native Mod Loader is incomplete or has invalid DLL files.");
		}
		return new ReduxNativeLoaderStatus(true, false,
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

	private void EnsurePluginVersion(ReduxGameDirectoryModDefinition definition)
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

	private void EnsureCommitPrerequisites(ReduxGameDirectoryModDefinition definition, NativeInstallManifest? manifest)
	{
		EnsurePluginVersion(definition);
		EnsurePackageSpecificPrerequisites(definition);
		if (!definition.RequiresLoader) return;
		var loaderStatus = DetectLoader(manifest);
		if (!loaderStatus.IsPresent)
			throw new InvalidOperationException($"{definition.Name} requires Native Mod Loader. {loaderStatus.Description}");
	}

	private void EnsurePackageSpecificPrerequisites(ReduxGameDirectoryModDefinition definition)
	{
		if (definition.NexusModId != 23959) return;
		var nativeMods = Path.Combine(_gameBin, "NativeMods");
		if (!Directory.Exists(nativeMods)) return;
		EnsureSafeDirectoryTree(nativeMods, create: false);
		if (Directory.EnumerateFileSystemEntries(nativeMods, "BG3NativeCameraTweaks*", SearchOption.TopDirectoryOnly).Any())
		{
			throw new InvalidOperationException(
				"True Third-Person Camera requires all legacy BG3NativeCameraTweaks files to be removed first. Redux will not delete those files automatically.");
		}
	}

	private static string ValidateArchivePath(string archivePath)
	{
		if (String.IsNullOrWhiteSpace(archivePath)) throw new InvalidDataException("A native archive path is required.");
		var path = Path.GetFullPath(archivePath);
		if (!SupportedArchiveExtensions.Contains(Path.GetExtension(path))
			|| Path.GetFileName(path).Contains(':'))
			throw new InvalidDataException("Choose a reviewed ZIP, 7z, or RAR native archive.");
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
		EnsureSafeDirectoryContents(path);
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

	private static void ValidateArchiveEntryPath(IArchiveEntry entry)
	{
		var path = NormalizeArchivePath(entry.Key);
		if (String.IsNullOrWhiteSpace(path) || path.StartsWith("/", StringComparison.Ordinal)
			|| Path.IsPathRooted(path) || path.Contains(':') || !String.IsNullOrEmpty(entry.LinkTarget)
			|| (entry.Attrib is int attributes && (attributes & (int)FileAttributes.ReparsePoint) != 0))
			throw new InvalidDataException("The native archive contains an unsafe entry path or link.");
		var parts = path.TrimEnd('/').Split('/');
		if (parts.Length == 0 || parts.Any(part => String.IsNullOrWhiteSpace(part) || part is "." or ".."))
			throw new InvalidDataException("The native archive contains a traversal entry path.");
	}

	private static string NormalizeArchivePath(string path) => (path ?? String.Empty).Replace('\\', '/');

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

	private static NativeOwnedInstallation? FindInstallation(NativeInstallManifest manifest, string packageId) =>
		manifest.Installations.SingleOrDefault(installation => PackageIdsEqual(installation.PackageId, packageId));

	private static bool PackageIdsEqual(string left, string right) =>
		String.Equals(left, right, StringComparison.OrdinalIgnoreCase);

	private static NativeOwnedFile? FindOwnedFile(NativeOwnedInstallation installation, string relativePath) =>
		installation.Files.SingleOrDefault(file => String.Equals(file.RelativePath, relativePath, StringComparison.Ordinal));

	private void ValidateManifest(NativeInstallManifest manifest)
	{
		if (manifest.Version != ManifestVersion || !String.Equals(manifest.GameBinIdentity, _gameBinIdentity, StringComparison.Ordinal)
			|| manifest.Installations == null || manifest.Installations.Count > ReduxGameDirectoryModCatalog.All.Count)
		{
			throw new InvalidDataException("Redux's native ownership manifest is foreign or invalid.");
		}
		var packageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var installation in manifest.Installations)
		{
			if (installation == null) throw new InvalidDataException("Redux's native ownership manifest has a null installation entry.");
			var definition = ReduxGameDirectoryModCatalog.Find(installation.NexusModId);
			if (definition == null || !PackageIdsEqual(definition.PackageId, installation.PackageId)
				|| !packageIds.Add(installation.PackageId) || String.IsNullOrWhiteSpace(installation.Name)
				|| !Uri.TryCreate(installation.SourceUrl, UriKind.Absolute, out var sourceUri)
				|| sourceUri.Scheme != Uri.UriSchemeHttps || !IsHash(installation.ArchiveHash)
				|| String.IsNullOrWhiteSpace(installation.ArchiveName) || installation.ArchiveName.Length > 260
				|| !String.Equals(installation.ArchiveName, Path.GetFileName(installation.ArchiveName), StringComparison.Ordinal)
				|| installation.InstalledAtUtc == default || installation.Files == null || installation.Files.Count == 0)
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
			if (definition.Kind == ReduxGameDirectoryModKind.NativeLoader && paths.Count != allowed.Count)
				throw new InvalidDataException("Redux's Native Mod Loader ownership record is incomplete.");
		}
	}

	private void ValidateJournal(NativeJournal journal)
	{
		if (journal.Version != ManifestVersion || !String.Equals(journal.GameBinIdentity, _gameBinIdentity, StringComparison.Ordinal)
			|| journal.Operation is not ("install" or "restore") || ReduxGameDirectoryModCatalog.Find(journal.ProjectId) == null
			|| !IsIdentifier(journal.TransactionId) || journal.Files == null || journal.Files.Count == 0)
		{
			throw new InvalidDataException("A native installer journal is foreign or invalid.");
		}
		var definition = ReduxGameDirectoryModCatalog.Find(journal.ProjectId)!;
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
		PackageId = installation.PackageId,
		NexusModId = installation.NexusModId,
		Name = installation.Name,
		SourceUrl = installation.SourceUrl,
		ArchiveName = installation.ArchiveName,
		ArchiveHash = installation.ArchiveHash,
		InstalledAtUtc = installation.InstalledAtUtc,
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

	private static void EnsureSafeDirectoryContents(string root)
	{
		var pending = new Stack<string>();
		pending.Push(root);
		while (pending.Count > 0)
		{
			var directory = pending.Pop();
			foreach (var entry in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly))
			{
				var attributes = File.GetAttributes(entry);
				if ((attributes & FileAttributes.ReparsePoint) != 0)
					throw new InvalidDataException("Redux-managed directories cannot contain symlinks or junctions.");
				if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
				else EnsureSafeRegularFile(entry);
			}
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
		public ReduxGameDirectoryModDefinition Definition { get; }
		public string ManifestFingerprint { get; }
		public string ArchivePath { get; }
		public FileFingerprint ArchiveFingerprint { get; }
		public string StageDirectory { get; }
		public string TransactionId { get; }
		public IReadOnlyList<DestinationSnapshot> Guards { get; }
		public IReadOnlyList<PlannedWrite> Writes { get; }
		public NativeOwnedInstallation InstallationAfter { get; }
		public string ReviewText { get; }

		public NativeInstallPlan(ReduxGameDirectoryModDefinition definition, string manifestFingerprint, string archivePath,
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
		public string PackageId { get; set; } = String.Empty;
		public long NexusModId { get; set; }
		public string Name { get; set; } = String.Empty;
		public string SourceUrl { get; set; } = String.Empty;
		public string ArchiveName { get; set; } = String.Empty;
		public string ArchiveHash { get; set; } = String.Empty;
		public DateTimeOffset InstalledAtUtc { get; set; }
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

public sealed class ReduxGameDirectoryInstallTransaction : IAsyncDisposable
{
	private ReduxGameDirectoryInstallService? _installer;
	private ReduxGameDirectoryInstallService.NativeInstallPlan? _plan;
	private bool _committed;

	internal ReduxGameDirectoryInstallTransaction(ReduxGameDirectoryInstallService installer, ReduxGameDirectoryInstallService.NativeInstallPlan plan, string reviewText)
	{
		_installer = installer;
		_plan = plan;
		ReviewText = reviewText;
	}

	public string ReviewText { get; }

	public async Task CommitAsync(CancellationToken cancellationToken = default)
	{
		if (_committed) throw new InvalidOperationException("This native installation transaction was already committed.");
		var installer = _installer ?? throw new ObjectDisposedException(nameof(ReduxGameDirectoryInstallTransaction));
		var plan = _plan ?? throw new ObjectDisposedException(nameof(ReduxGameDirectoryInstallTransaction));
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
