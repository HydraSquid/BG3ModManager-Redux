namespace DivinityModManager.Models.NexusMods;

public sealed record NxmApiUser(long UserId, bool IsPremium);
public sealed record NxmApiMod(long ModId, string Name, string Author);
public sealed record NxmApiFile(long FileId, string Name, string FileName, string Version, long SizeBytes);
public sealed record NxmDownloadDescriptor(
	long ModId,
	long FileId,
	string ProjectName,
	string Author,
	string FileDisplayName,
	string FileName,
	string Version,
	long SizeBytes,
	bool RequiresAuthorization);
public sealed record DownloadProgress(long BytesReceived, long? TotalBytes);

public enum NxmResolutionFailureKind
{
	FreshLinkRequired,
	ExpiredAuthorization,
	AccountMismatch,
	NotFound,
	RateLimited,
	ServiceUnavailable
}

public sealed class NxmResolutionException : InvalidOperationException
{
	public NxmResolutionFailureKind Kind { get; }
	public DateTimeOffset? RetryAfter { get; }

	public NxmResolutionException(NxmResolutionFailureKind kind, string message, Exception innerException = null,
		DateTimeOffset? retryAfter = null)
		: base(message, innerException)
	{
		Kind = kind;
		RetryAfter = retryAfter;
	}
}
