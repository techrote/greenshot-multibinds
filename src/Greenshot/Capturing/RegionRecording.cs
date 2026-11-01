using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Dapplo.Ini;
using Greenshot.Base.Core;
using Greenshot.Base.Interfaces;
using Greenshot.Base.Recipes.Sources;
using Greenshot.Base.Threading;
using Greenshot.Capturing.Views;
using log4net;

namespace Greenshot.Capturing
{
    public static class RegionRecording
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(RegionRecording));
        private static int _startKey = -1, _stopKey = -1;
        private static CancellationTokenSource _stop;
        private static CaptureWindow _selection;
        private static Task _completion = Task.CompletedTask;
        public static bool IsActive => _stop != null;
        public static string LastFolder { get; private set; }
        private static ICoreConfiguration Config => IniConfigRegistry.GetSection<ICoreConfiguration>();

        public static void RegisterHotkeys()
        {
            UnregisterHotkeys();
            _startKey = HotkeyManager.RegisterHotKey(Config.RecordingStartHotkey, () =>
                UiDispatcher.Current.InvokeAsync(Start).FireAndLog("Start region recording", Log));
            _stopKey = HotkeyManager.RegisterHotKey(Config.RecordingStopHotkey, () =>
                UiDispatcher.Current.InvokeAsync(Stop).FireAndLog("Stop region recording", Log));
            if (_startKey < 0 || _stopKey < 0) Log.Warn("A region recording shortcut could not be registered; use the tray menu.");
        }

        public static void UnregisterHotkeys()
        {
            if (_startKey >= 0) HotkeyManager.UnregisterHotKey(_startKey);
            if (_stopKey >= 0) HotkeyManager.UnregisterHotKey(_stopKey);
            _startKey = _stopKey = -1;
        }

        public static void Start()
        {
            Log.Info("Region recording start requested; active=" + IsActive);
            if (IsActive) return;
            _stop = new CancellationTokenSource();
            _completion = RunAsync(_stop);
            SimpleServiceProvider.Current.GetInstance<IGreenshotShell>(true)?.UpdateUi();
        }

        public static void Stop()
        {
            _stop?.Cancel();
            _selection?.Cancel();
        }

        public static async Task StopAsync()
        {
            Stop();
            await _completion;
        }

        public static void OpenLastFolder()
        {
            if (LastFolder != null) Process.Start(new ProcessStartInfo(LastFolder) { UseShellExecute = true });
        }

        private static async Task RunAsync(CancellationTokenSource stop)
        {
            try
            {
                if (!LiveScreenCaptureSource.IsSupported) throw new NotSupportedException("Live region recording needs Windows 10 2004 or newer.");
                using var capture = new Capture();
                capture.CaptureDetails.CaptureMode = CaptureMode.Region;
                capture.CaptureDetails.AddMetaData("recording_live_only", "true");
                _selection = new CaptureWindow(capture.ScreenBounds) { Title = "Select recording region" };
                _selection.Prepare();
                _selection.SetCapture(capture, null, "Region");
                Log.Info("Live recording selector shown");
                bool? selected = _selection.WaitUntilClosed();
                var area = _selection.CaptureRectangle;
                _selection = null;
                if (selected != true || stop.IsCancellationRequested || area.Width <= 0 || area.Height <= 0) return;
                var region = new Rectangle(area.X + capture.ScreenBounds.X, area.Y + capture.ScreenBounds.Y, area.Width, area.Height);
                string root = Config.RecordingOutputDirectory;
                if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Greenshot Recordings");
                LastFolder = Path.Combine(root, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
                // Give DWM a chance to remove the selector before the first frame.
                await Task.Delay(75, stop.Token);
                System.Media.SystemSounds.Asterisk.Play();
                int frames = await PngFrameRecorder.RecordAsync(region, LastFolder, Config.RecordingFramesPerSecond, stop.Token);
                Log.Info("Region recording saved " + frames + " PNG frames to " + LastFolder);
                OpenLastFolder();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log.Error("Region recording failed", ex);
                MessageBox.Show(ex.Message, "Region recording", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _selection = null;
                _stop = null;
                stop.Dispose();
                SimpleServiceProvider.Current.GetInstance<IGreenshotShell>(true)?.UpdateUi();
            }
        }
    }
}
