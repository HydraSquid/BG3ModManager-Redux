using System.Runtime.InteropServices;

namespace DivinityModManager.Util;
public static partial class NativeLibraryHelper
{
	[LibraryImport("kernel32", EntryPoint = "LoadLibraryW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
	public static partial nint LoadLibrary(string lpFileName);
}
