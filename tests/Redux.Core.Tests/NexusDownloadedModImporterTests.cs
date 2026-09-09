using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.Modio;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.ModUpdater;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using DynamicData;
using DynamicData.Binding;

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Redux.Core.Tests;

internal sealed class NexusDownloadedModImporterTests
{
	public void ValidationFailureLeavesInstalledFilesUntouched()
	{
		using var fixture = new ImportFixture();
		fixture.WriteInstalled("One.pak", "old-one");
		fixture.CreateArchive(("One.pak", "new-one"), ("Two.pak", "invalid"));
		var importer = fixture.CreateImporter((path, _) => File.ReadAllText(path) == "invalid"
			? Task.FromResult<DivinityModData>(null!)
			: Task.FromResult<DivinityModData>(new RegressionModData { Name = Path.GetFileName(path) }));

		RegressionAssert.Throws<NexusDownloadedModValidationException>(() =>
			importer.StageAsync(fixture.ArchivePath, CancellationToken.None).GetAwaiter().GetResult());

		RegressionAssert.Equal("old-one", fixture.ReadInstalled("One.pak"));
		RegressionAssert.False(File.Exists(Path.Combine(fixture.ModsDirectory, "Two.pak")));
		RegressionAssert.Equal(0, fixture.StagingDirectoryCount());
	}

	public void DuplicateFlattenedPakNamesAreRejectedBeforeCommit()
	{
		using var fixture = new ImportFixture();
		fixture.CreateArchive(("Main/Same.pak", "one"), ("Optional/Same.pak", "two"));
		var importer = fixture.CreateImporter((path, _) =>
			Task.FromResult<DivinityModData>(new RegressionModData { Name = Path.GetFileName(path) }));

		RegressionAssert.Throws<NexusDownloadedModValidationException>(() =>
			importer.StageAsync(fixture.ArchivePath, CancellationToken.None).GetAwaiter().GetResult());

		RegressionAssert.Equal(0, fixture.StagingDirectoryCount());
	}

	public void CommitFailureRestoresEveryDestination()
	{
		using var fixture = new ImportFixture();
		fixture.WriteInstalled("One.pak", "old-one");
		fixture.WriteInstalled("Two.pak", "old-two");
		fixture.CreateArchive(("One.pak", "new-one"), ("Two.pak", "new-two"));
		var importer = fixture.CreateImporter((path, _) =>
			Task.FromResult<DivinityModData>(new RegressionModData { Name = Path.GetFileName(path) }));
		var transaction = importer.StageAsync(fixture.ArchivePath, CancellationToken.None).GetAwaiter().GetResult();
		try
		{
			var callbacks = 0;
			RegressionAssert.Throws<IOException>(() => transaction.CommitAsync(_ =>
			{
				if (++callbacks == 2) throw new IOException("simulated registration failure");
				return Task.FromResult<DivinityModData>(new RegressionModData());
			}, CancellationToken.None).GetAwaiter().GetResult());
		}
		finally { transaction.DisposeAsync().AsTask().GetAwaiter().GetResult(); }

		RegressionAssert.Equal("old-one", fixture.ReadInstalled("One.pak"));
		RegressionAssert.Equal("old-two", fixture.ReadInstalled("Two.pak"));
		RegressionAssert.Equal(0, fixture.StagingDirectoryCount());
	}

	public void StagingPreflightsSiblingPackagesBeforeExternalDependencies()
	{
		using var fixture = new ImportFixture();
		fixture.CreateArchive(("Consumer.pak", "consumer"), ("Library.pak", "library"));
		var library = Mod("Library");
		var consumer = Mod("Consumer", library);
		var loaded = 0;
		var importer = new NexusDownloadedModImporter(
			fixture.ModsDirectory,
			fixture.RecoveryDirectory,
			(path, _) =>
			{
				loaded++;
				return Task.FromResult<DivinityModData>(Path.GetFileName(path) == "Consumer.pak" ? consumer : library);
			},
			(path, otherPackages, _) =>
			{
				RegressionAssert.Equal(2, loaded);
				RegressionAssert.Equal(1, otherPackages.Count);
				var mod = Path.GetFileName(path) == "Consumer.pak" ? consumer : library;
				NexusDownloadedModValidationException.ThrowIfBlocked(
					PackagePreflightService.AnalyzeLoadedPackage(path, mod, otherPackages));
				return Task.CompletedTask;
			});

		var transaction = importer.StageAsync(fixture.ArchivePath, CancellationToken.None).GetAwaiter().GetResult();
		try { RegressionAssert.Equal(2, transaction.Packages.Count); }
		finally { transaction.DisposeAsync().AsTask().GetAwaiter().GetResult(); }

		RegressionAssert.Equal(0, fixture.StagingDirectoryCount());
	}

	public void RegisteredModelFailureRestoresLibraryPlacementOrdersAndSourceCaches()
	{
		using var fixture = new ImportFixture();
		fixture.WriteInstalled("First.pak", "old-first");
		fixture.CreateArchive(("First.pak", "new-first"), ("Second.pak", "new-second"));
		var existing = Mod("Existing");
		var first = Mod("Replacement");
		first.UUID = existing.UUID;
		var second = Mod("Second");
		var importer = new NexusDownloadedModImporter(
			fixture.ModsDirectory,
			fixture.RecoveryDirectory,
			(path, _) => Task.FromResult<DivinityModData>(Path.GetFileName(path) switch
			{
				"First.pak" => first,
				"Second.pak" => second,
				_ => null!
			}),
			(_, _) => Task.CompletedTask);
		using var models = new SourceCache<DivinityModData, string>(mod => mod.UUID);
		models.AddOrUpdate(existing);
		var active = new ObservableCollectionExtended<DivinityModData> { existing };
		var inactive = new ObservableCollectionExtended<DivinityModData>();
		var order = new DivinityLoadOrder { Order = [existing.ToOrderEntry()] };
		var updates = new ModUpdateHandler();
		updates.Modio.CacheData.Mods[existing.UUID] = new ModioModData
		{
			UUID = existing.UUID,
			ModId = 6197684,
			MetadataOrigin = ModioMetadataOrigin.Manual
		};
		var snapshot = NxmPakInstallStateSnapshot.Capture(models, active, inactive, [order], updates,
			[first.UUID, second.UUID]);
		var transaction = importer.StageAsync(fixture.ArchivePath, CancellationToken.None).GetAwaiter().GetResult();
		try
		{
			RegressionAssert.Throws<IOException>(() => transaction.CommitAndRegisterAsync(
				package => Task.FromResult(package.Mod),
				registered =>
				{
					var imported = registered[0];
					updates.Modio.CacheData.Mods.Remove(imported.UUID);
					imported.NexusModsData.SetModVersion(23751, 123);
					imported.NexusModsData.MetadataOrigin = NexusMetadataOrigin.NexusArchiveImport;
					updates.Nexus.CacheData.Mods[imported.UUID] = imported.NexusModsData;
					models.AddOrUpdate(imported);
					active.Remove(existing);
					active.Add(imported);
					order.Update(imported);
					return Task.FromException(new IOException("model registration failed after the first replacement"));
				},
				() =>
				{
					snapshot.Restore(models, active, inactive, [order], updates);
					return Task.CompletedTask;
				}, CancellationToken.None).GetAwaiter().GetResult());
		}
		finally { transaction.DisposeAsync().AsTask().GetAwaiter().GetResult(); }

		RegressionAssert.Equal("old-first", fixture.ReadInstalled("First.pak"));
		RegressionAssert.False(File.Exists(Path.Combine(fixture.ModsDirectory, "Second.pak")));
		RegressionAssert.True(ReferenceEquals(existing, models.Lookup(existing.UUID).Value));
		RegressionAssert.Equal(1, active.Count);
		RegressionAssert.True(ReferenceEquals(existing, active[0]));
		RegressionAssert.Equal("Existing", order.Order[0].Name);
		RegressionAssert.False(updates.Nexus.CacheData.Mods.ContainsKey(existing.UUID));
		RegressionAssert.Equal(ModioMetadataOrigin.Manual, updates.Modio.CacheData.Mods[existing.UUID].MetadataOrigin);
	}

	public void CleanupFailureDoesNotRollbackCommittedFiles()
	{
		using var fixture = new ImportFixture();
		fixture.WriteInstalled("One.pak", "old-one");
		fixture.CreateArchive(("One.pak", "new-one"));
		var importer = fixture.CreateImporter((path, _) =>
			Task.FromResult<DivinityModData>(new RegressionModData { Name = Path.GetFileName(path) }));
		var transaction = importer.StageAsync(fixture.ArchivePath, CancellationToken.None).GetAwaiter().GetResult();
		try
		{
			transaction.CommitAsync(package => Task.FromResult(package.Mod), CancellationToken.None).GetAwaiter().GetResult();
			var stagingRoot = Path.GetDirectoryName(transaction.Packages[0].StagedPath)!;
			var lockedPath = Path.Combine(stagingRoot, "cleanup-lock.tmp");
			using (new FileStream(lockedPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
			{
				RegressionAssert.Throws<IOException>(() => transaction.Complete());
				transaction.DisposeAsync().AsTask().GetAwaiter().GetResult();
			}
		}
		finally { transaction.DisposeAsync().AsTask().GetAwaiter().GetResult(); }

		RegressionAssert.Equal("new-one", fixture.ReadInstalled("One.pak"));
	}

	private static RegressionModData Mod(string name, params DivinityModData[] dependencies)
	{
		var mod = new RegressionModData
		{
			UUID = Guid.NewGuid().ToString(),
			Name = name,
			Author = "Test",
			Folder = name,
			HasMetadata = true,
			Files = [$"Mods/{name}/meta.lsx"]
		};
		foreach (var dependency in dependencies) mod.Dependencies.AddOrUpdate(ModuleShortDesc.FromModData(dependency));
		return mod;
	}

	private sealed class ImportFixture : IDisposable
	{
		private readonly string _root = Path.Combine(Path.GetTempPath(), "ReduxNxmImportTests", Guid.NewGuid().ToString("N"));
		public string ModsDirectory { get; }
		public string RecoveryDirectory { get; }
		public string ArchivePath { get; }

		public ImportFixture()
		{
			ModsDirectory = Path.Combine(_root, "Mods");
			RecoveryDirectory = Path.Combine(_root, "Recovery");
			ArchivePath = Path.Combine(_root, "Download.zip");
			Directory.CreateDirectory(ModsDirectory);
		}

		public NexusDownloadedModImporter CreateImporter(Func<string, CancellationToken, Task<DivinityModData>> validator) =>
			new(ModsDirectory, RecoveryDirectory, validator, (_, _) => Task.CompletedTask);

		public void CreateArchive(params (string Name, string Contents)[] files)
		{
			using var archive = ZipFile.Open(ArchivePath, ZipArchiveMode.Create);
			foreach (var file in files)
			{
				var entry = archive.CreateEntry(file.Name);
				using var writer = new StreamWriter(entry.Open());
				writer.Write(file.Contents);
			}
		}

		public void WriteInstalled(string name, string contents) => File.WriteAllText(Path.Combine(ModsDirectory, name), contents);
		public string ReadInstalled(string name) => File.ReadAllText(Path.Combine(ModsDirectory, name));
		public int StagingDirectoryCount() => Directory.GetDirectories(ModsDirectory, ".redux-nxm-*", SearchOption.TopDirectoryOnly).Length;

		public void Dispose()
		{
			if (Directory.Exists(_root)) Directory.Delete(_root, true);
		}
	}
}
