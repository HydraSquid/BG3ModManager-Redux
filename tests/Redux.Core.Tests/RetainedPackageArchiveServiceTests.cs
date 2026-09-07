using DivinityModManager.AppServices;
using DivinityModManager.Models.NexusMods;

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Redux.Core.Tests;

internal sealed class RetainedPackageArchiveServiceTests
{
	public void PakArchiveCardDoesNotPromiseAForcedInactiveReinstall()
	{
		var entry = new RetainedPackageArchiveEntry
		{
			PackageKind = "PAK mod archive",
			InstallDestination = "Inactive Mods"
		};

		RegressionAssert.Contains(entry.DestinationText, "placement preserved");
		RegressionAssert.False(entry.DestinationText.Contains("to Inactive Mods", StringComparison.OrdinalIgnoreCase));
	}

	public void InitializeDoesNotCreateAnEmptyOptOutLibrary()
	{
		WithFixture((root, service) =>
		{
			service.InitializeAsync().GetAwaiter().GetResult();
			RegressionAssert.False(Directory.Exists(root));
		});
	}

	public void RetentionIsContentAddressedDeduplicatedAndRemovesManagedInboxCopy()
	{
		WithFixture((root, service) =>
		{
			var first = Path.Combine(Path.GetDirectoryName(root)!, "first.zip");
			var second = Path.Combine(Path.GetDirectoryName(root)!, "second.zip");
			File.WriteAllBytes(first, [1, 2, 3, 4]);
			File.WriteAllBytes(second, [1, 2, 3, 4]);
			var hash = Hash(first);
			var item = Item(hash);

			service.RetainAsync(first, item, "Inactive Mods", 1024, true).GetAwaiter().GetResult();
			service.RetainAsync(second, item, "Inactive Mods", 1024, true).GetAwaiter().GetResult();

			RegressionAssert.Equal(1, service.Entries.Count);
			RegressionAssert.False(File.Exists(first));
			RegressionAssert.False(File.Exists(second));
			RegressionAssert.True(File.Exists(service.GetPackagePath(hash)));
			RegressionAssert.Equal(1, Directory.EnumerateFiles(Path.Combine(root, "packages"), "*", SearchOption.AllDirectories).Count());
		});
	}

	public void ArchiveIndexContainsSafeIdentityButNoCapabilitiesOrSignedUrls()
	{
		WithFixture((root, service) =>
		{
			var source = Path.Combine(Path.GetDirectoryName(root)!, "nexus.zip");
			File.WriteAllBytes(source, [5, 6, 7, 8]);
			var item = Item(Hash(source));
			item.SourceKind = AcquiredPackageSourceKind.NexusMods;
			item.ModId = 781;
			item.FileId = 1234;
			item.Authorization = new NexusModManagerLink(781, 1234, "secret-download-key", 99, 88);
			item.ThumbnailUrl = "https://signed.example.test/image.png?token=private";

			service.RetainAsync(source, item, "Game-directory Mods", 1024, false).GetAwaiter().GetResult();
			var json = File.ReadAllText(Path.Combine(root, "archives.json"));

			RegressionAssert.True(json.Contains("781", StringComparison.Ordinal));
			RegressionAssert.True(json.Contains(item.ArchiveSha256, StringComparison.Ordinal));
			RegressionAssert.False(json.Contains("secret-download-key", StringComparison.Ordinal));
			RegressionAssert.False(json.Contains("token=private", StringComparison.Ordinal));
			RegressionAssert.False(json.Contains(source, StringComparison.OrdinalIgnoreCase));
		});
	}

	public void QuotaPrunesLeastRecentlyUsedPackages()
	{
		WithFixture((root, service) =>
		{
			var first = Path.Combine(Path.GetDirectoryName(root)!, "old.zip");
			var second = Path.Combine(Path.GetDirectoryName(root)!, "new.zip");
			File.WriteAllBytes(first, [1, 1, 1, 1, 1, 1]);
			File.WriteAllBytes(second, [2, 2, 2, 2, 2, 2]);
			var firstHash = Hash(first);
			var secondHash = Hash(second);
			service.RetainAsync(first, Item(firstHash), "Inactive Mods", 8, false).GetAwaiter().GetResult();
			service.RetainAsync(second, Item(secondHash), "Inactive Mods", 8, false).GetAwaiter().GetResult();

			RegressionAssert.Equal(1, service.Entries.Count);
			RegressionAssert.Equal(secondHash, service.Entries.Single().Sha256);
			RegressionAssert.True(service.GetPackagePath(firstHash) == null);
			RegressionAssert.True(File.Exists(service.GetPackagePath(secondHash)));
		});
	}

	private static NxmDownloadItem Item(string hash) => new()
	{
		ArchiveSha256 = hash,
		SourceKind = AcquiredPackageSourceKind.LocalFile,
		SourceFileName = "Example.zip",
		FileName = "Example.zip",
		CompletedFileName = "Example.zip",
		ProjectName = "Example",
		Version = "1.2.3",
		DetectedContentKind = "PAK mod archive"
	};

	private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

	private static void WithFixture(Action<string, RetainedPackageArchiveService> test)
	{
		var parent = Path.Combine(Path.GetTempPath(), "ReduxArchiveLibraryTests", Guid.NewGuid().ToString("N"));
		var root = Path.Combine(parent, "Archives");
		Directory.CreateDirectory(parent);
		try { test(root, new RetainedPackageArchiveService(root)); }
		finally { if (Directory.Exists(parent)) Directory.Delete(parent, true); }
	}
}
