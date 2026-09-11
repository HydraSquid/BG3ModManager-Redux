using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace DivinityModManager.Converters;

/// <summary>
/// Decodes a local image completely before closing its stream. WPF's implicit
/// string-to-image conversion uses on-demand decoding, which can keep save-game
/// thumbnails open and prevent their containing folders from being recycled.
/// </summary>
public sealed class FilePathToBitmapImageConverter : IValueConverter
{
	private static readonly object CacheLock = new();
	private static readonly Dictionary<string, CachedBitmap> ImageCache = new(StringComparer.OrdinalIgnoreCase);

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is not string path || String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

		try
		{
			var fullPath = Path.GetFullPath(path);
			var file = new FileInfo(fullPath);
			lock (CacheLock)
			{
				if (ImageCache.TryGetValue(fullPath, out var cached)
					&& cached.Length == file.Length
					&& cached.ModifiedUtc == file.LastWriteTimeUtc)
					return cached.Bitmap;
			}

			BitmapSource bitmap;
			using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read,
				FileShare.ReadWrite | FileShare.Delete))
			{
				bitmap = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
				if (bitmap.CanFreeze) bitmap.Freeze();
			}

			lock (CacheLock)
				ImageCache[fullPath] = new CachedBitmap(file.Length, file.LastWriteTimeUtc, bitmap);
			return bitmap;
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Failed to load local image '{path}':\n{ex}");
			return null;
		}
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;

	public static void EvictTree(string folderPath)
	{
		if (String.IsNullOrWhiteSpace(folderPath)) return;
		var prefix = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
			+ Path.DirectorySeparatorChar;
		lock (CacheLock)
		{
			foreach (var path in ImageCache.Keys.Where(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray())
				ImageCache.Remove(path);
		}
	}

	private sealed record CachedBitmap(long Length, DateTime ModifiedUtc, BitmapSource Bitmap);
}
