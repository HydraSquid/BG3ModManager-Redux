using DivinityModManager.AppServices;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Redux.Core.Tests;

public sealed class NexusInstalledIdentityResolverTests
{
	public void RetainedArchivePakWithDifferentNameRecoversExactNexusFile()
	{
		using var fixture = new Fixture();
		var pak = new byte[] { 1, 2, 3, 4, 5 };
		fixture.WriteInstalled(pak);
		fixture.CreateArchive(("Optional/renamed-package.pak", pak));

		var result = fixture.Resolve(fixture.Evidence(44, "2.4"));

		RegressionAssert.Equal(42L, result.ModId);
		RegressionAssert.Equal(44L, result.FileId);
		RegressionAssert.True(result.HasExactFileIdentity);
		RegressionAssert.Equal(Hash(pak), result.PackageSha256);
		RegressionAssert.Equal("2.4", result.DownloadVersion);
	}

	public void RetainedDirectPakWithVerifiedHashRecoversExactNexusFile()
	{
		using var fixture = new Fixture();
		var pak = new byte[] { 5, 4, 3, 2, 1 };
		fixture.WriteInstalled(pak);
		var retainedPath = fixture.CreateDirectPak(pak);
		var evidence = new NexusRetainedDownload(42, 44, retainedPath, HashFile(retainedPath), "2.4");

		var result = fixture.Resolve(evidence);

		RegressionAssert.Equal(44L, result.FileId);
		RegressionAssert.True(result.HasExactFileIdentity);
		RegressionAssert.Equal("2.4", result.DownloadVersion);
	}

	public void ArchiveHashMismatchCannotRecoverExactNexusFile()
	{
		using var fixture = new Fixture();
		var pak = new byte[] { 6, 7, 8, 9 };
		fixture.WriteInstalled(pak);
		fixture.CreateArchive(("Main.pak", pak));

		var result = fixture.Resolve(fixture.Evidence(44, "2.4") with { ArchiveSha256 = new string('0', 64) });

		RegressionAssert.Equal(0L, result.FileId);
		RegressionAssert.False(result.HasExactFileIdentity);
		RegressionAssert.Equal(Hash(pak), result.PackageSha256);
	}

	public void MatchingUuidAndVersionWithDifferentPakBytesCannotRecoverExactNexusFile()
	{
		using var fixture = new Fixture();
		fixture.WriteInstalled([1, 1, 1, 1]);
		fixture.CreateArchive(("Same-uuid-and-version.pak", new byte[] { 2, 2, 2, 2 }));

		var result = fixture.Resolve(fixture.Evidence(44, "1.0"));

		RegressionAssert.Equal(0L, result.FileId);
		RegressionAssert.False(result.HasExactFileIdentity);
	}

	public void MultipleMatchingNexusFileIdsRemainUnresolved()
	{
		using var fixture = new Fixture();
		var pak = new byte[] { 3, 1, 4, 1, 5, 9 };
		fixture.WriteInstalled(pak);
		fixture.CreateArchive(("First.pak", pak));
		var first = fixture.Evidence(44, "2.4");
		var secondPath = fixture.CreateSecondArchive(("Second.pak", pak));
		var second = new NexusRetainedDownload(42, 45, secondPath, HashFile(secondPath), "2.5");

		var result = fixture.Resolve(first, second);

		RegressionAssert.Equal(0L, result.FileId);
		RegressionAssert.False(result.HasExactFileIdentity);
		RegressionAssert.True(result.IdentityDescription.Contains("different", StringComparison.Ordinal));
	}

	public void ChangedInstalledPakBytesReplaceStaleHashAndInvalidateIdentity()
	{
		using var fixture = new Fixture();
		var original = new byte[] { 10, 11, 12 };
		var changed = new byte[] { 13, 14, 15 };
		fixture.WriteInstalled(original);
		fixture.CreateArchive(("Main.pak", original));
		var identified = fixture.Resolve(fixture.Evidence(44, "2.4"));
		RegressionAssert.True(identified.HasExactFileIdentity);
		fixture.WriteInstalled(changed);

		var result = fixture.Resolve(fixture.Evidence(44, "2.4"), identified);

		RegressionAssert.Equal(Hash(changed), result.PackageSha256);
		RegressionAssert.Equal(0L, result.FileId);
		RegressionAssert.False(result.HasExactFileIdentity);
	}

	public void CancellationIsPropagatedBeforeAnyIdentityClaim()
	{
		using var fixture = new Fixture();
		fixture.WriteInstalled([1, 2, 3]);
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();

		RegressionAssert.Throws<OperationCanceledException>(() => fixture.Resolver.ResolveAsync(
			fixture.Installed(), [], cancellation.Token).GetAwaiter().GetResult());
	}

	public void OversizedEntryListCannotAcceptAnEarlyMatchingPak()
	{
		using var fixture = new Fixture();
		fixture.WriteInstalled([1, 2, 3]);
		var entries = new (string Name, byte[] Bytes)[4097];
		for (var i = 0; i < entries.Length; i++) entries[i] = ($"entry-{i}.pak", new byte[] { 1, 2, 3 });
		fixture.CreateArchive(entries);
		var result = fixture.Resolve(fixture.Evidence(44, "2.4"));
		RegressionAssert.False(result.HasExactFileIdentity);
	}

	public void BundledFingerprintFromAnotherProjectCannotRelabelInstalledMod()
	{
		using var fixture = new Fixture(project: 42, bundledProject: 43, bundledFile: 44);
		fixture.WriteInstalled([1, 2, 3]);

		var result = fixture.Resolve();

		RegressionAssert.Equal(42L, result.ModId);
		RegressionAssert.Equal(0L, result.FileId);
		RegressionAssert.False(result.HasExactFileIdentity);
	}

	public void LegacyRecordedIdentityWithoutLocalProofIsPreservedAsUnverified()
	{
		using var fixture = new Fixture();
		fixture.WriteInstalled([1, 2, 3]);
		var recorded = fixture.Installed() with { FileId = 44, HasExactFileIdentity = true, PackageSha256 = String.Empty };

		var result = fixture.Resolver.ResolveAsync(recorded, []).GetAwaiter().GetResult();

		RegressionAssert.Equal(44L, result.FileId);
		RegressionAssert.False(result.HasExactFileIdentity);
		RegressionAssert.True(result.IdentityDescription.Contains("Recorded", StringComparison.Ordinal));
	}

	public void UnverifiedRecordedIdentityClearsStaleDownloadVersion()
	{
		using var fixture = new Fixture();
		fixture.WriteInstalled([1, 2, 3]);
		var recorded = fixture.Installed() with { FileId = 44, HasExactFileIdentity = true, DownloadVersion = "2.4", PackageSha256 = HashFile(fixture.InstalledPath) };

		var result = fixture.Resolver.ResolveAsync(recorded, []).GetAwaiter().GetResult();

		RegressionAssert.Equal(44L, result.FileId);
		RegressionAssert.False(result.HasExactFileIdentity);
		RegressionAssert.Equal(String.Empty, result.DownloadVersion);
	}

	public void ExcessSameProjectEvidenceRemainsUnidentifiedWithoutPartialScan()
	{
		using var fixture = new Fixture();
		var pak = new byte[] { 7, 7, 7 };
		fixture.WriteInstalled(pak);
		fixture.CreateArchive(("Main.pak", pak));
		var evidence = new List<NexusRetainedDownload> { fixture.Evidence(44, "2.4") };
		for (var index = 0; index < 256; index++)
		{
			var unavailablePath = Path.Combine(Path.GetDirectoryName(fixture.ArchivePath)!, $"unavailable-{index}.zip");
			evidence.Add(new NexusRetainedDownload(42, 1000 + index, unavailablePath, new string('a', 64), "2.4"));
		}

		var result = fixture.Resolver.ResolveAsync(fixture.Installed(), evidence).GetAwaiter().GetResult();

		RegressionAssert.Equal(0L, result.FileId);
		RegressionAssert.False(result.HasExactFileIdentity);
	}

	public void InstalledPakReadHandleRemainsHeldDuringBundledResolution()
	{
		using var fixture = new Fixture();
		fixture.WriteInstalled([1, 2, 3]);
		var writeWasBlocked = false;
		var resolver = new NexusInstalledIdentityResolver((path, _) =>
		{
			try
			{
				using var attempt = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
			}
			catch (IOException) { writeWasBlocked = true; }
			return Task.FromResult<ReduxModDatabaseMatch>(null!);
		});

		resolver.ResolveAsync(fixture.Installed(), []).GetAwaiter().GetResult();

		RegressionAssert.True(writeWasBlocked);
	}

	private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
	private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

	private sealed class Fixture : IDisposable
	{
		private readonly string _directory = Path.Combine(Path.GetTempPath(), "redux-nexus-identity-" + Guid.NewGuid().ToString("N"));
		private readonly long _project;
		private readonly long _bundledProject;
		private readonly long _bundledFile;

		public Fixture(long project = 42, long bundledProject = 0, long bundledFile = 0)
		{
			_project = project;
			_bundledProject = bundledProject;
			_bundledFile = bundledFile;
			Directory.CreateDirectory(_directory);
			Resolver = new NexusInstalledIdentityResolver((_, _) => Task.FromResult(BundledMatch()));
		}

		public string InstalledPath => Path.Combine(_directory, "installed.pak");
		public string ArchivePath => Path.Combine(_directory, "download.zip");
		public NexusInstalledIdentityResolver Resolver { get; }

		public void WriteInstalled(byte[] bytes) => File.WriteAllBytes(InstalledPath, bytes);

		public void CreateArchive(params (string Name, byte[] Bytes)[] entries) => CreateArchive(ArchivePath, entries);

		public string CreateSecondArchive(params (string Name, byte[] Bytes)[] entries)
		{
			var path = Path.Combine(_directory, "download-second.zip");
			CreateArchive(path, entries);
			return path;
		}

		public string CreateDirectPak(byte[] bytes)
		{
			var path = Path.Combine(_directory, "download.pak");
			File.WriteAllBytes(path, bytes);
			return path;
		}

		public NexusRetainedDownload Evidence(long fileId, string version) =>
			new(_project, fileId, ArchivePath, HashFile(ArchivePath), version);

		public NexusInstalledFile Installed() => new("fixture-uuid", "Fixture", _project, 0, "1.0", false)
		{
			FilePath = InstalledPath,
			PackageSha256 = "stale-hash",
			DownloadVersion = "1.0",
			IdentityDescription = "Installed download not identified"
		};

		public NexusInstalledFile Resolve(params NexusRetainedDownload[] evidence) =>
			Resolver.ResolveAsync(Installed(), evidence).GetAwaiter().GetResult();

		public NexusInstalledFile Resolve(NexusRetainedDownload evidence, NexusInstalledFile installed) =>
			Resolver.ResolveAsync(installed, [evidence]).GetAwaiter().GetResult();

		public void Dispose()
		{
			if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
		}

		private ReduxModDatabaseMatch BundledMatch() => _bundledProject > 0 && _bundledFile > 0
			? new ReduxModDatabaseMatch(new ReduxProjectRecord { ModId = _bundledProject }, _bundledFile,
				ReduxOfflineMatchKind.ExactPak, null!)
			: null!;

		private static void CreateArchive(string path, params (string Name, byte[] Bytes)[] entries)
		{
			using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
			foreach (var entry in entries)
			using (var output = archive.CreateEntry(entry.Name).Open()) output.Write(entry.Bytes);
		}
	}
}
