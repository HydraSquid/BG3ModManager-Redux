using System.Runtime.Serialization;

namespace DivinityModManager.Models.NexusMods;

public enum NxmDownloadState
{
	Resolving,
	Queued,
	Downloading,
	Paused,
	RetryWaiting,
	Downloaded,
	NeedsReview,
	Installing,
	Installed,
	NeedsFreshLink,
	Failed,
	InstallFailed
}

[DataContract]
public sealed class NxmDownloadItem : ReactiveObject
{
	[DataMember(Order = 1), Reactive] public string Id { get; set; } = Guid.NewGuid().ToString("N");
	[DataMember(Order = 2), Reactive] public long QueuePosition { get; set; }
	[DataMember(Order = 3), Reactive] public long ModId { get; set; }
	[DataMember(Order = 4), Reactive] public long FileId { get; set; }
	[DataMember(Order = 5), Reactive] public string ProjectName { get; set; } = String.Empty;
	[DataMember(Order = 6), Reactive] public string Author { get; set; } = String.Empty;
	[DataMember(Order = 7), Reactive] public string FileDisplayName { get; set; } = String.Empty;
	[DataMember(Order = 8), Reactive] public string FileName { get; set; } = String.Empty;
	[DataMember(Order = 9), Reactive] public string Version { get; set; } = String.Empty;
	[DataMember(Order = 10), Reactive] public long SizeBytes { get; set; }
	[DataMember(Order = 11), Reactive] public long BytesReceived { get; set; }
	[DataMember(Order = 12), Reactive] public string ETag { get; set; } = String.Empty;
	[DataMember(Order = 13), Reactive] public DateTimeOffset? LastModified { get; set; }
	[DataMember(Order = 14), Reactive] public string PartialFileName { get; set; } = String.Empty;
	[DataMember(Order = 15), Reactive] public string CompletedFileName { get; set; } = String.Empty;
	private NxmDownloadState _state;
	[DataMember(Order = 16)]
	public NxmDownloadState State
	{
		get => _state;
		set
		{
			this.RaiseAndSetIfChanged(ref _state, value);
			this.RaisePropertyChanged(nameof(StatusText));
			this.RaisePropertyChanged(nameof(FailureDetails));
			this.RaisePropertyChanged(nameof(CanDownloadAgain));
			this.RaisePropertyChanged(nameof(CanRetryInstall));
		}
	}
	[DataMember(Order = 17), Reactive] public bool RequiresAuthorization { get; set; }
	[DataMember(Order = 18), Reactive] public int RetryCount { get; set; }
	[DataMember(Order = 19), Reactive] public DateTimeOffset? RetryAfter { get; set; }
	private string _errorCode = String.Empty;
	[DataMember(Order = 20)] public string ErrorCode
	{
		get => _errorCode;
		set
		{
			this.RaiseAndSetIfChanged(ref _errorCode, value ?? String.Empty);
			this.RaisePropertyChanged(nameof(StatusText));
			this.RaisePropertyChanged(nameof(FailureDetails));
			this.RaisePropertyChanged(nameof(CanDownloadAgain));
			this.RaisePropertyChanged(nameof(CanRetryInstall));
		}
	}
	[DataMember(Order = 21), Reactive] public bool IsSelected { get; set; }
	private string _errorDetails = String.Empty;
	[DataMember(Order = 22)] public string ErrorDetails
	{
		get => _errorDetails;
		set
		{
			this.RaiseAndSetIfChanged(ref _errorDetails, value ?? String.Empty);
			this.RaisePropertyChanged(nameof(FailureDetails));
		}
	}

	[IgnoreDataMember] public NexusModManagerLink Authorization { get; set; }
	[IgnoreDataMember, Reactive] public double BytesPerSecond { get; set; }
	[IgnoreDataMember, Reactive] public double Progress { get; set; }

	public string Identity => $"{ModId}:{FileId}";
	public string StatusText => State switch
	{
		NxmDownloadState.Resolving => "Getting file details",
		NxmDownloadState.Queued => "Queued",
		NxmDownloadState.Downloading => "Downloading",
		NxmDownloadState.Paused => "Paused",
		NxmDownloadState.RetryWaiting => "Waiting to retry",
		NxmDownloadState.Downloaded => "Ready to install",
		NxmDownloadState.NeedsReview => "Review before installing",
		NxmDownloadState.Installing => "Installing",
		NxmDownloadState.Installed => "Installed",
		NxmDownloadState.NeedsFreshLink => "Open Nexus to continue",
		NxmDownloadState.Failed => "Failed",
		NxmDownloadState.InstallFailed => ErrorCode switch
		{
			"missing-dependencies" => "Blocked by dependencies",
			"archive-unreadable" => "Archive unreadable",
			"archive-layout" => "Unsupported archive layout",
			"invalid-package" => "Package validation failed",
			"rollback-failed" => "Manual recovery required",
			_ => "Installation failed"
		},
		_ => String.Empty
	};
	public bool CanDownloadAgain => ErrorCode != "rollback-failed" && State is
		(NxmDownloadState.Downloaded or NxmDownloadState.NeedsReview or NxmDownloadState.InstallFailed or NxmDownloadState.Failed);
	public bool CanRetryInstall => State == NxmDownloadState.InstallFailed
		&& ErrorCode is not ("archive-unreadable" or "rollback-failed");
	public string FailureDetails => !String.IsNullOrWhiteSpace(ErrorDetails) ? ErrorDetails : ErrorCode switch
	{
		"missing-file" => "The downloaded archive is missing. Download it again; a fresh Nexus link may be required.",
		"file-size-mismatch" => "The archive size differs from the completed download record. Download it again before installing.",
		"fresh-link-required" => "Open this file on Nexus Mods and choose Mod Manager Download to authorize a new download.",
		"install-failed" => "This older failure record did not retain its cause. Open Details to inspect the archive and check its requirements without installing it.",
		"transfer-failed" => "The transfer failed. Check the connection and available disk space, then retry the download.",
		_ => StatusText
	};
	public Uri NexusPage => ModId > 0
		? new Uri($"https://www.nexusmods.com/baldursgate3/mods/{ModId}?tab=files&file_id={FileId}")
		: null;
}
