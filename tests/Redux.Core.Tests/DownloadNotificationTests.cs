using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DivinityModManager.Views;

namespace Redux.Core.Tests;

public sealed class DownloadNotificationTests
{
	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	public void NotificationsReuseTheirWindowWithoutTakingForeground()
	{
		var main = new Window();
		var opened = false;
		var notification = new ReduxDownloadNotification(main, () => { opened = true; return Task.CompletedTask; });
		try
		{
			var foreground = GetForegroundWindow();
			notification.Notify("first", "Download started: Example mod");
			var handle = new WindowInteropHelper(notification).Handle;
			RegressionAssert.True(handle != IntPtr.Zero);
			RegressionAssert.True(GetForegroundWindow() == foreground);
			notification.Notify("second", "Download queued: Another mod");
			RegressionAssert.True(new WindowInteropHelper(notification).Handle == handle);
			RegressionAssert.True(GetForegroundWindow() == foreground);
			RegressionAssert.True(AutomationProperties.GetName(notification).Contains("Download queued: Another mod"));
			RegressionAssert.True(!opened && !main.IsVisible && !notification.ShowInTaskbar);
			var output = Environment.GetEnvironmentVariable("REDUX_LAYOUT_PREVIEW");
			if (!String.IsNullOrWhiteSpace(output))
			{
				notification.UpdateLayout();
				var bitmap = new RenderTargetBitmap((int)notification.ActualWidth, (int)notification.ActualHeight, 96, 96, PixelFormats.Pbgra32);
				bitmap.Render((Visual)notification.Content);
				var encoder = new PngBitmapEncoder();
				encoder.Frames.Add(BitmapFrame.Create(bitmap));
				Directory.CreateDirectory(output);
				using var file = File.Create(Path.Combine(output, "nxm-notification.png"));
				encoder.Save(file);
			}
		}
		finally { notification.Close(); main.Close(); }
	}
}
