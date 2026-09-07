#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using DivinityModManager.AppServices;
using DivinityModManager.Models.NexusMods;

namespace Redux.Core.Tests;

internal sealed class NexusNxmResolverTests
{
	public void FreeUserResolutionUsesExactFileAndAuthorization()
	{
		var api = new FakeNxmApi { User = new NxmApiUser(7, false) };
		var resolver = new NexusNxmResolver(api, () => DateTimeOffset.FromUnixTimeSeconds(2_000_000_000));
		var link = new NexusModManagerLink(42, 99, "secret", 2_000_000_300, 7);

		var result = resolver.ResolveMetadataAsync(link, CancellationToken.None).GetAwaiter().GetResult();
		RegressionAssert.Equal(0, api.DownloadCallCount);
		resolver.ResolveDownloadUriAsync(link, CancellationToken.None).GetAwaiter().GetResult();

		RegressionAssert.Equal(42L, api.ModId);
		RegressionAssert.Equal(99L, api.FileId);
		RegressionAssert.Equal("secret", api.DownloadKey);
		RegressionAssert.Equal(2_000_000_300L, api.Expires);
		RegressionAssert.Equal("Example Mod", result.ProjectName);
		RegressionAssert.Equal("Example-1.0.zip", result.FileName);
		RegressionAssert.Equal("https://static.example.test/mod.webp", result.ThumbnailUrl);
		RegressionAssert.True(result.RequiresAuthorization);
	}

	public void PremiumResolutionDoesNotForwardShortLivedAuthorization()
	{
		var api = new FakeNxmApi { User = new NxmApiUser(7, true) };
		var resolver = new NexusNxmResolver(api);

		var link = new NexusModManagerLink(42, 99, "secret", 4_000_000_000, 7);
		var metadata = resolver.ResolveMetadataAsync(link, CancellationToken.None).GetAwaiter().GetResult();
		resolver.ResolveDownloadUriAsync(link, CancellationToken.None).GetAwaiter().GetResult();

		RegressionAssert.Equal(null, api.DownloadKey);
		RegressionAssert.Equal(null, api.Expires);
		RegressionAssert.False(metadata.RequiresAuthorization);
	}

	public void FreeUserRequiresFreshMatchingAuthorization()
	{
		var api = new FakeNxmApi { User = new NxmApiUser(8, false) };
		var resolver = new NexusNxmResolver(api, () => DateTimeOffset.FromUnixTimeSeconds(2_000_000_000));

		RegressionAssert.Throws<InvalidOperationException>(() => resolver.ResolveDownloadUriAsync(
			new NexusModManagerLink(42, 99, null, null, null), CancellationToken.None).GetAwaiter().GetResult());
		RegressionAssert.Throws<InvalidOperationException>(() => resolver.ResolveDownloadUriAsync(
			new NexusModManagerLink(42, 99, "secret", 2_000_000_300, 7), CancellationToken.None).GetAwaiter().GetResult());
		RegressionAssert.Throws<InvalidOperationException>(() => resolver.ResolveDownloadUriAsync(
			new NexusModManagerLink(42, 99, "secret", 1_999_999_999, 8), CancellationToken.None).GetAwaiter().GetResult());
	}

	public void ResolutionRejectsWrongFileAndUnsafeDownloadUri()
	{
		var api = new FakeNxmApi { User = new NxmApiUser(7, true), ReturnedFileId = 100 };
		var resolver = new NexusNxmResolver(api);
		RegressionAssert.Throws<InvalidOperationException>(() => resolver.ResolveMetadataAsync(
			new NexusModManagerLink(42, 99, null, null, null), CancellationToken.None).GetAwaiter().GetResult());

		api.ReturnedFileId = 99;
		api.Downloads = new[] { new Uri("http://cdn.example.test/file.zip") };
		RegressionAssert.Throws<InvalidOperationException>(() => resolver.ResolveDownloadUriAsync(
			new NexusModManagerLink(42, 99, null, null, null), CancellationToken.None).GetAwaiter().GetResult());
	}

	public void ThirdPartyFailureDoesNotExposeSignedUrl()
	{
		var api = new FakeNxmApi
		{
			UserException = new InvalidOperationException("request failed: https://example.test/file?key=secret")
		};
		var resolver = new NexusNxmResolver(api);

		try
		{
			resolver.ResolveMetadataAsync(new NexusModManagerLink(42, 99, "secret", 4_000_000_000, 7), CancellationToken.None)
				.GetAwaiter().GetResult();
			throw new InvalidOperationException("Expected NxmResolutionException.");
		}
		catch (NxmResolutionException ex)
		{
			RegressionAssert.Equal(NxmResolutionFailureKind.ServiceUnavailable, ex.Kind);
			RegressionAssert.False(ex.Message.Contains("secret", StringComparison.Ordinal));
			RegressionAssert.False(ex.Message.Contains("example.test", StringComparison.Ordinal));
		}
	}

	private sealed class FakeNxmApi : INexusNxmApi
	{
		public NxmApiUser User { get; set; } = new(7, false);
		public long ReturnedFileId { get; set; } = 99;
		public IReadOnlyList<Uri> Downloads { get; set; } = new[] { new Uri("https://cdn.example.test/file.zip") };
		public long ModId { get; private set; }
		public long FileId { get; private set; }
		public string DownloadKey { get; private set; }
		public long? Expires { get; private set; }
		public int DownloadCallCount { get; private set; }
		public Exception UserException { get; set; }

		public Task<NxmApiUser> GetUserAsync(CancellationToken cancellationToken) => UserException == null
			? Task.FromResult(User)
			: Task.FromException<NxmApiUser>(UserException);
		public Task<NxmApiMod> GetModAsync(long modId, CancellationToken cancellationToken)
		{
			ModId = modId;
			return Task.FromResult(new NxmApiMod(modId, "Example Mod", "Author", "https://static.example.test/mod.webp"));
		}
		public Task<NxmApiFile> GetFileAsync(long modId, long fileId, CancellationToken cancellationToken)
		{
			FileId = fileId;
			return Task.FromResult(new NxmApiFile(ReturnedFileId, "Example File", "Example-1.0.zip", "1.0", 1024));
		}
		public Task<IReadOnlyList<Uri>> GetDownloadLinksAsync(long modId, long fileId, string key, long? expires, CancellationToken cancellationToken)
		{
			DownloadCallCount++;
			DownloadKey = key;
			Expires = expires;
			return Task.FromResult(Downloads);
		}
	}
}
