using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;

using Newtonsoft.Json;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Redux.Core.Tests;

public sealed class FileSafetyTests
{
	public void FailedStagedWritePreservesTheExistingDestination()
	{
		WithTemporaryDirectory(directory =>
		{
			var destination = Path.Combine(directory, "order.json");
			File.WriteAllText(destination, "saved");
			try
			{
				AtomicFileWriter.WriteFile(destination, temporaryPath =>
				{
					File.WriteAllText(temporaryPath, "partial");
					throw new IOException("simulated failure");
				});
				throw new InvalidOperationException("Expected the staged write to fail.");
			}
			catch (IOException)
			{
				RegressionAssert.Equal("saved", File.ReadAllText(destination));
				RegressionAssert.False(File.Exists(destination + ".tmp"));
			}
		});
	}

	public void AtomicCopyReplacesTheDestinationAndKeepsItsBackup()
	{
		WithTemporaryDirectory(directory =>
		{
			var source = Path.Combine(directory, "source.pak");
			var destination = Path.Combine(directory, "installed.pak");
			var backup = Path.Combine(directory, "installed.old.pak");
			File.WriteAllText(source, "updated mod");
			File.WriteAllText(destination, "previous mod");

			AtomicFileWriter.CopyFile(source, destination, backup);

			RegressionAssert.Equal("updated mod", File.ReadAllText(destination));
			RegressionAssert.Equal("previous mod", File.ReadAllText(backup));
			RegressionAssert.False(File.Exists(destination + ".tmp"));
		});
	}

	public void AsyncCopyReplacesTheDestinationAndKeepsItsBackup()
	{
		WithTemporaryDirectory(directory =>
		{
			var source = Path.Combine(directory, "source.pak");
			var destination = Path.Combine(directory, "installed.pak");
			var backup = Path.Combine(directory, "installed.old.pak");
			File.WriteAllText(source, "updated mod asynchronously");
			File.WriteAllText(destination, "previous mod");

			AtomicFileWriter.CopyFileAsync(source, destination, backup).GetAwaiter().GetResult();

			RegressionAssert.Equal("updated mod asynchronously", File.ReadAllText(destination));
			RegressionAssert.Equal("previous mod", File.ReadAllText(backup));
			RegressionAssert.False(Directory.EnumerateFiles(directory, "*.tmp").Any());
		});
	}

	public void ConcurrentWritesNeverExposePartialContent()
	{
		WithTemporaryDirectory(directory =>
		{
			var destination = Path.Combine(directory, "settings.json");
			var expectedContents = Enumerable.Range(0, 8)
				.Select(index => $"{{\"revision\":{index},\"payload\":\"{new string((char)('a' + index), 2048)}\"}}")
				.ToArray();

			Task.WaitAll(expectedContents.Select(contents => Task.Run(() =>
				AtomicFileWriter.WriteAllText(destination, contents))).ToArray());

			RegressionAssert.True(expectedContents.Contains(File.ReadAllText(destination), StringComparer.Ordinal));
			RegressionAssert.False(Directory.EnumerateFiles(directory, "*.tmp").Any());
		});
	}

	public void CancelledAsyncCopyPreservesTheExistingDestination()
	{
		WithTemporaryDirectory(directory =>
		{
			var source = Path.Combine(directory, "source.pak");
			var destination = Path.Combine(directory, "installed.pak");
			File.WriteAllText(source, "replacement");
			File.WriteAllText(destination, "installed");
			using var cancellation = new CancellationTokenSource();
			cancellation.Cancel();

			try
			{
				AtomicFileWriter.CopyFileAsync(source, destination, cancellationToken: cancellation.Token)
					.GetAwaiter().GetResult();
				throw new InvalidOperationException("Expected the copy to be cancelled.");
			}
			catch (OperationCanceledException)
			{
				RegressionAssert.Equal("installed", File.ReadAllText(destination));
				RegressionAssert.False(Directory.EnumerateFiles(directory, "*.tmp").Any());
			}
		});
	}

	public void GameLoadOrderChangeCanBeUndoneAndRedone()
	{
		WithTemporaryDirectory(directory =>
		{
			var path = Path.Combine(directory, "modsettings.lsx");
			File.WriteAllText(path, "before");
			var before = ReversibleFileChangeService.Capture(path);
			File.WriteAllText(path, "after");
			var after = ReversibleFileChangeService.Capture(path);

			RegressionAssert.True(ReversibleFileChangeService.TryRestore(path, after, before, out _));
			RegressionAssert.Equal("before", File.ReadAllText(path));
			RegressionAssert.True(ReversibleFileChangeService.TryRestore(path, before, after, out _));
			RegressionAssert.Equal("after", File.ReadAllText(path));
		});
	}

	public void GameLoadOrderUndoRefusesToOverwriteANewerExternalChange()
	{
		WithTemporaryDirectory(directory =>
		{
			var path = Path.Combine(directory, "modsettings.lsx");
			File.WriteAllText(path, "before");
			var before = ReversibleFileChangeService.Capture(path);
			File.WriteAllText(path, "redux export");
			var exported = ReversibleFileChangeService.Capture(path);
			File.WriteAllText(path, "newer external change");

			RegressionAssert.False(ReversibleFileChangeService.TryRestore(path, exported, before, out var error));
			RegressionAssert.True(error.Contains("newer file", StringComparison.OrdinalIgnoreCase));
			RegressionAssert.Equal("newer external change", File.ReadAllText(path));
		});
	}

	public void FirstGameLoadOrderExportCanUndoBackToNoFile()
	{
		WithTemporaryDirectory(directory =>
		{
			var path = Path.Combine(directory, "modsettings.lsx");
			var missing = ReversibleFileChangeService.Capture(path);
			File.WriteAllText(path, "first export");
			var exported = ReversibleFileChangeService.Capture(path);

			RegressionAssert.True(ReversibleFileChangeService.TryRestore(path, exported, missing, out _));
			RegressionAssert.False(File.Exists(path));
			RegressionAssert.True(ReversibleFileChangeService.TryRestore(path, missing, exported, out _));
			RegressionAssert.Equal("first export", File.ReadAllText(path));
		});
	}

	public void ProviderCredentialsAreEncryptedAndExcludedFromSettingsJson()
	{
		WithTemporaryDirectory(directory =>
		{
			const string nexusKey = "nexus-secret-regression-value";
			const string modioKey = "modio-secret-regression-value";
			var credentialPath = Path.Combine(directory, "provider-credentials.dat");

			ProtectedCredentialStore.Save(credentialPath, nexusKey, modioKey);
			var storedText = Encoding.UTF8.GetString(File.ReadAllBytes(credentialPath));
			RegressionAssert.False(storedText.Contains(nexusKey, StringComparison.Ordinal));
			RegressionAssert.False(storedText.Contains(modioKey, StringComparison.Ordinal));
			RegressionAssert.True(ProtectedCredentialStore.TryLoad(
				credentialPath, out var loadedNexusKey, out var loadedModioKey));
			RegressionAssert.Equal(nexusKey, loadedNexusKey);
			RegressionAssert.Equal(modioKey, loadedModioKey);

			var settingsJson = JsonConvert.SerializeObject(new DivinityModManagerSettings
			{
				NexusModsAPIKey = nexusKey,
				ModioAPIKey = modioKey
			});
			RegressionAssert.False(settingsJson.Contains(nexusKey, StringComparison.Ordinal));
			RegressionAssert.False(settingsJson.Contains(modioKey, StringComparison.Ordinal));
		});
	}

	private static void WithTemporaryDirectory(Action<string> action)
	{
		var directory = Path.Combine(Path.GetTempPath(), "ReduxFileSafetyTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			action(directory);
		}
		finally
		{
			if (Directory.Exists(directory)) Directory.Delete(directory, true);
		}
	}
}
