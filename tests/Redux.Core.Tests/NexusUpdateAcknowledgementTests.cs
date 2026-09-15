using DivinityModManager.AppServices;
using DivinityModManager.Views;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Redux.Core.Tests;

public sealed class NexusUpdateAcknowledgementTests
{
	public void ReferenceSurvivesRestartAndLaterSameVersionReplacementAppears()
	{
		using var fixture = new Fixture();
		var service = fixture.Checked();
		service.SetReferenceAsync(fixture.Installed, 20).GetAwaiter().GetResult();
		var acknowledged = fixture.Service().GetCachedResults([fixture.Installed]).Single();
		RegressionAssert.Equal(NexusModUpdateStatus.Acknowledged, acknowledged.Status);
		RegressionAssert.Equal(10L, acknowledged.Installed.FileId);
		RegressionAssert.Equal(20L, acknowledged.Acknowledgement.FileId);
		fixture.Now += TimeSpan.FromDays(1);
		fixture.Client.Data.Files.Add(new(30, "Newer main file with unchanged version text", "1.1.1", 1));
		fixture.Client.Data.Replacements.Add(new(20, 30));
		var next = fixture.Checked().GetCachedResults([fixture.Installed]).Single();
		RegressionAssert.Equal(NexusModUpdateStatus.UpdateAvailable, next.Status);
		RegressionAssert.Equal(30L, next.Candidate.FileId);
	}

	public void UnlinkedOptionalUploadDoesNotOverrideChosenReference()
	{
		using var fixture = new Fixture();
		var service = fixture.Checked();
		service.SetReferenceAsync(fixture.Installed, 20).GetAwaiter().GetResult();
		fixture.Now += TimeSpan.FromDays(1);
		fixture.Client.Data.Files.Add(new(999, "Optional patch", "999.0", 3));
		RegressionAssert.Equal(NexusModUpdateStatus.Acknowledged, fixture.Checked().GetCachedResults([fixture.Installed]).Single().Status);
	}

	public void ChangedPackageProjectOrUuidCannotReuseReference()
	{
		using var fixture = new Fixture();
		var service = fixture.Checked();
		service.SetReferenceAsync(fixture.Installed, 20).GetAwaiter().GetResult();
		foreach (var changed in new[] { fixture.Installed with { PackageSha256 = new string('a', 64) },
			fixture.Installed with { ModId = 43 }, fixture.Installed with { Uuid = Guid.NewGuid().ToString() } })
			RegressionAssert.Equal(null, service.GetCachedResults([changed]).Single().Acknowledgement);
	}

	public void UnknownInstalledDownloadCanUseReferenceWithoutInventingIdentity()
	{
		using var fixture = new Fixture();
		var unknown = fixture.Installed with { FileId = 0, HasExactFileIdentity = false };
		var service = fixture.Checked();
		service.SetReferenceAsync(unknown, 20).GetAwaiter().GetResult();
		var result = fixture.Service().GetCachedResults([unknown]).Single();
		RegressionAssert.Equal(NexusModUpdateStatus.Acknowledged, result.Status);
		RegressionAssert.False(result.Installed.HasExactFileIdentity);
		RegressionAssert.Equal(0L, result.Installed.FileId);
		RegressionAssert.Contains(result.ReferenceLabel, "not proof of installation");
		service.ResetReference(unknown);
		RegressionAssert.Equal(NexusModUpdateStatus.DownloadNotIdentified, service.GetCachedResults([unknown]).Single().Status);
	}

	public void StaleMissingAndChangedPackageReferencesAreRejected()
	{
		using var fixture = new Fixture();
		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Service().SetReferenceAsync(fixture.Installed, 20).GetAwaiter().GetResult());
		var service = fixture.Checked();
		RegressionAssert.Throws<InvalidOperationException>(() => service.SetReferenceAsync(fixture.Installed, 999).GetAwaiter().GetResult());
		fixture.Now += TimeSpan.FromHours(24);
		RegressionAssert.Throws<InvalidOperationException>(() => service.SetReferenceAsync(fixture.Installed, 20).GetAwaiter().GetResult());
		service = fixture.Checked();
		File.WriteAllText(fixture.Installed.FilePath, "different installed bytes");
		RegressionAssert.Throws<InvalidOperationException>(() => service.SetReferenceAsync(fixture.Installed, 20).GetAwaiter().GetResult());
		RegressionAssert.False(File.Exists(fixture.References));
	}

	public void ResetWorksWithoutApiCacheAndRestoresOriginalComparison()
	{
		using var fixture = new Fixture();
		var service = fixture.Checked();
		service.SetReferenceAsync(fixture.Installed, 20).GetAwaiter().GetResult();
		File.Delete(fixture.Cache);
		service = fixture.Service();
		RegressionAssert.True(service.GetCachedResults([fixture.Installed]).Single().Acknowledgement != null);
		service.ResetReference(fixture.Installed);
		RegressionAssert.Equal(null, service.GetCachedResults([fixture.Installed]).Single().Acknowledgement);
		RegressionAssert.Equal(NexusModUpdateStatus.UpdateAvailable, fixture.Checked().GetCachedResults([fixture.Installed]).Single().Status);
	}

	public void CorruptOrLockedReferenceStoreCannotOverwriteChoices()
	{
		using var fixture = new Fixture();
		var service = fixture.Checked();
		service.SetReferenceAsync(fixture.Installed, 20).GetAwaiter().GetResult();
		var original = File.ReadAllText(fixture.References);
		using (var lease = new FileStream(fixture.References + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
			RegressionAssert.Throws<IOException>(() => service.ResetReference(fixture.Installed));
		RegressionAssert.Equal(original, File.ReadAllText(fixture.References));
		File.WriteAllText(fixture.References, "{corrupt");
		RegressionAssert.Throws<Newtonsoft.Json.JsonReaderException>(() => service.ResetReference(fixture.Installed));
		RegressionAssert.Equal("{corrupt", File.ReadAllText(fixture.References));
		service.GetCachedResults([fixture.Installed]);
		RegressionAssert.True(!String.IsNullOrEmpty(service.ReferenceWarning));
	}

	public void ReferenceStoreContainsNoPrivatePathOrApiKey()
	{
		using var fixture = new Fixture();
		fixture.Checked().SetReferenceAsync(fixture.Installed, 20).GetAwaiter().GetResult();
		var json = File.ReadAllText(fixture.References);
		RegressionAssert.False(json.Contains("fixture-key"));
		RegressionAssert.False(json.Contains("private-installed"));
		RegressionAssert.False(json.Contains("FilePath"));
	}

	public void PackageAndDownloadVersionsAreExplicitAndChooserStartsEmpty()
	{
		using var fixture = new Fixture();
		var result = fixture.Checked().GetCachedResults([fixture.Installed with { InstalledVersion = "1.1.0.0", DownloadVersion = "1.1.1" }]).Single();
		RegressionAssert.Contains(result.InstalledLabel, "Package metadata version: 1.1.0.0");
		RegressionAssert.Contains(result.InstalledLabel, "Verified download release: 1.1.1");
		var row = new NexusModUpdateRow(result);
		RegressionAssert.Equal(null, row.SelectedRelease);
		RegressionAssert.True(result.CanChooseReference);
	}

	private sealed class Client : INexusModFilesClient
	{
		public NexusProjectFiles Data = new([new(10, "Original main", "1.1.0", 4), new(20, "Main update", "1.1.1", 1)], [new(10, 20)]);
		public Task<NexusFilesResponse> FetchAsync(long projectId, string apiKey, CancellationToken cancellationToken) => Task.FromResult(new NexusFilesResponse(Data));
	}
	private sealed class Fixture : IDisposable
	{
		private readonly string _root = Path.Combine(Path.GetTempPath(), "redux-reference-tests-" + Guid.NewGuid().ToString("N"));
		public string Cache => Path.Combine(_root, "checks.json");
		public string References => Cache + ".references.json";
		public DateTimeOffset Now = DateTimeOffset.UtcNow;
		public Client Client { get; } = new();
		public NexusUpdateInstalledFile Installed { get; }
		public Fixture()
		{
			Directory.CreateDirectory(_root);
			var path = Path.Combine(_root, "private-installed.pak");
			File.WriteAllText(path, "installed PAK bytes");
			Installed = new(Guid.NewGuid().ToString(), "Example", 42, 10, "1.1.0.0", true)
			{ FilePath = path, PackageSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) };
		}
		public NexusModUpdateService Service() => new(Client, Cache, () => Now, (delay, _) => { Now += delay; return Task.CompletedTask; });
		public NexusModUpdateService Checked()
		{
			var service = Service();
			service.CheckAsync([Installed], "fixture-key", () => true).GetAwaiter().GetResult();
			return service;
		}
		public void Dispose() => Directory.Delete(_root, true);
	}
}
