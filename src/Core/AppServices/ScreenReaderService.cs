using CrossSpeak;

using DivinityModManager.Util;

using DavyKager;

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DivinityModManager
{
	public interface IScreenReaderService
	{
		bool IsScreenReaderActive();
		void Output(string text, bool interrupt = false);
		void Speak(string text, bool interrupt = false);
		bool TrySpeak(string text, bool interrupt = false);
		void Close();
		void Silence();
	}
}

namespace DivinityModManager.AppServices
{
	public class ScreenReaderService : IScreenReaderService
	{
		private static readonly string[] _optionalDlls = ["nvdaControllerClient64.dll", "SAAPI64.dll"];
		private const string TolkDll = "Tolk.dll";
		private static bool _loadedDlls = false;
		private readonly object _initializationLock = new();
		private bool _initializationUnavailable;
		private bool _hasDetectionResult;
		private bool _lastDetectionResult;
		private long _lastDetectionTick;
		private const long DetectionCacheMilliseconds = 15000;

		public bool IsScreenReaderActive()
		{
			var now = Environment.TickCount64;
			lock (_initializationLock)
			{
				if (_hasDetectionResult && now - _lastDetectionTick < DetectionCacheMilliseconds)
					return _lastDetectionResult;
			}
			if (!EnsureInit(false))
			{
				CacheDetectionResult(false, now);
				return false;
			}
			try
			{
				var detected = !String.IsNullOrWhiteSpace(Tolk.DetectScreenReader());
				CacheDetectionResult(detected, now);
				return detected;
			}
			catch (Exception ex)
			{
				DisableAfterInteropFailure(ex);
				return false;
			}
		}

		public void Close()
		{
			try
			{
				if (Tolk.IsLoaded()) Tolk.Unload();
			}
			catch (Exception ex) { DisableAfterInteropFailure(ex); }
		}

		public void Silence()
		{
			try
			{
				if (Tolk.IsLoaded()) Tolk.Silence();
			}
			catch (Exception ex) { DisableAfterInteropFailure(ex); }
		}

		private bool EnsureInit(bool trySAPI = false)
		{
			if (_initializationUnavailable) return false;
			lock (_initializationLock)
			{
				if (_initializationUnavailable) return false;
				try
				{
					if (!_loadedDlls)
					{
						var libPath = Path.Combine(DivinityApp.GetAppDirectory(), "_Lib");
						foreach (var dll in _optionalDlls)
						{
							var filePath = Path.Combine(libPath, dll);
							if (File.Exists(filePath)) NativeLibraryHelper.LoadLibrary(filePath);
						}

						var tolkPath = Path.Combine(libPath, TolkDll);
						if (!File.Exists(tolkPath))
							throw new FileNotFoundException("The bundled screen-reader bridge is missing.", tolkPath);
						if (NativeLibraryHelper.LoadLibrary(tolkPath) == IntPtr.Zero)
							throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not load the bundled screen-reader bridge.");
						_loadedDlls = true;
					}

					if (!Tolk.IsLoaded())
					{
						Tolk.Load();
					}
					if (trySAPI && Tolk.IsLoaded() && !Tolk.HasSpeech())
						Tolk.TrySAPI(true);
					return Tolk.IsLoaded();
				}
				catch (Exception ex)
				{
					DisableAfterInteropFailure(ex);
					return false;
				}
			}
		}

		public void Output(string text, bool interrupt = true) => TrySpeak(text, interrupt);

		public bool TrySpeak(string text, bool interrupt = true)
		{
			if (String.IsNullOrWhiteSpace(text) || !EnsureInit(true)) return false;
			try
			{
				var delivered = Tolk.Output(text, interrupt);
				if (!delivered)
					DivinityApp.Log("Screen-reader output was requested, but the active screen reader or SAPI did not accept it.");
				return delivered;
			}
			catch (Exception ex)
			{
				DisableAfterInteropFailure(ex);
				return false;
			}
		}

		public void Speak(string text, bool interrupt = true) => TrySpeak(text, interrupt);

		private void CacheDetectionResult(bool detected, long tick)
		{
			lock (_initializationLock)
			{
				_hasDetectionResult = true;
				_lastDetectionResult = detected;
				_lastDetectionTick = tick;
			}
		}

		private void DisableAfterInteropFailure(Exception ex)
		{
			if (_initializationUnavailable) return;
			_initializationUnavailable = true;
			_hasDetectionResult = true;
			_lastDetectionResult = false;
			_lastDetectionTick = Environment.TickCount64;
			DivinityApp.Log($"Screen reader integration was disabled after an interop failure:\n{ex}");
		}
	}
}
