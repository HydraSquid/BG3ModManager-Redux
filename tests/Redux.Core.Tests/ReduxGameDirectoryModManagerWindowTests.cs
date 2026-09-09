using DivinityModManager.Views;

using System;
using System.Reflection;

namespace Redux.Core.Tests;

public sealed class ReduxGameDirectoryModManagerWindowTests
{
	public void NativeGameVersionParsesTheBg3ProductBuildAndRejectsInvalidText()
	{
		var parser = typeof(ReduxGameDirectoryModManagerWindow).GetMethod("ParseNativeGameVersion",
			BindingFlags.Static | BindingFlags.NonPublic)!;

		var supported = parser.Invoke(null, ["4.1.1.7398727"]) as Version;
		var invalid = parser.Invoke(null, ["not a BG3 build"]);

		RegressionAssert.Equal(new Version(4, 1, 1, 7398727), supported);
		RegressionAssert.Equal(null, invalid);
	}
}
