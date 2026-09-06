using DivinityModManager.AppServices;

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace Redux.Core.Tests;

public sealed class NativeModInstallerTests
{
	public void AtomicReplacementRejectsSourceChangedSinceItsReviewedHash()
	{
		using var fixture = new NativeFixture();
		var original = fixture.Pe("original destination");
		fixture.WriteVanillaLoader(original);
		var reviewed = fixture.Pe("reviewed source");
		var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(reviewed)).ToLowerInvariant();
		File.WriteAllBytes(fixture.OtherArchivePath, fixture.Pe("changed after review"));
		var installer = fixture.Installer();
		var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
		var snapshot = typeof(NativeModInstaller).GetMethod("CaptureDestination", flags)!.Invoke(installer, ["bink2w64.dll"]);
		var copy = typeof(NativeModInstaller).GetMethod("CopyToTargetAtomicallyAsync", flags)!;
		RegressionAssert.Throws<InvalidDataException>(() => ((System.Threading.Tasks.Task)copy.Invoke(installer,
			[fixture.OtherArchivePath, fixture.LoaderPath, expectedHash, snapshot, CancellationToken.None])!).GetAwaiter().GetResult());
		RegressionAssert.SequenceEqual(original, File.ReadAllBytes(fixture.LoaderPath));
		File.WriteAllBytes(fixture.OtherArchivePath, reviewed);
		((System.Threading.Tasks.Task)copy.Invoke(installer,
			[fixture.OtherArchivePath, fixture.LoaderPath, expectedHash, snapshot, CancellationToken.None])!).GetAwaiter().GetResult();
		RegressionAssert.SequenceEqual(reviewed, File.ReadAllBytes(fixture.LoaderPath));
	}

	public void CatalogContainsOnlyTheApprovedNativeProjects()
	{
		RegressionAssert.Equal(3, NativeModCatalog.All.Count);
		RegressionAssert.Equal("Native Mod Loader", NativeModCatalog.Find(944)!.Name);
		RegressionAssert.Equal("WASD Character Movement", NativeModCatalog.Find(781)!.Name);
		RegressionAssert.Equal("Native Camera Tweaks", NativeModCatalog.Find(945)!.Name);
		RegressionAssert.Equal(null, NativeModCatalog.Find(1));
		RegressionAssert.SequenceEqual(
			new[] { "bin/bink2w64.dll", "bin/bink2w64_original.dll" },
			NativeModCatalog.Find(944)!.RelativeFiles);
		RegressionAssert.SequenceEqual(
			new[] { "bin/NativeMods/BG3WASD.dll", "bin/NativeMods/BG3WASD.toml" },
			NativeModCatalog.Find(781)!.RelativeFiles);
		RegressionAssert.SequenceEqual(
			new[] { "bin/NativeMods/BG3NativeCameraTweaks.dll", "bin/NativeMods/BG3NativeCameraTweaks.toml" },
			NativeModCatalog.Find(945)!.RelativeFiles);
	}

	public void ZipValidationRejectsOtherFormatsTraversalAndUnexpectedFilesWithoutChangingGameFiles()
	{
		using var fixture = new NativeFixture();
		var vanilla = fixture.Pe("vanilla");
		fixture.WriteVanillaLoader(vanilla);
		File.WriteAllText(fixture.OtherArchivePath, "not a zip");

		RegressionAssert.Throws<InvalidDataException>(() => fixture.Installer().StageAsync(
			944, fixture.OtherArchivePath, CancellationToken.None).GetAwaiter().GetResult());

		fixture.CreateArchive(fixture.LoaderArchivePath,
			("bin/bink2w64.dll", fixture.Pe("loader")),
			("bin/bink2w64_original.dll", vanilla),
			("../outside.dll", fixture.Pe("outside")));
		RegressionAssert.Throws<InvalidDataException>(() => fixture.Installer().StageAsync(
			944, fixture.LoaderArchivePath, CancellationToken.None).GetAwaiter().GetResult());

		fixture.CreateArchive(fixture.LoaderArchivePath,
			("bin/bink2w64.dll", fixture.Pe("loader")),
			("bin/bink2w64_original.dll", vanilla),
			("readme.txt", Encoding.UTF8.GetBytes("unexpected")));
		RegressionAssert.Throws<InvalidDataException>(() => fixture.Installer().StageAsync(
			944, fixture.LoaderArchivePath, CancellationToken.None).GetAwaiter().GetResult());

		RegressionAssert.SequenceEqual(vanilla, File.ReadAllBytes(fixture.LoaderPath));
		RegressionAssert.False(File.Exists(fixture.LoaderOriginalPath));
	}

	public void OversizedZipEntriesAreRejectedBeforeTheyCanBeStaged()
	{
		using var fixture = new NativeFixture();
		var vanilla = fixture.Pe("vanilla");
		fixture.WriteVanillaLoader(vanilla);
		fixture.CreateOversizedLoaderArchive(vanilla);

		RegressionAssert.Throws<InvalidDataException>(() => fixture.Installer().StageAsync(
			944, fixture.LoaderArchivePath, CancellationToken.None).GetAwaiter().GetResult());

		RegressionAssert.SequenceEqual(vanilla, File.ReadAllBytes(fixture.LoaderPath));
	}

	public void NativePluginsRequireAKnownSupportedGameVersionAndLoader()
	{
		using var fixture = new NativeFixture();
		fixture.CreateWasdArchive(fixture.Pe("wasd"), "[input]\nforward = \"W\"\n");

		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer(null).StageAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult());
		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer(new Version(4, 1, 1, 6931812)).StageAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult());
		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer(NativeFixture.SupportedVersion).StageAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult());
	}

	public void InspectArchiveAllowsDeferredLoaderPrerequisiteButStillChecksGameVersion()
	{
		using var fixture = new NativeFixture();
		fixture.CreateWasdArchive(fixture.Pe("wasd"), "[input]\nforward = \"W\"\n");

		fixture.Installer(NativeFixture.SupportedVersion).InspectArchiveAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult();

		RegressionAssert.False(Directory.Exists(fixture.NativeModsDirectory));
		RegressionAssert.Equal(0, fixture.StagingDirectoryCount());
		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer().InspectArchiveAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult());
		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer(NativeFixture.SupportedVersion).StageAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult());
	}

	public void LoaderInstallStagesWithoutChangingGameFilesThenCommitsAsReduxVerified()
	{
		using var fixture = new NativeFixture();
		var vanilla = fixture.Pe("vanilla");
		var loader = fixture.Pe("loader");
		fixture.WriteVanillaLoader(vanilla);
		fixture.CreateLoaderArchive(loader, vanilla);

		var transaction = fixture.Installer().StageAsync(944, fixture.LoaderArchivePath, CancellationToken.None)
			.GetAwaiter().GetResult();
		try
		{
			RegressionAssert.SequenceEqual(vanilla, File.ReadAllBytes(fixture.LoaderPath));
			RegressionAssert.False(File.Exists(fixture.LoaderOriginalPath));
			RegressionAssert.Contains(transaction.ReviewText, "Native Mod Loader");
			RegressionAssert.Contains(transaction.ReviewText, fixture.GameBin);
			transaction.CommitAsync(CancellationToken.None).GetAwaiter().GetResult();
		}
		finally
		{
			transaction.DisposeAsync().AsTask().GetAwaiter().GetResult();
		}

		RegressionAssert.SequenceEqual(loader, File.ReadAllBytes(fixture.LoaderPath));
		RegressionAssert.SequenceEqual(vanilla, File.ReadAllBytes(fixture.LoaderOriginalPath));
		var status = fixture.Installer().DetectLoader();
		RegressionAssert.True(status.IsPresent);
		RegressionAssert.True(status.IsVerified);
		RegressionAssert.Contains(status.Description, "verified");
	}

	public void ExternalLoaderPairIsPresentButUnverifiedAndUnmanagedOriginalConflicts()
	{
		using var fixture = new NativeFixture();
		var vanilla = fixture.Pe("vanilla");
		fixture.WriteVanillaLoader(fixture.Pe("external loader"));
		File.WriteAllBytes(fixture.LoaderOriginalPath, vanilla);

		var status = fixture.Installer().DetectLoader();
		RegressionAssert.True(status.IsPresent);
		RegressionAssert.False(status.IsVerified);
		RegressionAssert.Contains(status.Description, "unverified");

		fixture.CreateLoaderArchive(fixture.Pe("approved loader"), vanilla);
		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer().StageAsync(
			944, fixture.LoaderArchivePath, CancellationToken.None).GetAwaiter().GetResult());
	}

	public void ChangedReduxOwnedLoaderBlocksDependentPluginInstallation()
	{
		using var fixture = new NativeFixture();
		fixture.InstallLoader();
		File.WriteAllBytes(fixture.LoaderPath, fixture.Pe("user replacement"));
		fixture.CreateWasdArchive(fixture.Pe("wasd"), "[input]\nforward = \"W\"\n");

		var status = fixture.Installer(NativeFixture.SupportedVersion).DetectLoader();
		RegressionAssert.False(status.IsPresent);
		RegressionAssert.False(status.IsVerified);
		RegressionAssert.Contains(status.Description, "changed");
		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer(NativeFixture.SupportedVersion).StageAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult());
	}

	public void CommitRejectsArchiveAndDestinationChangesAfterReview()
	{
		using var fixture = new NativeFixture();
		var vanilla = fixture.Pe("vanilla");
		fixture.WriteVanillaLoader(vanilla);
		fixture.CreateLoaderArchive(fixture.Pe("loader one"), vanilla);
		var archiveChanged = fixture.Installer().StageAsync(944, fixture.LoaderArchivePath, CancellationToken.None)
			.GetAwaiter().GetResult();
		try
		{
			fixture.CreateLoaderArchive(fixture.Pe("loader two"), vanilla);
			RegressionAssert.Throws<InvalidOperationException>(() => archiveChanged.CommitAsync(CancellationToken.None)
				.GetAwaiter().GetResult());
		}
		finally
		{
			archiveChanged.DisposeAsync().AsTask().GetAwaiter().GetResult();
		}
		RegressionAssert.SequenceEqual(vanilla, File.ReadAllBytes(fixture.LoaderPath));

		fixture.CreateLoaderArchive(fixture.Pe("loader one"), vanilla);
		var destinationChanged = fixture.Installer().StageAsync(944, fixture.LoaderArchivePath, CancellationToken.None)
			.GetAwaiter().GetResult();
		try
		{
			var replacement = fixture.Pe("replacement");
			File.WriteAllBytes(fixture.LoaderPath, replacement);
			RegressionAssert.Throws<InvalidOperationException>(() => destinationChanged.CommitAsync(CancellationToken.None)
				.GetAwaiter().GetResult());
			RegressionAssert.SequenceEqual(replacement, File.ReadAllBytes(fixture.LoaderPath));
		}
		finally
		{
			destinationChanged.DisposeAsync().AsTask().GetAwaiter().GetResult();
		}
	}

	public void PluginCommitRejectsRemovedLoaderAfterReview()
	{
		using var fixture = new NativeFixture();
		fixture.InstallLoader();
		fixture.CreateWasdArchive(fixture.Pe("wasd"), "[input]\nforward = \"W\"\n");
		var transaction = fixture.Installer(NativeFixture.SupportedVersion).StageAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult();
		try
		{
			File.Delete(fixture.LoaderPath);
			RegressionAssert.Throws<InvalidOperationException>(() => transaction.CommitAsync(CancellationToken.None)
				.GetAwaiter().GetResult());
		}
		finally
		{
			transaction.DisposeAsync().AsTask().GetAwaiter().GetResult();
		}

		RegressionAssert.False(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.dll")));
	}

	public void PluginCommitRejectsChangedLoaderAfterReview()
	{
		using var fixture = new NativeFixture();
		fixture.InstallLoader();
		fixture.CreateWasdArchive(fixture.Pe("wasd"), "[input]\nforward = \"W\"\n");
		var transaction = fixture.Installer(NativeFixture.SupportedVersion).StageAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult();
		try
		{
			File.WriteAllBytes(fixture.LoaderPath, fixture.Pe("replacement"));
			RegressionAssert.Throws<InvalidOperationException>(() => transaction.CommitAsync(CancellationToken.None)
				.GetAwaiter().GetResult());
		}
		finally
		{
			transaction.DisposeAsync().AsTask().GetAwaiter().GetResult();
		}

		RegressionAssert.False(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.dll")));
	}

	public void CommitRejectsStaleOwnershipManifestFromAnotherTransaction()
	{
		using var fixture = new NativeFixture();
		fixture.InstallLoader();
		fixture.CreateWasdArchive(fixture.Pe("wasd"), "[input]\nforward = \"W\"\n");
		fixture.CreateCameraArchive(fixture.Pe("camera"), "[camera]\nfov = 75\n");
		var wasd = fixture.Installer(NativeFixture.SupportedVersion).StageAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult();
		var camera = fixture.Installer(NativeFixture.SupportedVersion).StageAsync(
			945, fixture.CameraArchivePath, CancellationToken.None).GetAwaiter().GetResult();
		try
		{
			camera.CommitAsync(CancellationToken.None).GetAwaiter().GetResult();
			RegressionAssert.Throws<InvalidOperationException>(() => wasd.CommitAsync(CancellationToken.None)
				.GetAwaiter().GetResult());
		}
		finally
		{
			camera.DisposeAsync().AsTask().GetAwaiter().GetResult();
			wasd.DisposeAsync().AsTask().GetAwaiter().GetResult();
		}

		RegressionAssert.True(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3NativeCameraTweaks.dll")));
		RegressionAssert.False(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.dll")));
	}

	public void CommitFailureRollsBackEveryWrittenTarget()
	{
		using var fixture = new NativeFixture();
		fixture.InstallLoader();
		var loader = File.ReadAllBytes(fixture.LoaderPath);
		fixture.CreateWasdArchive(fixture.Pe("wasd"), "[input]\nforward = \"W\"\n");
		var transaction = fixture.Installer(NativeFixture.SupportedVersion).StageAsync(781, fixture.WasdArchivePath, CancellationToken.None)
			.GetAwaiter().GetResult();
		try
		{
			// Reads and the review fingerprint still succeed, but publishing ownership
			// fails after both plugin files have been copied.
			using var manifestLock = File.Open(fixture.ManifestPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			RegressionAssert.Throws<IOException>(() => transaction.CommitAsync(CancellationToken.None)
				.GetAwaiter().GetResult());
		}
		finally
		{
			transaction.DisposeAsync().AsTask().GetAwaiter().GetResult();
		}

		RegressionAssert.SequenceEqual(loader, File.ReadAllBytes(fixture.LoaderPath));
		RegressionAssert.False(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.dll")));
		RegressionAssert.False(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.toml")));
		RegressionAssert.True(fixture.Installer().DetectLoader().IsVerified);
	}

	public void PluginInstallPreservesExistingTomlConfiguration()
	{
		using var fixture = new NativeFixture();
		fixture.InstallLoader();
		fixture.CreateWasdArchive(fixture.Pe("wasd"), "[input]\nforward = \"W\"\n");
		Directory.CreateDirectory(fixture.NativeModsDirectory);
		const string userConfiguration = "[input]\nforward = \"Up\"\n";
		File.WriteAllText(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.toml"), userConfiguration);

		fixture.Install(781, fixture.WasdArchivePath, NativeFixture.SupportedVersion);

		RegressionAssert.True(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.dll")));
		RegressionAssert.Equal(userConfiguration, File.ReadAllText(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.toml")));
	}

	public void RestoreRefusesActivePluginsThenRestoresOnlyOwnedFiles()
	{
		using var fixture = new NativeFixture();
		var vanilla = fixture.InstallLoader();
		fixture.CreateWasdArchive(fixture.Pe("wasd"), "[input]\nforward = \"W\"\n");
		fixture.Install(781, fixture.WasdArchivePath, NativeFixture.SupportedVersion);
		Directory.CreateDirectory(fixture.NativeModsDirectory);
		var unrelated = Path.Combine(fixture.NativeModsDirectory, "Unrelated.dll");
		File.WriteAllBytes(unrelated, fixture.Pe("unrelated"));

		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer().RestoreAsync(944, CancellationToken.None)
			.GetAwaiter().GetResult());
		fixture.Installer(NativeFixture.SupportedVersion).RestoreAsync(781, CancellationToken.None).GetAwaiter().GetResult();

		RegressionAssert.False(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.dll")));
		RegressionAssert.False(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.toml")));
		RegressionAssert.True(File.Exists(unrelated));
		RegressionAssert.True(Directory.Exists(fixture.NativeModsDirectory));

		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer().RestoreAsync(944, CancellationToken.None).GetAwaiter().GetResult());
		RegressionAssert.True(File.Exists(unrelated));
		File.Delete(unrelated);
		fixture.Installer().RestoreAsync(944, CancellationToken.None).GetAwaiter().GetResult();
		RegressionAssert.SequenceEqual(vanilla, File.ReadAllBytes(fixture.LoaderPath));
		RegressionAssert.False(File.Exists(fixture.LoaderOriginalPath));
	}

	private sealed class NativeFixture : IDisposable
	{
		public static readonly Version SupportedVersion = new(4, 1, 1, 6931813);
		private readonly string _root = Path.Combine(Path.GetTempPath(), "ReduxNativeInstallerTests", Guid.NewGuid().ToString("N"));
		public string GameBin { get; }
		public string StateDirectory { get; }
		public string LoaderArchivePath { get; }
		public string WasdArchivePath { get; }
		public string CameraArchivePath { get; }
		public string OtherArchivePath { get; }
		public string ManifestPath => Path.Combine(Directory.GetDirectories(Path.Combine(StateDirectory, "native-mods")).Single(), "manifest.json");
		public string LoaderPath => Path.Combine(GameBin, "bink2w64.dll");
		public string LoaderOriginalPath => Path.Combine(GameBin, "bink2w64_original.dll");
		public string NativeModsDirectory => Path.Combine(GameBin, "NativeMods");

		public NativeFixture()
		{
			GameBin = Path.Combine(_root, "Game", "bin");
			StateDirectory = Path.Combine(_root, "State");
			LoaderArchivePath = Path.Combine(_root, "NativeModLoader.zip");
			WasdArchivePath = Path.Combine(_root, "WASD.zip");
			CameraArchivePath = Path.Combine(_root, "Camera.zip");
			OtherArchivePath = Path.Combine(_root, "Other.7z");
			Directory.CreateDirectory(GameBin);
			File.WriteAllBytes(Path.Combine(GameBin, "bg3.exe"), new byte[] { 1 });
		}

		public NativeModInstaller Installer(Version? version = null) =>
			new(GameBin, StateDirectory, version!);

		public byte[] InstallLoader()
		{
			var vanilla = Pe("vanilla");
			WriteVanillaLoader(vanilla);
			CreateLoaderArchive(Pe("redux loader"), vanilla);
			Install(944, LoaderArchivePath, null);
			return vanilla;
		}

		public void Install(long projectId, string archivePath, Version? version)
		{
			var transaction = Installer(version).StageAsync(projectId, archivePath, CancellationToken.None)
				.GetAwaiter().GetResult();
			try { transaction.CommitAsync(CancellationToken.None).GetAwaiter().GetResult(); }
			finally { transaction.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
		}

		public void WriteVanillaLoader(byte[] vanilla) => File.WriteAllBytes(LoaderPath, vanilla);

		public void CreateLoaderArchive(byte[] loader, byte[] original) => CreateArchive(LoaderArchivePath,
			("bin/bink2w64.dll", loader),
			("bin/bink2w64_original.dll", original));

		public void CreateWasdArchive(byte[] dll, string toml) => CreateArchive(WasdArchivePath,
			("bin/NativeMods/BG3WASD.dll", dll),
			("bin/NativeMods/BG3WASD.toml", Encoding.UTF8.GetBytes(toml)));

		public void CreateCameraArchive(byte[] dll, string toml) => CreateArchive(CameraArchivePath,
			("bin/NativeMods/BG3NativeCameraTweaks.dll", dll),
			("bin/NativeMods/BG3NativeCameraTweaks.toml", Encoding.UTF8.GetBytes(toml)));

		public int StagingDirectoryCount() => Directory.GetDirectories(Path.Combine(
			Directory.GetDirectories(Path.Combine(StateDirectory, "native-mods")).Single(), "staging")).Length;

		public void CreateArchive(string path, params (string Name, byte[] Bytes)[] files)
		{
			if (File.Exists(path)) File.Delete(path);
			using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
			foreach (var file in files)
			{
				var entry = archive.CreateEntry(file.Name, CompressionLevel.Optimal);
				using var stream = entry.Open();
				stream.Write(file.Bytes, 0, file.Bytes.Length);
			}
		}

		public void CreateOversizedLoaderArchive(byte[] original)
		{
			if (File.Exists(LoaderArchivePath)) File.Delete(LoaderArchivePath);
			using var archive = ZipFile.Open(LoaderArchivePath, ZipArchiveMode.Create);
			var loader = archive.CreateEntry("bin/bink2w64.dll", CompressionLevel.Optimal);
			using (var stream = loader.Open())
			{
				var zeros = new byte[64 * 1024];
				for (var index = 0; index < 528; index++) stream.Write(zeros, 0, zeros.Length);
			}
			var originalEntry = archive.CreateEntry("bin/bink2w64_original.dll", CompressionLevel.Optimal);
			using var originalStream = originalEntry.Open();
			originalStream.Write(original, 0, original.Length);
		}

		public byte[] Pe(string marker)
		{
			var bytes = new byte[1024];
			bytes[0] = (byte)'M';
			bytes[1] = (byte)'Z';
			bytes[0x3c] = 0x80;
			bytes[0x80] = (byte)'P';
			bytes[0x81] = (byte)'E';
			bytes[0x84] = 0x64;
			bytes[0x85] = 0x86;
			bytes[0x94] = 0xf0;
			bytes[0x96] = 0x00;
			bytes[0x97] = 0x20;
			bytes[0x98] = 0x0b;
			bytes[0x99] = 0x02;
			Encoding.UTF8.GetBytes(marker).CopyTo(bytes, 0x200);
			return bytes;
		}

		public void Dispose()
		{
			if (Directory.Exists(_root)) Directory.Delete(_root, true);
		}
	}
}
