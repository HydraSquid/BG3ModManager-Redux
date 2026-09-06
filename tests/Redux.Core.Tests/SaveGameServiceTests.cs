using DivinityModManager.AppServices;

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Redux.Core.Tests;

public sealed class SaveGameServiceTests
{
	public void RecognizesEveryAdvertisedSaveArchiveFormat()
	{
		foreach (var extension in new[] { ".zip", ".7z", ".7zip", ".rar", ".tar", ".tar.gz", ".tgz", ".gz", ".gzip" })
			RegressionAssert.True(Bg3SaveGameService.IsSupportedSaveArchive($"download{extension}"));

		RegressionAssert.False(Bg3SaveGameService.IsSupportedSaveArchive("download.pak"));
		RegressionAssert.False(Bg3SaveGameService.IsSupportedSaveArchive("download.txt"));
	}

	public void ClassifiesSaveDifficultyFromAuthoritativeRulesetValues()
	{
		RegressionAssert.Equal(Bg3SaveDifficulty.Honour,
			Bg3SaveGameService.ClassifyDifficulty(new[] { "DifficultyHard", "RulesetHonour" }));
		RegressionAssert.Equal(Bg3SaveDifficulty.Custom,
			Bg3SaveGameService.ClassifyDifficulty(new[] { "DifficultyMedium", "RulesetCustom" }));
		RegressionAssert.Equal(Bg3SaveDifficulty.Tactician,
			Bg3SaveGameService.ClassifyDifficulty(new[] { "DifficultyHard", "RulesetLarian" }));
		RegressionAssert.Equal(Bg3SaveDifficulty.Balanced,
			Bg3SaveGameService.ClassifyDifficulty(new[] { "DifficultyMedium", "RulesetLarian" }));
		RegressionAssert.Equal(Bg3SaveDifficulty.Explorer,
			Bg3SaveGameService.ClassifyDifficulty(new[] { "DifficultyEasy", "RulesetLarian" }));
	}

	public void DiscoversSaveMetadataAndMatchingThumbnail()
	{
		WithTemporaryDirectory(root =>
		{
			var folder = Path.Combine(root, "Tav-123__Camp_Night");
			Directory.CreateDirectory(folder);
			File.WriteAllText(Path.Combine(folder, "Camp_Night.lsv"), "save-data");
			File.WriteAllText(Path.Combine(folder, "Camp_Night.WebP"), "image-data");

			var save = Bg3SaveGameService.Discover(root).Single();

			RegressionAssert.Equal("Camp Night", save.DisplayName);
			RegressionAssert.Equal("Tav-123", save.CampaignName);
			RegressionAssert.True(save.ThumbnailPath.EndsWith("Camp_Night.WebP", StringComparison.OrdinalIgnoreCase));
			RegressionAssert.True(save.SizeBytes > 0);
		});
	}

	public void InstallsNestedZipAsOneSaveFolder()
	{
		WithTemporaryDirectory(root =>
		{
			var archivePath = Path.Combine(root, "download.zip");
			var storyFolder = Path.Combine(root, "Story");
			using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
			{
				WriteEntry(archive, "Package/Tav-456__Manual_Save/Manual_Save.lsv", "save-data");
				WriteEntry(archive, "Package/Tav-456__Manual_Save/Manual_Save.WebP", "image-data");
				WriteEntry(archive, "Package/readme.txt", "ignored");
			}

			var imported = Bg3SaveGameService.Import(archivePath, storyFolder, false);

			RegressionAssert.SequenceEqual(new[] { "Tav-456__Manual_Save" }, imported);
			RegressionAssert.True(File.Exists(Path.Combine(storyFolder, imported[0], "Manual_Save.lsv")));
			RegressionAssert.True(File.Exists(Path.Combine(storyFolder, imported[0], "Manual_Save.WebP")));
			RegressionAssert.False(File.Exists(Path.Combine(storyFolder, imported[0], "readme.txt")));
		});
	}

	public void RejectsUnsafeArchivePathsBeforeImport()
	{
		WithTemporaryDirectory(root =>
		{
			var archivePath = Path.Combine(root, "unsafe.zip");
			using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
			{
				WriteEntry(archive, "../Escaped/Save.lsv", "save-data");
			}

			try
			{
				Bg3SaveGameService.Import(archivePath, Path.Combine(root, "Story"), false);
				throw new InvalidOperationException("Expected the unsafe archive to be rejected.");
			}
			catch (InvalidDataException)
			{
				RegressionAssert.False(Directory.Exists(Path.Combine(root, "Escaped")));
			}
		});
	}

	public void ExistingSaveIsPreservedUntilReplacementIsRequested()
	{
		WithTemporaryDirectory(root =>
		{
			var storyFolder = Path.Combine(root, "Story");
			var existing = Path.Combine(storyFolder, "Tav-789__Save");
			Directory.CreateDirectory(existing);
			File.WriteAllText(Path.Combine(existing, "Save.lsv"), "old");
			var source = Path.Combine(root, "Tav-789__Save");
			Directory.CreateDirectory(source);
			File.WriteAllText(Path.Combine(source, "Save.lsv"), "new");

			try
			{
				Bg3SaveGameService.Import(source, storyFolder, false);
				throw new InvalidOperationException("Expected the collision to require confirmation.");
			}
			catch (IOException)
			{
				RegressionAssert.Equal("old", File.ReadAllText(Path.Combine(existing, "Save.lsv")));
			}

			Bg3SaveGameService.Import(source, storyFolder, true);
			RegressionAssert.Equal("new", File.ReadAllText(Path.Combine(existing, "Save.lsv")));
			RegressionAssert.False(Directory.EnumerateDirectories(storyFolder, ".redux-*", SearchOption.TopDirectoryOnly).Any());
		});
	}

	private static void WriteEntry(ZipArchive archive, string name, string contents)
	{
		var entry = archive.CreateEntry(name);
		using var writer = new StreamWriter(entry.Open());
		writer.Write(contents);
	}

	private static void WithTemporaryDirectory(Action<string> action)
	{
		var directory = Path.Combine(Path.GetTempPath(), "ReduxSaveGameTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try { action(directory); }
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}
}
