using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Redux.Core.Tests;

public sealed class ReleaseVersionContractTests
{
	public void ApplicationAndBinaryVersionsIdentifyTheSameAlphaRelease()
	{
		var displayMatch = Regex.Match(
			DivinityModManager.DivinityApp.REDUX_DISPLAY_VERSION,
			@"^0\.1\.0-alpha\.(?<release>[1-9][0-9]*)(?:\.(?<hotfix>[1-9][0-9]*))?$",
			RegexOptions.CultureInvariant);
		RegressionAssert.True(displayMatch.Success);
		var releaseNumber = Int32.Parse(displayMatch.Groups["release"].Value);
		var hotfixNumber = displayMatch.Groups["hotfix"].Success
			? Int32.Parse(displayMatch.Groups["hotfix"].Value)
			: 0;
		var expectedInternal = Version.Parse(DivinityModManager.DivinityApp.REDUX_INTERNAL_VERSION);
		var validLegacyBase = releaseNumber <= 16
			&& hotfixNumber == 0
			&& expectedInternal == new Version(0, 1, 0, releaseNumber);
		var validHotfixAware = expectedInternal == new Version(0, 1, releaseNumber, hotfixNumber);
		RegressionAssert.True(validLegacyBase || validHotfixAware);

		var guiAssembly = typeof(DivinityModManager.App).Assembly;
		RegressionAssert.Equal("Redux", guiAssembly.GetName().Name);
		RegressionAssert.Equal("Redux.dll", Path.GetFileName(guiAssembly.Location));
		RegressionAssert.Equal(expectedInternal, guiAssembly.GetName().Version);
		RegressionAssert.Equal(
			DivinityModManager.DivinityApp.REDUX_DISPLAY_VERSION,
			guiAssembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().Single().InformationalVersion);
		RegressionAssert.Equal(
			expectedInternal.ToString(),
			FileVersionInfo.GetVersionInfo(guiAssembly.Location).FileVersion);
	}
}
