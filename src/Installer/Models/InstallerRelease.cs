using System;
using System.Collections.Generic;
using System.IO;

namespace ReduxInstaller.Models;

internal sealed class InstallerReleaseManifest
{
	public int SchemaVersion { get; set; }
	public string Channel { get; set; } = String.Empty;
	public string DisplayVersion { get; set; } = String.Empty;
	public string InternalVersion { get; set; } = String.Empty;
	public DateTimeOffset PublishedAtUtc { get; set; }
	public string ReleaseNotesUrl { get; set; } = String.Empty;
	public InstallerReleaseArtifact Artifact { get; set; } = new InstallerReleaseArtifact();
}

internal sealed class InstallerReleaseArtifact
{
	public string Kind { get; set; } = String.Empty;
	public string Url { get; set; } = String.Empty;
	public long SizeBytes { get; set; }
	public string Sha256 { get; set; } = String.Empty;
}

internal sealed class InstallerReleaseInventory
{
	public int SchemaVersion { get; set; }
	public IReadOnlyList<string> Files { get; set; } = Array.Empty<string>();
}

internal sealed class PreparedInstallerPackage : IDisposable
{
	public string WorkingDirectory { get; set; } = String.Empty;
	public string PayloadDirectory { get; set; } = String.Empty;
	public InstallerReleaseManifest Manifest { get; set; } = new InstallerReleaseManifest();
	public InstallerReleaseInventory Inventory { get; set; } = new InstallerReleaseInventory();
	private bool _retained;

	public void Retain() => _retained = true;

	public void Dispose()
	{
		if (_retained || String.IsNullOrWhiteSpace(WorkingDirectory)) return;
		try
		{
			if (Directory.Exists(WorkingDirectory)) Directory.Delete(WorkingDirectory, recursive: true);
		}
		catch
		{
			// A later setup run or normal temporary-file cleanup can remove abandoned staging.
		}
	}
}

internal sealed class InstallerProgress
{
	public string Status { get; set; } = String.Empty;
	public double Fraction { get; set; }
}
