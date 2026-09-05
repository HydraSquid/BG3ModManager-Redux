using DivinityModManager.Util.ScreenReader;
using DivinityModManager.AppServices;
using DivinityModManager.Util;

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DivinityModManager;

internal class Program
{
	private static string _libDirectory;
	private static int _fatalStartupExceptionState;

	private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
	{
		var assyName = new AssemblyName(args.Name);

		var newPath = Path.Combine(_libDirectory, assyName.Name);
		if (!newPath.EndsWith(".dll"))
		{
			newPath += ".dll";
		}

		if (File.Exists(newPath))
		{
			var assy = Assembly.LoadFile(newPath);
			return assy;
		}
		return null;
	}

	private static void OnAppExit(object sender, EventArgs e)
	{
		//CrossSpeakManager: Make sure to always call the Close() method before your application closes.
		Services.ScreenReader?.Close();
	}

	/// <summary>
	/// Safety net for exceptions thrown before MainWindow wires up its own (richer) handlers -
	/// e.g. during the App() constructor, OnStartup, or MainWindow's InitializeComponent().
	/// MainWindow unsubscribes these once it takes over, so there's no double-handling.
	/// </summary>
	internal static void OnEarlyUnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		ReportFatalStartupException(e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));
	}

	internal static void OnEarlyDispatcherException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
	{
		e.Handled = true;
		ReportFatalStartupException(e.Exception);
	}

	private static void ReportFatalStartupException(Exception ex)
	{
		// Dispatcher rendering failures can be raised repeatedly while the first
		// modal error is open. Never allow one fatal startup failure to create an
		// unbounded cascade of MessageBox windows.
		if (Interlocked.CompareExchange(ref _fatalStartupExceptionState, 1, 0) != 0)
		{
			Environment.Exit(1);
			return;
		}

		try
		{
			var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_Logs");
			Directory.CreateDirectory(logDirectory);
			File.AppendAllText(Path.Combine(logDirectory, "startup_crash.log"), $"{DateTime.Now}: {ex}\n\n");
		}
		catch { }

		System.Windows.MessageBox.Show($"A fatal error occurred while starting up and the application must close.\n\n{ex.Message}",
			"Startup Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);

		Environment.Exit(1);
	}

	[STAThread]
	static void Main(string[] args)
	{
		AppDomain.CurrentDomain.UnhandledException += OnEarlyUnhandledException;

		_libDirectory = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "_Lib");
		AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
		RunApplication(args);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void RunApplication(string[] args)
	{
		var executablePath = Environment.ProcessPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BG3ModManager.exe");
		string initialNxmLink = null;
		if (args.Length > 0)
		{
			if (args.Length != 2 || !args[0].Equals("--nxm", StringComparison.OrdinalIgnoreCase) ||
				!NexusModManagerLinkParser.TryReadGame(args[1], out var game))
			{
				System.Windows.MessageBox.Show("Redux received an invalid Nexus Mod Manager link.", "Nexus Link Error",
					System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
				return;
			}

			initialNxmLink = args[1];
			if (!game.Equals(DivinityApp.NEXUSMODS_GAME_DOMAIN, StringComparison.OrdinalIgnoreCase))
			{
				if (!NxmPreviousHandlerForwarder.TryForward(new NxmRegistryStore(), initialNxmLink, executablePath, out var error))
				{
					System.Windows.MessageBox.Show(error, "Unsupported Nexus Game",
						System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
				}
				return;
			}
		}

		using var activationCoordinator = NxmActivationCoordinator.CreateForExecutable(executablePath);
		if (initialNxmLink != null && activationCoordinator.TryForwardAsync(initialNxmLink, TimeSpan.FromSeconds(3)).GetAwaiter().GetResult())
			return;

		var pendingActivations = new ConcurrentQueue<string>();
		if (initialNxmLink != null) pendingActivations.Enqueue(initialNxmLink);
		App app = null;
		var listening = activationCoordinator.StartListening(value =>
		{
			pendingActivations.Enqueue(value);
			app?.NotifyNxmActivationAvailable();
			return Task.CompletedTask;
		});
		if (!listening && initialNxmLink != null)
		{
			if (activationCoordinator.TryForwardAsync(initialNxmLink, TimeSpan.FromSeconds(3)).GetAwaiter().GetResult()) return;
			System.Windows.MessageBox.Show("Redux could not deliver the Nexus link to the running instance.",
				"Nexus Link Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
			return;
		}

		Util.SmoothLogicalScrollBehavior.Initialize();
		app = new App(pendingActivations);
		app.DispatcherUnhandledException += OnEarlyDispatcherException;
		app.Exit += OnAppExit;
		app.InitializeComponent();
		app.Run();
	}
}
