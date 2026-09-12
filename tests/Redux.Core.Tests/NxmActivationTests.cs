#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using DivinityModManager.AppServices;
using DivinityModManager;
using DivinityModManager.Util;

namespace Redux.Core.Tests;

internal sealed class NxmActivationTests
{
	public void ForkAndUpstreamInstancesAndActivationRemainIndependent()
	{
		var suffix = ".Test." + Guid.NewGuid().ToString("N");
		var upstreamIdentity = "BG3ModManagerRedux.Instance" + suffix;
		var forkIdentity = DivinityApp.REDUX_FORK_INSTANCE_NAME + suffix;
		using var upstreamGuard = new ReduxInstanceGuard(upstreamIdentity);
		using var forkGuard = new ReduxInstanceGuard(forkIdentity);
		RegressionAssert.True(upstreamGuard.TryAcquire());
		RegressionAssert.True(forkGuard.TryAcquire());
		RegressionAssert.False(Task.Run(() =>
		{
			using var duplicate = new ReduxInstanceGuard(forkIdentity);
			return duplicate.TryAcquire();
		}).GetAwaiter().GetResult());

		var upstreamReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		var forkReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var upstream = NxmActivationCoordinator.CreateForUser("shared-instance" + suffix);
		using var fork = NxmActivationCoordinator.CreateForUser(forkIdentity);
		RegressionAssert.True(upstream.StartListening(value => { upstreamReceived.TrySetResult(value); return Task.CompletedTask; }));
		RegressionAssert.True(fork.StartListening(value => { forkReceived.TrySetResult(value); return Task.CompletedTask; }));
		using var upstreamClient = NxmActivationCoordinator.CreateForUser("shared-instance" + suffix);
		using var forkClient = NxmActivationCoordinator.CreateForUser(forkIdentity);
		const string upstreamLink = "nxm://baldursgate3/mods/1/files/2";
		const string forkLink = "nxm://baldursgate3/mods/3/files/4";
		RegressionAssert.True(forkClient.TryForwardAsync(forkLink, TimeSpan.FromSeconds(2)).GetAwaiter().GetResult());
		RegressionAssert.Equal(forkLink, forkReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult());
		RegressionAssert.False(upstreamReceived.Task.IsCompleted);
		RegressionAssert.True(upstreamClient.TryForwardAsync(upstreamLink, TimeSpan.FromSeconds(2)).GetAwaiter().GetResult());
		RegressionAssert.Equal(upstreamLink, upstreamReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult());
	}

	public void SameUserPipeDeliversValidatedLink()
	{
		var identity = @"C:\Tests\" + Guid.NewGuid().ToString("N") + @"\Redux.exe";
		var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var server = NxmActivationCoordinator.CreateForExecutable(identity);
		RegressionAssert.True(server.StartListening(value =>
		{
			received.TrySetResult(value);
			return Task.CompletedTask;
		}));
		using var client = NxmActivationCoordinator.CreateForExecutable(identity);
		const string link = "nxm://baldursgate3/mods/42/files/99";

		RegressionAssert.True(client.TryForwardAsync(link, TimeSpan.FromSeconds(2)).GetAwaiter().GetResult());
		RegressionAssert.Equal(link, received.Task.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult());
	}

	public void OversizedMessageIsRejectedBeforeConnection()
	{
		using var client = NxmActivationCoordinator.CreateForExecutable(@"C:\Tests\" + Guid.NewGuid().ToString("N") + @"\Redux.exe");
		var oversized = "nxm://baldursgate3/mods/42/files/99?key=" + new string('x', NexusModManagerLinkParser.MaximumLength);

		RegressionAssert.False(client.TryForwardAsync(oversized, TimeSpan.FromMilliseconds(50)).GetAwaiter().GetResult());
	}

	public void ListenerSurvivesMalformedClientMessage()
	{
		var identity = @"C:\Tests\" + Guid.NewGuid().ToString("N") + @"\Redux.exe";
		var received = new List<string>();
		using var server = NxmActivationCoordinator.CreateForExecutable(identity);
		RegressionAssert.True(server.StartListening(value =>
		{
			lock (received) received.Add(value);
			return Task.CompletedTask;
		}));
		using var client = NxmActivationCoordinator.CreateForExecutable(identity);

		RegressionAssert.True(client.TryForwardAsync("not-an-nxm-link", TimeSpan.FromSeconds(2), validate: false).GetAwaiter().GetResult());
		RegressionAssert.True(client.TryForwardAsync("nxm://baldursgate3/mods/5/files/6", TimeSpan.FromSeconds(2)).GetAwaiter().GetResult());
		SpinWait.SpinUntil(() => { lock (received) return received.Count == 1; }, TimeSpan.FromSeconds(2));

		lock (received)
		{
			RegressionAssert.Equal(1, received.Count);
			RegressionAssert.Equal("nxm://baldursgate3/mods/5/files/6", received[0]);
		}
	}
}
