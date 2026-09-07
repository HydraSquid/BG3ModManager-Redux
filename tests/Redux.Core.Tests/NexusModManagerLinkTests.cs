using System;

using DivinityModManager.Util;

namespace Redux.Core.Tests;

internal sealed class NexusModManagerLinkTests
{
	private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(2_000_000_000);

	public void ParsesAuthenticatedBg3Link()
	{
		const string raw = "nxm://baldursgate3/mods/42/files/99?key=secret%2Bvalue&expires=2000000300&user_id=7";

		RegressionAssert.True(NexusModManagerLinkParser.TryParseBg3(raw, Now, out var link, out _));
		RegressionAssert.Equal(42L, link.ModId);
		RegressionAssert.Equal(99L, link.FileId);
		RegressionAssert.Equal("secret+value", link.DownloadKey);
		RegressionAssert.Equal(2_000_000_300L, link.ExpiresUnixSeconds);
		RegressionAssert.Equal(7L, link.UserId);
	}

	public void ParsesPremiumBg3LinkWithoutAuthorizationQuery()
	{
		RegressionAssert.True(NexusModManagerLinkParser.TryParseBg3(
			"nxm://baldursgate3/mods/42/files/99",
			Now,
			out var link,
			out _));
		RegressionAssert.Equal(null, link.DownloadKey);
		RegressionAssert.Equal(null, link.ExpiresUnixSeconds);
	}

	public void RejectsWrongSchemeOrGame()
	{
		RegressionAssert.False(NexusModManagerLinkParser.TryParseBg3("https://baldursgate3/mods/42/files/99", Now, out _, out _));
		RegressionAssert.False(NexusModManagerLinkParser.TryParseBg3("nxm://skyrim/mods/42/files/99", Now, out _, out _));
		RegressionAssert.True(NexusModManagerLinkParser.TryReadGame("nxm://skyrim/mods/42/files/99", out var game));
		RegressionAssert.Equal("skyrim", game);
	}

	public void RejectsUnsafeAuthorityAndPathForms()
	{
		var invalid = new[]
		{
			"nxm://user@baldursgate3/mods/42/files/99",
			"nxm://baldursgate3:123/mods/42/files/99",
			"nxm://baldursgate3/mods/42/files/99#fragment",
			"nxm://baldursgate3/mods/0/files/99",
			"nxm://baldursgate3/mods/42/files/-1",
			"nxm://baldursgate3/mods/42/files/99/extra",
			"nxm://baldursgate3/mods/42/files/99/",
			"nxm://baldursgate3/mods/%34%32/files/99",
			"nxm://baldursgate3/mods/9223372036854775808/files/99"
		};

		foreach (var value in invalid)
		{
			if (NexusModManagerLinkParser.TryParseBg3(value, Now, out _, out _))
				throw new InvalidOperationException($"Accepted unsafe URI: {value}");
		}
	}

	public void RejectsMalformedOrUnexpectedQueryData()
	{
		var invalid = new[]
		{
			"nxm://baldursgate3/mods/42/files/99?key=one&key=two&expires=2000000300",
			"nxm://baldursgate3/mods/42/files/99?other=value",
			"nxm://baldursgate3/mods/42/files/99?key=secret",
			"nxm://baldursgate3/mods/42/files/99?expires=2000000300",
			"nxm://baldursgate3/mods/42/files/99?key=secret&expires=not-a-number",
			"nxm://baldursgate3/mods/42/files/99?key=%ZZ&expires=2000000300",
			"nxm://baldursgate3/mods/42/files/99?key=secret&&expires=2000000300",
			"nxm://baldursgate3/mods/42/files/99?user_id=0"
		};

		foreach (var value in invalid)
		{
			if (NexusModManagerLinkParser.TryParseBg3(value, Now, out _, out _))
				throw new InvalidOperationException($"Accepted malformed query: {value}");
		}
	}

	public void RejectsExpiredAuthorization()
	{
		RegressionAssert.False(NexusModManagerLinkParser.TryParseBg3(
			"nxm://baldursgate3/mods/42/files/99?key=secret&expires=1999999999&user_id=7",
			Now,
			out _,
			out var error));
		RegressionAssert.Contains(error, "expired");
	}

	public void RejectsOversizedInput()
	{
		var value = "nxm://baldursgate3/mods/42/files/99?key=" + new string('a', 8192) + "&expires=2000000300";
		RegressionAssert.False(NexusModManagerLinkParser.TryParseBg3(value, Now, out _, out var error));
		RegressionAssert.Contains(error, "long");
	}

	public void RedactsAuthorizationValues()
	{
		const string raw = "nxm://baldursgate3/mods/42/files/99?key=secret&expires=2000000300&user_id=7";
		var redacted = NexusModManagerLinkParser.Redact(raw);

		RegressionAssert.Equal("nxm://baldursgate3/mods/42/files/99?authorization=redacted", redacted);
		RegressionAssert.False(redacted.Contains("secret", StringComparison.Ordinal));
		RegressionAssert.Equal("nxm://invalid", NexusModManagerLinkParser.Redact("not a uri?key=secret"));
	}
}
