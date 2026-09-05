using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

using DivinityModManager.Util;

namespace DivinityModManager.AppServices;

public sealed class NxmActivationCoordinator : IDisposable
{
	private static readonly UTF8Encoding StrictUtf8 = new(false, true);
	private readonly string _pipeName;
	private readonly Semaphore _listenerLease;
	private readonly CancellationTokenSource _cancellation = new();
	private Task _listenerTask;
	private bool _ownsListener;

	private NxmActivationCoordinator(string identity)
	{
		var user = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
		var normalized = Path.GetFullPath(identity).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
		var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user + "\n" + normalized))).ToLowerInvariant();
		_pipeName = "BG3ModManagerRedux.Nxm." + hash[..32];
		_listenerLease = new Semaphore(1, 1, @"Local\" + _pipeName + ".listener");
	}

	public static NxmActivationCoordinator CreateForExecutable(string executablePath) =>
		new(executablePath ?? throw new ArgumentNullException(nameof(executablePath)));

	public bool StartListening(Func<string, Task> handler)
	{
		ArgumentNullException.ThrowIfNull(handler);
		if (_listenerTask != null) return _ownsListener;
		_ownsListener = _listenerLease.WaitOne(0);
		if (!_ownsListener) return false;
		_listenerTask = Task.Run(() => ListenAsync(handler, _cancellation.Token));
		return true;
	}

	public async Task<bool> TryForwardAsync(string value, TimeSpan timeout, bool validate = true)
	{
		if (validate && !NexusModManagerLinkParser.TryReadGame(value, out _)) return false;
		byte[] payload;
		try
		{
			payload = StrictUtf8.GetBytes(value ?? String.Empty);
		}
		catch (EncoderFallbackException)
		{
			return false;
		}
		if (payload.Length == 0 || payload.Length > NexusModManagerLinkParser.MaximumLength) return false;

		using var timeoutSource = new CancellationTokenSource(timeout);
		try
		{
			await using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out,
				PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
			await client.ConnectAsync(timeoutSource.Token);
			var length = BitConverter.GetBytes(payload.Length);
			await client.WriteAsync(length, timeoutSource.Token);
			await client.WriteAsync(payload, timeoutSource.Token);
			await client.FlushAsync(timeoutSource.Token);
			return true;
		}
		catch (Exception ex) when (ex is IOException or OperationCanceledException or UnauthorizedAccessException)
		{
			return false;
		}
	}

	private async Task ListenAsync(Func<string, Task> handler, CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				await using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1,
					PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
					NexusModManagerLinkParser.MaximumLength + sizeof(int), NexusModManagerLinkParser.MaximumLength + sizeof(int));
				await server.WaitForConnectionAsync(cancellationToken);
				var lengthBytes = new byte[sizeof(int)];
				if (!await ReadExactlyAsync(server, lengthBytes, cancellationToken)) continue;
				var length = BitConverter.ToInt32(lengthBytes);
				if (length <= 0 || length > NexusModManagerLinkParser.MaximumLength) continue;
				var payload = new byte[length];
				if (!await ReadExactlyAsync(server, payload, cancellationToken)) continue;
				var value = StrictUtf8.GetString(payload);
				if (NexusModManagerLinkParser.TryReadGame(value, out _)) await handler(value);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
			{
				// A malformed or disconnected client cannot terminate the listener.
			}
		}
	}

	private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
	{
		var offset = 0;
		while (offset < buffer.Length)
		{
			var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
			if (read == 0) return false;
			offset += read;
		}
		return true;
	}

	public void Dispose()
	{
		_cancellation.Cancel();
		try { _listenerTask?.Wait(TimeSpan.FromSeconds(2)); }
		catch (AggregateException ex) when (ex.InnerExceptions.All(inner => inner is OperationCanceledException)) { }
		if (_ownsListener)
		{
			_listenerLease.Release();
			_ownsListener = false;
		}
		_listenerLease.Dispose();
		_cancellation.Dispose();
	}
}
