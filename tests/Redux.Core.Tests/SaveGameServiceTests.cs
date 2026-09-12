using DivinityModManager.AppServices;
using DivinityModManager.Converters;

using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

	public void SaveThumbnailPreviewDoesNotKeepItsFileOrFolderLocked()
	{
		WithTemporaryDirectory(root =>
		{
			var saveFolder = Path.Combine(root, "Tav-123__Camp_Night");
			Directory.CreateDirectory(saveFolder);
			var thumbnail = Path.Combine(saveFolder, "Camp_Night.png");
			var source = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 40, 80, 120, 255 }, 4);
			var encoder = new PngBitmapEncoder();
			encoder.Frames.Add(BitmapFrame.Create(source));
			using (var output = new FileStream(thumbnail, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				encoder.Save(output);

			var converter = new FilePathToBitmapImageConverter();
			var preview = converter.Convert(thumbnail, typeof(BitmapSource), null!, CultureInfo.InvariantCulture);

			RegressionAssert.True(preview is BitmapSource { IsFrozen: true });
			using (new FileStream(thumbnail, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
			FilePathToBitmapImageConverter.EvictTree(saveFolder);
			Directory.Delete(saveFolder, true);
			RegressionAssert.False(Directory.Exists(saveFolder));
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

	public void CorruptSaveMetadataDoesNotAbortDiscoveryOrChangeFiles()
	{
		WithTemporaryDirectory(root =>
		{
			var broken = Path.Combine(root, "Tav__Broken");
			var other = Path.Combine(root, "Tav__Other");
			Directory.CreateDirectory(broken);
			Directory.CreateDirectory(other);
			var brokenPath = Path.Combine(broken, "Broken.lsv");
			var otherPath = Path.Combine(other, "Other.lsv");
			var bytes = new byte[128];
			File.WriteAllBytes(brokenPath, bytes);
			File.WriteAllText(otherPath, "save-data");
			var saves = Bg3SaveGameService.Discover(root);
			RegressionAssert.Equal(2, saves.Count);
			RegressionAssert.Equal(Bg3SaveDifficulty.Unknown, saves.Single(x => x.SaveFilePath == brokenPath).Difficulty);
			RegressionAssert.SequenceEqual(bytes, File.ReadAllBytes(brokenPath));
			RegressionAssert.Equal("save-data", File.ReadAllText(otherPath));
		});
	}

	public void LooseArchiveSavesKeepOnlyTheirMatchingFiles()
	{
		WithTemporaryDirectory(root =>
		{
			foreach (var prefix in new[] { "", "Story/", "Wrapper/Story/" })
			{
				var archivePath = Path.Combine(root, Guid.NewGuid() + ".zip");
				var story = Path.Combine(root, Guid.NewGuid().ToString());
				using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
				{
					WriteEntry(archive, prefix + "First.lsv", "first");
					WriteEntry(archive, prefix + "First.WebP", "thumbnail");
					WriteEntry(archive, prefix + "Second.lsv", "second");
					WriteEntry(archive, prefix + "Unrelated.webp", "unrelated");
				}
				var names = Bg3SaveGameService.Import(archivePath, story, false);
				RegressionAssert.SequenceEqual(new[] { "Imported__First", "Imported__Second" }, names);
				RegressionAssert.Equal(2, Directory.GetFiles(Path.Combine(story, names[0])).Length);
				RegressionAssert.Equal(1, Directory.GetFiles(Path.Combine(story, names[1])).Length);
				RegressionAssert.Equal("first", File.ReadAllText(Path.Combine(story, names[0], "First.lsv")));
				RegressionAssert.Equal("second", File.ReadAllText(Path.Combine(story, names[1], "Second.lsv")));
				RegressionAssert.Throws<IOException>(() => Bg3SaveGameService.Import(archivePath, story, false));
				RegressionAssert.Equal("first", File.ReadAllText(Path.Combine(story, names[0], "First.lsv")));
			}
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
