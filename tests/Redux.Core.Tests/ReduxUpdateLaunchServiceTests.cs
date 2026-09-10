using DivinityModManager.AppServices;
using DivinityModManager.Models.Updates;

using ReduxUpdater;

using System;
using System.IO;

namespace Redux.Core.Tests;

public sealed class ReduxUpdateLaunchServiceTests
{
	public void CompletedUpdateResultIsShownOnce()
	{
		var root = TemporaryDirectory();
		try
		{
			var path = Path.Combine(root, "result.json");
			ReduxUpdateTransaction.WriteResult(path, new ReduxUpdateResult
			{
				Succeeded = true,
				DisplayVersion = "0.1.0-alpha.15",
				Message = "Redux was updated to 0.1.0-alpha.15.",
				CompletedAtUtc = "2026-09-08T23:30:00.0000000+00:00"
			});

			RegressionAssert.True(ReduxUpdateResultService.TryConsume(out var result, path));
			RegressionAssert.True(result.Succeeded);
			RegressionAssert.Equal("0.1.0-alpha.15", result.DisplayVersion);
			RegressionAssert.False(File.Exists(path));
			RegressionAssert.False(ReduxUpdateResultService.TryConsume(out _, path));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	public void QueuedRunnerLivesOutsideTheInstallationAndCancellationCleansIt()
	{
		var root = TemporaryDirectory();
		try
		{
			var target = Path.Combine(root, "target");
			var transaction = Path.Combine(root, "transaction");
			var staged = Path.Combine(transaction, "staged");
			var updater = Path.Combine(staged, "Updater");
			Directory.CreateDirectory(target);
			Directory.CreateDirectory(updater);
			File.WriteAllText(Path.Combine(target, "Redux.exe"), "old");
			foreach (var name in new[] { "ReduxUpdater.exe", "ReduxUpdater.dll", "ReduxUpdater.deps.json", "ReduxUpdater.runtimeconfig.json" })
				File.WriteAllText(Path.Combine(updater, name), name);
			var service = new ReduxUpdateLaunchService();

			service.Queue(new ReduxPreparedUpdate
			{
				DisplayVersion = "0.1.0-alpha.15",
				TransactionDirectory = transaction,
				StagedDirectory = staged
			}, target, 42);

			RegressionAssert.True(service.HasPendingUpdate);
			var request = File.ReadAllText(Path.Combine(transaction, "runner", "request.json"));
			RegressionAssert.Contains(request, "0.1.0-alpha.15");
			RegressionAssert.Contains(request, Path.GetFullPath(target).Replace("\\", "\\\\"));
			var parsed = ReduxUpdateTransaction.ReadRequest(Path.Combine(transaction, "runner", "request.json"));
			RegressionAssert.Equal(Path.GetFullPath(target), parsed.TargetDirectory);
			RegressionAssert.Equal(42, parsed.ParentProcessId);
			service.CancelPending();
			RegressionAssert.False(service.HasPendingUpdate);
			RegressionAssert.False(Directory.Exists(transaction));
		}
		finally
		{
			if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
		}
	}

	private static string TemporaryDirectory()
	{
		var path = Path.Combine(Path.GetTempPath(), "redux-update-launch-tests-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}
}
