namespace ReduxUpdater;

public sealed class ReduxUpdateRequest
{
	public int SchemaVersion { get; init; }
	public int ParentProcessId { get; init; }
	public string TargetDirectory { get; init; } = String.Empty;
	public string StagedDirectory { get; init; } = String.Empty;
	public string BackupDirectory { get; init; } = String.Empty;
	public string ResultPath { get; init; } = String.Empty;
	public string DisplayVersion { get; init; } = String.Empty;
	public string RelaunchRelativePath { get; init; } = "Redux.exe";
}

public sealed class ReduxUpdateResult
{
	public bool Succeeded { get; init; }
	public string DisplayVersion { get; init; } = String.Empty;
	public string Message { get; init; } = String.Empty;
	public string CompletedAtUtc { get; init; } = String.Empty;
}

internal sealed class ReduxReleaseInventory
{
	public int SchemaVersion { get; init; }
	public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
}
