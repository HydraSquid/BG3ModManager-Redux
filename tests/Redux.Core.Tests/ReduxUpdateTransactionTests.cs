using ReduxUpdater;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Redux.Core.Tests;

public sealed class ReduxUpdateTransactionTests
{
	public void TransactionReplacesOwnedFilesAndPreservesUserFiles()
	{
		var root = TemporaryDirectory();
		try
		{
			var target = Path.Combine(root, "target");
			var staged = Path.Combine(root, "staged");
			var backup = Path.Combine(root, "backup");
			WriteRelease(target, new Dictionary<string, string>
			{
				["BG3ModManager.exe"] = "old app",
				["Updater/ReduxUpdater.exe"] = "old updater",
				["obsolete.dll"] = "obsolete"
			});
			File.WriteAllText(Path.Combine(target, "Settings.json"), "user settings");
			WriteRelease(staged, new Dictionary<string, string>
			{
				["BG3ModManager.exe"] = "new app",
				["Updater/ReduxUpdater.exe"] = "new updater",
				["new.dll"] = "new file"
			});

			var result = ReduxUpdateTransaction.Apply(Request(target, staged, backup));

			RegressionAssert.True(result.Succeeded);
			RegressionAssert.Equal("new app", File.ReadAllText(Path.Combine(target, "BG3ModManager.exe")));
			RegressionAssert.True(File.Exists(Path.Combine(target, "new.dll")));
			RegressionAssert.False(File.Exists(Path.Combine(target, "obsolete.dll")));
			RegressionAssert.Equal("user settings", File.ReadAllText(Path.Combine(target, "Settings.json")));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	public void FailedReplacementRollsBackFilesChangedEarlierInTheTransaction()
	{
		var root = TemporaryDirectory();
		try
		{
			var target = Path.Combine(root, "target");
			var staged = Path.Combine(root, "staged");
			var backup = Path.Combine(root, "backup");
			var files = new Dictionary<string, string>
			{
				["BG3ModManager.exe"] = "old app",
				["locked.dll"] = "old locked",
				["Updater/ReduxUpdater.exe"] = "old updater"
			};
			WriteRelease(target, files);
			WriteRelease(staged, new Dictionary<string, string>
			{
				["BG3ModManager.exe"] = "new app",
				["locked.dll"] = "new locked",
				["Updater/ReduxUpdater.exe"] = "new updater"
			});

			using var locked = new FileStream(Path.Combine(target, "locked.dll"), FileMode.Open, FileAccess.Read, FileShare.Read);
			RegressionAssert.Throws<Exception>(() => ReduxUpdateTransaction.Apply(Request(target, staged, backup)));

			RegressionAssert.Equal("old app", File.ReadAllText(Path.Combine(target, "BG3ModManager.exe")));
			RegressionAssert.Equal("old updater", File.ReadAllText(Path.Combine(target, "Updater", "ReduxUpdater.exe")));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	public void ReleaseInventoryCannotClaimUserState()
	{
		var root = TemporaryDirectory();
		try
		{
			var target = Path.Combine(root, "target");
			var staged = Path.Combine(root, "staged");
			WriteRelease(target, new Dictionary<string, string>
			{
				["BG3ModManager.exe"] = "old app",
				["Updater/ReduxUpdater.exe"] = "old updater"
			});
			WriteRelease(staged, new Dictionary<string, string>
			{
				["BG3ModManager.exe"] = "new app",
				["Updater/ReduxUpdater.exe"] = "new updater",
				["Data/Downloads/private.zip"] = "user data"
			});

			RegressionAssert.Throws<InvalidDataException>(() =>
				ReduxUpdateTransaction.Apply(Request(target, staged, Path.Combine(root, "backup"))));
			RegressionAssert.Equal("old app", File.ReadAllText(Path.Combine(target, "BG3ModManager.exe")));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static ReduxUpdateRequest Request(string target, string staged, string backup) => new()
	{
		SchemaVersion = ReduxUpdateTransaction.RequestSchemaVersion,
		ParentProcessId = 1,
		TargetDirectory = target,
		StagedDirectory = staged,
		BackupDirectory = backup,
		ResultPath = Path.Combine(Path.GetDirectoryName(target)!, "result.json"),
		DisplayVersion = "0.1.0-alpha.15",
		RelaunchRelativePath = "BG3ModManager.exe"
	};

	private static void WriteRelease(string root, IReadOnlyDictionary<string, string> files)
	{
		Directory.CreateDirectory(root);
		var paths = new List<string> { "Redux-Release-Files.json" };
		foreach (var file in files)
		{
			var path = Path.Combine(root, file.Key.Replace('/', Path.DirectorySeparatorChar));
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(path, file.Value);
			paths.Add(file.Key);
		}
		File.WriteAllText(Path.Combine(root, "Redux-Release-Files.json"), JsonSerializer.Serialize(new
		{
			schemaVersion = 1,
			files = paths
		}));
	}

	private static string TemporaryDirectory()
	{
		var path = Path.Combine(Path.GetTempPath(), "redux-update-transaction-tests-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}
}
