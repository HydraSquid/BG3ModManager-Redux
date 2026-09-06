using DivinityModManager.Util;

namespace DivinityModManager.AppServices;

/// <summary>
/// Captures small files and restores them only while the destination still
/// contains the bytes written by the operation being undone or redone.
/// </summary>
public static class ReversibleFileChangeService
{
	public sealed record Snapshot(bool Exists, byte[] Contents)
	{
		public static Snapshot Missing { get; } = new(false, Array.Empty<byte>());
	}

	public static Snapshot Capture(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		return File.Exists(path)
			? new Snapshot(true, File.ReadAllBytes(path))
			: Snapshot.Missing;
	}

	public static bool TryRestore(
		string path,
		Snapshot expectedCurrent,
		Snapshot replacement,
		out string error)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		ArgumentNullException.ThrowIfNull(expectedCurrent);
		ArgumentNullException.ThrowIfNull(replacement);

		try
		{
			var current = Capture(path);
			if (!SnapshotsEqual(current, expectedCurrent))
			{
				error = "The game load-order file changed after Redux wrote it. Redux left the newer file untouched.";
				return false;
			}

			if (replacement.Exists)
			{
				AtomicFileWriter.WriteAllBytes(
					path,
					replacement.Contents,
					validateTemporaryFile: temporaryPath =>
						File.ReadAllBytes(temporaryPath).AsSpan().SequenceEqual(replacement.Contents));
			}
			else if (File.Exists(path))
			{
				File.Delete(path);
			}

			error = null;
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return false;
		}
	}

	private static bool SnapshotsEqual(Snapshot left, Snapshot right) =>
		left.Exists == right.Exists
		&& (!left.Exists || left.Contents.AsSpan().SequenceEqual(right.Contents));
}
