using System;
using System.IO;
using System.Reflection;
using System.Windows;

using Newtonsoft.Json.Linq;

namespace ReduxInstaller;

public partial class App : Application
{
	static App()
	{
		AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbeddedDependency;
	}

	private static Assembly? ResolveEmbeddedDependency(object sender, ResolveEventArgs args)
	{
		var requested = new AssemblyName(args.Name).Name;
		if (!String.Equals(requested, "Newtonsoft.Json", StringComparison.OrdinalIgnoreCase)) return null;
		using var stream = Assembly.GetExecutingAssembly()
			.GetManifestResourceStream("ReduxInstaller.Dependencies.Newtonsoft.Json.dll");
		if (stream == null) return null;
		using var buffer = new MemoryStream();
		stream.CopyTo(buffer);
		return Assembly.Load(buffer.ToArray());
	}

	protected override void OnStartup(StartupEventArgs e)
	{
		if (Array.Exists(e.Args, argument => String.Equals(argument, "--self-test", StringComparison.Ordinal)))
		{
			try
			{
				_ = JObject.Parse("{\"setup\":true}");
				if (GetType().Assembly.GetManifestResourceStream("ReduxInstaller.Dependencies.Newtonsoft.Json.dll") == null)
					throw new InvalidDataException("The embedded JSON dependency is missing.");
				Shutdown(0);
			}
			catch
			{
				Shutdown(1);
			}
			return;
		}
		base.OnStartup(e);
	}
}
