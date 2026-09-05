using DivinityModManager.Models;
using DivinityModManager.AppServices;
using DivinityModManager.Util;

using System;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;

namespace Redux.Core.Tests;

public sealed class NexusDownloadedModImporterTests
{
	public void ValidationFailureChangesNoInstalledFiles()
	{
		using var fixture = new ImportFixture();
		fixture.WriteInstalled("One.pak", "old-one");
		fixture.CreateArchive(("One.pak", "new-one"), ("Two.pak", "invalid"));
		var importer = fixture.CreateImporter((path, _) =>
		{
			if (File.ReadAllText(path) == "invalid") return Task.FromResult<DivinityModData>(null!);
			return Task.FromResult<DivinityModData>(new RegressionModData { Name = Path.GetFileName(path) });
		});

		RegressionAssert.Throws<NexusDownloadedModValidationException>(() => importer.StageAsync(fixture.ArchivePath, CancellationToken.None).GetAwaiter().GetResult());

		RegressionAssert.Equal("old-one", fixture.ReadInstalled("One.pak"));
		RegressionAssert.False(File.Exists(Path.Combine(fixture.ModsDirectory, "Two.pak")));
		RegressionAssert.Equal(0, fixture.StagingDirectoryCount());
	}

	public void DuplicateFlattenedPakNamesAreRejected()
	{
		using var fixture = new ImportFixture();
		fixture.CreateArchive(("Main/Same.pak", "one"), ("Optional/Same.pak", "two"));
		var importer = fixture.CreateImporter((path, _) =>
			Task.FromResult<DivinityModData>(new RegressionModData { Name = Path.GetFileName(path) }));

		RegressionAssert.Throws<NexusDownloadedModValidationException>(() => importer.StageAsync(fixture.ArchivePath, CancellationToken.None).GetAwaiter().GetResult());

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
		using var transaction = new AsyncDisposableScope(importer.StageAsync(fixture.ArchivePath, CancellationToken.None).GetAwaiter().GetResult());
		var callbacks = 0;

		RegressionAssert.Throws<IOException>(() => transaction.Value.CommitAsync(_ =>
		{
			if (++callbacks == 2) throw new IOException("simulated registration failure");
			return Task.FromResult<DivinityModData>(new RegressionModData());
		}, CancellationToken.None).GetAwaiter().GetResult());

		RegressionAssert.Equal("old-one", fixture.ReadInstalled("One.pak"));
		RegressionAssert.Equal("old-two", fixture.ReadInstalled("Two.pak"));
		RegressionAssert.Equal(0, fixture.StagingDirectoryCount());
	}

	public void SuccessfulCommitReplacesAllFilesAndKeepsRecoveryCopies()
	{
		using var fixture = new ImportFixture();
		fixture.WriteInstalled("One.pak", "old-one");
		fixture.WriteInstalled("Two.pak", "old-two");
		fixture.CreateArchive(("One.pak", "new-one"), ("Two.pak", "new-two"));
		var importer = fixture.CreateImporter((path, _) =>
			Task.FromResult<DivinityModData>(new RegressionModData { Name = Path.GetFileName(path) }));
		using var transaction = new AsyncDisposableScope(importer.StageAsync(fixture.ArchivePath, CancellationToken.None).GetAwaiter().GetResult());

		var models = transaction.Value.CommitAsync(package => Task.FromResult(package.Mod), CancellationToken.None).GetAwaiter().GetResult();
		transaction.Value.Complete();

		RegressionAssert.Equal(2, models.Count);
		RegressionAssert.Equal("new-one", fixture.ReadInstalled("One.pak"));
		RegressionAssert.Equal("new-two", fixture.ReadInstalled("Two.pak"));
		RegressionAssert.Equal("old-one", File.ReadAllText(Path.Combine(fixture.RecoveryDirectory, "One.pak")));
		RegressionAssert.Equal("old-two", File.ReadAllText(Path.Combine(fixture.RecoveryDirectory, "Two.pak")));
		RegressionAssert.Equal(0, fixture.StagingDirectoryCount());
	}

	public void DisposingUnfinishedCommitRollsBackFiles()
	{
		using var fixture = new ImportFixture();
		fixture.WriteInstalled("One.pak", "old-one");
		fixture.CreateArchive(("One.pak", "new-one"));
		var importer = fixture.CreateImporter((path, _) =>
			Task.FromResult<DivinityModData>(new RegressionModData { Name = Path.GetFileName(path) }));
		var transaction = importer.StageAsync(fixture.ArchivePath, CancellationToken.None).GetAwaiter().GetResult();

		transaction.CommitAsync(package => Task.FromResult(package.Mod), CancellationToken.None).GetAwaiter().GetResult();
		transaction.DisposeAsync().AsTask().GetAwaiter().GetResult();

		RegressionAssert.Equal("old-one", fixture.ReadInstalled("One.pak"));
		RegressionAssert.Equal(0, fixture.StagingDirectoryCount());
	}

	public void ForceLoadedIdentityUsesFinalInstalledPath()
	{
		using var fixture = new ImportFixture();
		fixture.CreateArchive(("Override.pak", "override"));
		var importer = fixture.CreateImporter((path, _) => Task.FromResult<DivinityModData>(new RegressionModData
		{
			Name = Path.GetFileNameWithoutExtension(path),
			UUID = path,
			IsForceLoaded = true,
			HasMetadata = false
		}));
		using var transaction = new AsyncDisposableScope(importer.StageAsync(fixture.ArchivePath, CancellationToken.None).GetAwaiter().GetResult());

		var model = transaction.Value.CommitAsync(package => Task.FromResult(package.Mod), CancellationToken.None).GetAwaiter().GetResult()[0];
		transaction.Value.Complete();

		RegressionAssert.Equal("Override", model.Name);
		RegressionAssert.Equal(Path.Combine(fixture.ModsDirectory, "Override.pak"), model.UUID);
	}

	public void PreflightReceivesBoundedStagedPakWithSupportedExtension()
	{
		using var fixture = new ImportFixture();
		fixture.CreateArchive(("One.pak", "one"));
		var inspectedPath = String.Empty;
		var importer = new NexusDownloadedModImporter(fixture.ModsDirectory, fixture.RecoveryDirectory,
			(path, _) => Task.FromResult<DivinityModData>(new RegressionModData { Name = Path.GetFileName(path) }),
			(path, _) =>
			{
				inspectedPath = path;
				return Task.CompletedTask;
			});

		using var transaction = new AsyncDisposableScope(
			importer.StageAsync(fixture.ArchivePath, CancellationToken.None).GetAwaiter().GetResult());

		RegressionAssert.Equal(".pak", Path.GetExtension(inspectedPath).ToLowerInvariant());
		RegressionAssert.False(inspectedPath.Equals(fixture.ArchivePath, StringComparison.OrdinalIgnoreCase));
	}

	public void DuplicateModUuidsAreRejectedBeforeCommit()
	{
		using var fixture = new ImportFixture();
		fixture.CreateArchive(("One.pak", "one"), ("Two.pak", "two"));
		var importer = fixture.CreateImporter((path, _) => Task.FromResult<DivinityModData>(new RegressionModData
		{
			Name = Path.GetFileName(path),
			UUID = "duplicate-uuid"
		}));

		RegressionAssert.Throws<InvalidDataException>(() =>
			importer.StageAsync(fixture.ArchivePath, CancellationToken.None).GetAwaiter().GetResult());
		RegressionAssert.Equal(0, fixture.StagingDirectoryCount());
	}

	public void RollbackFailureIdentifiesAffectedFileAndRecoveryDirectory()
	{
		using var fixture = new ImportFixture();
		fixture.WriteInstalled("One.pak", "old-one");
		fixture.CreateArchive(("One.pak", "new-one"));
		var importer = fixture.CreateImporter((path, _) =>
			Task.FromResult<DivinityModData>(new RegressionModData { Name = Path.GetFileName(path) }));
		using var transaction = new AsyncDisposableScope(importer.StageAsync(fixture.ArchivePath, CancellationToken.None).GetAwaiter().GetResult());
		FileStream blocker = null!;
		NexusDownloadedModRollbackException failure = null!;
		try
		{
			transaction.Value.CommitAsync(package =>
			{
				blocker = new FileStream(package.DestinationPath, FileMode.Open, FileAccess.Read, FileShare.None);
				throw new IOException("simulated registration failure");
			}, CancellationToken.None).GetAwaiter().GetResult();
		}
		catch (NexusDownloadedModRollbackException ex) { failure = ex; }
		finally { blocker?.Dispose(); }

		RegressionAssert.True(failure != null);
		RegressionAssert.Contains(failure!.RecoveryDirectory, fixture.RecoveryDirectory);
		RegressionAssert.Equal(1, failure.AffectedFiles.Count);
		RegressionAssert.Equal("One.pak", failure.AffectedFiles[0]);
	}

	public void UnreadableArchiveHasAnActionableFailureWithoutInstalling()
	{
		using var fixture = new ImportFixture();
		File.WriteAllText(fixture.ArchivePath, "not an archive");
		var importer = fixture.CreateImporter((_, _) => Task.FromResult<DivinityModData>(new RegressionModData()));
		NexusDownloadedModValidationException? failure = null;
		try { importer.StageAsync(fixture.ArchivePath, CancellationToken.None).GetAwaiter().GetResult(); }
		catch (NexusDownloadedModValidationException ex) { failure = ex; }
		RegressionAssert.True(failure != null);
		RegressionAssert.Equal("archive-unreadable", failure!.ErrorCode);
		RegressionAssert.Contains(failure.Message, "Download Again");
		RegressionAssert.Equal(0, fixture.StagingDirectoryCount());
		RegressionAssert.True(File.Exists(fixture.ArchivePath));
	}

	public void BundledDependenciesAreIdentifiedBeforeAnyPackagePreflight()
	{
		foreach (var missingExternal in new[] { false, true })
		{
			using var fixture = new ImportFixture();
			fixture.CreateArchive(("A.pak", "package A"), ("B.pak", "package B"));
			var a = new RegressionModData { UUID = Guid.NewGuid().ToString(), Name = "A", Author = "Author", Folder = "A", HasMetadata = true, Files = ["Mods/A/meta.lsx"] };
			var b = new RegressionModData { UUID = Guid.NewGuid().ToString(), Name = "B", Author = "Author", Folder = "B", HasMetadata = true, Files = ["Mods/B/meta.lsx"] };
			a.Dependencies.AddOrUpdate(ModuleShortDesc.FromModData(b));
			if (missingExternal) a.Dependencies.AddOrUpdate(new ModuleShortDesc { UUID = Guid.NewGuid().ToString(), Name = "External library" });
			var loaded = 0;
			var importer = new NexusDownloadedModImporter(fixture.ModsDirectory, fixture.RecoveryDirectory,
				(path, _) => { loaded++; return Task.FromResult<DivinityModData>(Path.GetFileName(path) == "A.pak" ? a : b); },
				(path, others, _) =>
				{
					RegressionAssert.Equal(2, loaded);
					RegressionAssert.Equal(1, others.Count);
					var mod = Path.GetFileName(path) == "A.pak" ? a : b;
					NexusDownloadedModValidationException.ThrowIfBlocked(PackagePreflightService.AnalyzeLoadedPackage(path, mod, others));
					return Task.CompletedTask;
				});
			NexusDownloadedModValidationException? failure = null;
			try
			{
				var transaction = importer.StageAsync(fixture.ArchivePath, CancellationToken.None).GetAwaiter().GetResult();
				try { RegressionAssert.Equal(2, transaction.Packages.Count); }
				finally { transaction.DisposeAsync().GetAwaiter().GetResult(); }
			}
			catch (NexusDownloadedModValidationException ex) { failure = ex; }
			RegressionAssert.Equal(missingExternal, failure != null);
			if (failure != null)
			{
				RegressionAssert.Equal(2, failure.InspectedMods.Count);
				var rows = ModDependencyAssistanceService.Build(a.Dependencies.Items, [], [], "", false, failure.InspectedMods);
				RegressionAssert.Contains(rows.Single(row => row.Uuid == b.UUID).Status, "Included in this archive");
				RegressionAssert.Contains(rows.Single(row => row.Name == "External library").Status, "Not installed");
			}
			RegressionAssert.Equal(0, fixture.StagingDirectoryCount());
			RegressionAssert.Equal(0, Directory.GetFiles(fixture.ModsDirectory, "*.pak").Length);
		}
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

	private sealed class AsyncDisposableScope : IDisposable
	{
		public NexusDownloadedModImportTransaction Value { get; }
		public AsyncDisposableScope(NexusDownloadedModImportTransaction value) => Value = value;
		public void Dispose() => Value.DisposeAsync().AsTask().GetAwaiter().GetResult();
	}
}
