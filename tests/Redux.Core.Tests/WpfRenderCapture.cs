using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using DivinityModManager;
using DivinityModManager.AppServices;

using Splat;

namespace Redux.Core.Tests;

internal static class WpfRenderCapture
{
	private const string ScreenshotsDirectoryVariable = "REDUX_TABLE_SCREENSHOTS";

	public static ResourceDictionary CreateReduxApplicationResources()
	{
		var resources = new ResourceDictionary();
		foreach (var uri in new[]
		{
			"/BG3ModManager;component/Themes/Typography.xaml",
			"/BG3ModManager;component/Themes/Light.xaml",
			"/BG3ModManager;component/Themes/Dark.xaml",
			"/AdonisUI.ClassicTheme;component/Resources.xaml"
		})
			resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) });
		return resources;
	}

	public static ResourceDictionary CreateReduxWindowResources()
	{
		var resources = new ResourceDictionary();
		resources.MergedDictionaries.Add(new ResourceDictionary
		{
			Source = new Uri("/BG3ModManager;component/Themes/MainResourceDictionary.xaml", UriKind.Relative)
		});
		return resources;
	}

	public static IDisposable RegisterNoOpFileWatcherService()
	{
		var previous = Services.Get<IFileWatcherService>();
		Locator.CurrentMutable.RegisterConstant<IFileWatcherService>(new NoOpFileWatcherService());
		return Disposable.Create(() =>
		{
			Locator.CurrentMutable.UnregisterCurrent<IFileWatcherService>();
			if (previous != null && !ReferenceEquals(Services.Get<IFileWatcherService>(), previous))
				Locator.CurrentMutable.RegisterConstant(previous);
		});
	}

	public static void CaptureIfRequested(FrameworkElement element, string name)
	{
		var directory = Environment.GetEnvironmentVariable(ScreenshotsDirectoryVariable);
		if (String.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;

		element.UpdateLayout();
		if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
			throw new InvalidOperationException($"Cannot capture '{name}' because its rendered bounds are empty.");

		var dpi = VisualTreeHelper.GetDpi(element);
		var bitmap = new RenderTargetBitmap(
			Math.Max(1, (int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX)),
			Math.Max(1, (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY)),
			96 * dpi.DpiScaleX,
			96 * dpi.DpiScaleY,
			PixelFormats.Pbgra32);
		bitmap.Render(element);

		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));
		using var stream = new FileStream(Path.Combine(directory, $"{name}.png"), FileMode.Create,
			FileAccess.Write, FileShare.Read);
		encoder.Save(stream);
	}

	public static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
	{
		for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
		{
			var child = VisualTreeHelper.GetChild(root, index);
			if (child is T match) yield return match;
			foreach (var nested in Descendants<T>(child)) yield return nested;
		}
	}

	public static void AssertFullyWithin(FrameworkElement element, FrameworkElement container)
	{
		const double tolerance = 0.5;
		if (!element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
			throw new InvalidOperationException($"{Describe(element)} must be visible with non-empty bounds inside {Describe(container)}.");
		var origin = element.TransformToAncestor(container).Transform(new Point());
		var right = origin.X + element.ActualWidth;
		var bottom = origin.Y + element.ActualHeight;
		if (origin.X < -tolerance || origin.Y < -tolerance || right > container.ActualWidth + tolerance || bottom > container.ActualHeight + tolerance)
			throw new InvalidOperationException($"{Describe(element)} bounds x={origin.X:0.##}, y={origin.Y:0.##}, " +
				$"width={element.ActualWidth:0.##}, height={element.ActualHeight:0.##}, right={right:0.##}, bottom={bottom:0.##} " +
				$"exceed {Describe(container)} width={container.ActualWidth:0.##}, height={container.ActualHeight:0.##}.");
	}

	private static string Describe(FrameworkElement element) =>
		String.IsNullOrWhiteSpace(element.Name) ? element.GetType().Name : element.Name;

	private sealed class NoOpFileWatcherService : IFileWatcherService
	{
		public IFileWatcherWrapper WatchDirectory(string directory, string filter) => new NoOpFileWatcherWrapper(directory);
	}

	private sealed class NoOpFileWatcherWrapper : IFileWatcherWrapper
	{
		public string DefaultDirectory => String.Empty;
		public string DirectoryPath { get; private set; }
		public bool IsEnabled => false;
		public IObservable<FileSystemEventArgs> FileChanged { get; } = Observable.Never<FileSystemEventArgs>();
		public IObservable<FileSystemEventArgs> FileCreated { get; } = Observable.Never<FileSystemEventArgs>();
		public IObservable<FileSystemEventArgs> FileDeleted { get; } = Observable.Never<FileSystemEventArgs>();

		public NoOpFileWatcherWrapper(string directory)
		{
			DirectoryPath = directory ?? String.Empty;
		}

		public void SetDirectory(string path) => DirectoryPath = path ?? DefaultDirectory;

		public void PauseWatcher(bool paused, double pauseFor = -1) { }
	}
}
