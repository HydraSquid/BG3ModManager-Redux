namespace DivinityModManager.Models.Updates;

public static class ReduxUpdateChannels
{
	public const string PublicAlpha = "public-alpha";
}

public static class ReduxUpdateArtifactKinds
{
	public const string Portable = "portable";
}

public sealed class ReduxUpdateManifest
{
	public int SchemaVersion { get; init; }
	public string Channel { get; init; } = String.Empty;
	public string DisplayVersion { get; init; } = String.Empty;
	public string InternalVersion { get; init; } = String.Empty;
	public DateTimeOffset PublishedAtUtc { get; init; }
	public string ReleaseNotesUrl { get; init; } = String.Empty;
	public IReadOnlyList<ReduxUpdateArtifact> Artifacts { get; init; } = Array.Empty<ReduxUpdateArtifact>();
}

public sealed class ReduxUpdateArtifact
{
	public string Kind { get; init; } = String.Empty;
	public string Url { get; init; } = String.Empty;
	public long SizeBytes { get; init; }
	public string Sha256 { get; init; } = String.Empty;
}

public enum ReduxUpdateAvailability
{
	UpToDate,
	UpdateAvailable,
	InstalledVersionIsNewer
}

public sealed class ReduxUpdateDecision
{
	public ReduxUpdateAvailability Availability { get; init; }
	public ReduxUpdateManifest Manifest { get; init; }
	public ReduxUpdateArtifact Artifact { get; init; }
}

public sealed class ReduxReleaseInventory
{
	public int SchemaVersion { get; init; }
	public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
}

public sealed class ReduxPreparedUpdate
{
	public string DisplayVersion { get; init; } = String.Empty;
	public string TransactionDirectory { get; init; } = String.Empty;
	public string StagedDirectory { get; init; } = String.Empty;
	public ReduxReleaseInventory Inventory { get; init; }
}

public sealed class ReduxUpdateProgress
{
	public string Status { get; init; } = String.Empty;
	public double Fraction { get; init; }
}

public sealed class ReduxUpdateCompletionResult
{
	public bool Succeeded { get; init; }
	public string DisplayVersion { get; init; } = String.Empty;
	public string Message { get; init; } = String.Empty;
	public DateTimeOffset CompletedAtUtc { get; init; }
}
