using System;

namespace Redux.Installer.Tests;

internal static class RegressionAssert
{
	public static void True(bool value, string message = "Expected true.")
	{
		if (!value) throw new InvalidOperationException(message);
	}

	public static void False(bool value, string message = "Expected false.") => True(!value, message);

	public static void Equal<T>(T expected, T actual)
	{
		if (!Equals(expected, actual))
			throw new InvalidOperationException("Expected '" + expected + "' but got '" + actual + "'.");
	}

	public static void Throws<TException>(Action action) where TException : Exception
	{
		try { action(); }
		catch (TException) { return; }
		throw new InvalidOperationException("Expected " + typeof(TException).Name + ".");
	}
}
