using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Dapplo.Ini;
using Dapplo.Windows.Common.Structs;
using Dapplo.Windows.User32;
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
        private static readonly List<int> Keys = new List<int>();
        private static CancellationTokenSource _stop;
        private static CaptureWindow _selection;
        private static RecordingBorderWindow _border;
        private static Task _completion = Task.CompletedTask;
        private static volatile bool _timerEnabled;
        public static bool IsActive => _stop != null;
        public static string LastFolder { get; private set; }
        public static NativeRect SelectedRegion => Config.RecordingRegion;
        private static ICoreConfiguration Config => IniConfigRegistry.GetSection<ICoreConfiguration>();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int length);

        public static void RegisterHotkeys()
        {
            UnregisterHotkeys();
            var ui = UiDispatcher.Current;
            Register(Config.RecordingStartHotkey, Toggle);
            Register(Config.RecordingSelectHotkey, SelectRegion);
            Register(Config.RecordingBorderHotkey, ToggleBorder);
            Register(Config.RecordingTimerHotkey, ToggleTimer);
            RefreshPreferences();
            void Register(string text, Action action)
            {
                var sequence = HotkeySequence.Parse(text);
                if (sequence.IsEmpty || !sequence.Validate(out _) || HotkeyManager.IsRegistered(sequence))
                {
                    Log.Warn("Recording shortcut unavailable: " + text + ". Use the tray menu.");
                    return;
                }
                Keys.Add(HotkeyManager.RegisterHotKey(sequence, () => ui.InvokeAsync(action).FireAndLog("Recording shortcut " + text, Log)));
            }
        }

        public static void UnregisterHotkeys()
        {
            foreach (int key in Keys) HotkeyManager.UnregisterHotKey(key);
            Keys.Clear();
        }

        public static void RefreshPreferences()
        {
            _timerEnabled = Config.RecordingTimeLimitEnabled;
            UpdateBorder();
            SimpleServiceProvider.Current.GetInstance<IGreenshotShell>(true)?.UpdateUi();
        }

        public static void Toggle() { if (IsActive) Stop(); else Start(); }
        public static void ToggleTimer()
        {
            if (Config.IsConstant(nameof(Config.RecordingTimeLimitEnabled))) return;
            Config.RecordingTimeLimitEnabled = !Config.RecordingTimeLimitEnabled;
            _timerEnabled = Config.RecordingTimeLimitEnabled;
            IniConfigRegistry.Get()?.Save();
            System.Media.SystemSounds.Asterisk.Play();
            SimpleServiceProvider.Current.GetInstance<INotificationService>(true)?.ShowInfoMessage(
                "Optional recording timer " + (_timerEnabled ? "ON: " + Config.RecordingTimeLimit : "OFF") +
                ". Universal limit: " + RecordingNames.HardLimit(Config.RecordingMaximumMinutes).TotalMinutes + " minutes.");
            SimpleServiceProvider.Current.GetInstance<IGreenshotShell>(true)?.UpdateUi();
        }
        public static void ToggleBorder()
        {
            if (Config.IsConstant(nameof(Config.RecordingShowBorder))) return;
            Config.RecordingShowBorder = !Config.RecordingShowBorder;
            UpdateBorder();
            IniConfigRegistry.Get()?.Save();
        }
        private static void UpdateBorder()
        {
            _border?.Close();
            _border = null;
            if (!Config.RecordingShowBorder || _selection != null || Config.RecordingRegion.Width <= 0 || Config.RecordingRegion.Height <= 0) return;
            try { _border = new RecordingBorderWindow(Config.RecordingRegion); _border.Show(); }
            catch (Exception ex) { _border?.Close(); _border = null; Log.Warn("Recording border hidden: " + ex.Message); }
        }

        public static void SelectRegion()
        {
            if (IsActive || _selection != null) return;
            try
            {
                if (!LiveScreenCaptureSource.IsSupported) throw new NotSupportedException("Live region selection requires Windows 10 2004 or newer.");
                var title = new StringBuilder(512);
                GetWindowText(User32Api.GetForegroundWindow(), title, title.Capacity);
                _border?.Hide();
                using var capture = new Capture();
                capture.CaptureDetails.CaptureMode = CaptureMode.Region;
                capture.CaptureDetails.AddMetaData("recording_live_only", "true");
                _selection = new CaptureWindow(capture.ScreenBounds) { Title = "Select recording region" };
                _selection.Prepare();
                _selection.SetCapture(capture, null, "Region");
                if (_selection.WaitUntilClosed() == true)
                {
                    var area = _selection.CaptureRectangle;
                    Config.RecordingRegion = new NativeRect(area.X + capture.ScreenBounds.X, area.Y + capture.ScreenBounds.Y, area.Width, area.Height);
                    Config.RecordingWindowTitle = title.Length > 0 ? title.ToString() : "Desktop";
                    IniConfigRegistry.Get()?.Save();
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Recording region", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { _selection = null; UpdateBorder(); }
        }

        public static void Start()
        {
            if (IsActive || _selection != null) return;
            if (Config.RecordingRegion.Width <= 0 || Config.RecordingRegion.Height <= 0)
            {
                MessageBox.Show("Press " + Config.RecordingSelectHotkey + " to select a recording region first.", "Recording region");
                return;
            }
            _stop = new CancellationTokenSource();
            _completion = RunAsync(_stop);
            RefreshPreferences();
        }
        public static void Stop() { _stop?.Cancel(); _selection?.Cancel(); }
        public static async Task StopAsync()
        {
            Stop();
            await _completion;
            _border?.Close();
            _border = null;
        }
        public static void OpenLastFolder()
        {
            if (LastFolder != null) Process.Start(new ProcessStartInfo(LastFolder) { UseShellExecute = true });
        }

        private static async Task RunAsync(CancellationTokenSource stop)
        {
            await Task.Yield(); // Start stores completion before anything can re-enter the UI.
            string working = null;
            RecordingStatusWindow status = null;
            try
            {
                var area = Config.RecordingRegion;
                var region = new Rectangle(area.X, area.Y, area.Width, area.Height);
                int fps = Config.RecordingFramesPerSecond;
                if (fps < 1 || fps > 120) throw new FormatException("Target FPS must be between 1 and 120.");
                TimeSpan hard = RecordingNames.HardLimit(Config.RecordingMaximumMinutes);
                TimeSpan optional = RecordingNames.ParseLimit(Config.RecordingTimeLimit);
                bool folder = Config.RecordingCreateFolder, zip = Config.RecordingCreateZip;
                string container = Config.RecordingContainerTemplate, frameTemplate = Config.RecordingFrameTemplate;
                string root = Config.RecordingOutputDirectory;
                if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Greenshot Recordings");
                root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(root));
                Directory.CreateDirectory(root);
                var names = new RecordingNames { Started = DateTime.Now, Fps = fps, Window = Config.RecordingWindowTitle, Batch = checked(Config.RecordingBatchNumber + 1) };
                names.Render(container);
                names.Render(frameTemplate, 1);
                Config.RecordingBatchNumber = names.Batch;
                IniConfigRegistry.Get()?.Save();
                working = Path.Combine(root, ".recording-" + Guid.NewGuid().ToString("N"));
                LastFolder = working;
                System.Media.SystemSounds.Asterisk.Play();
                var progress = new RecordingProgress();
                TimeSpan CurrentLimit() => _timerEnabled && optional < hard ? optional : hard;
                try { status = new RecordingStatusWindow(region, progress, CurrentLimit, Config.RecordingStartHotkey); status.Show(); }
                catch (Exception ex) { status?.Close(); status = null; Log.Warn("Recording status overlay hidden: " + ex.Message); }
                int frames = await PngFrameRecorder.RecordAsync(region, working, fps, stop.Token, names: names, frameTemplate: frameTemplate,
                    hardLimit: hard, optionalLimit: () => _timerEnabled ? (TimeSpan?)optional : null, progress: progress);
                status?.Finalizing();
                // Final names can now include the actual duration and saved frame count.
                // R10 exception: ZIP/file finalization owns a dedicated I/O worker; capture and the UI remain responsive.
#pragma warning disable RS0030
                LastFolder = await Task.Factory.StartNew(() => RecordingOutput.Finish(working, root, names.Render(container), folder, zip, names.Batch),
                    CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
#pragma warning restore RS0030
                Log.Info("Recording finished: " + frames + " frames; " + LastFolder);
                if (Config.RecordingOpenCompletedOutput) OpenLastFolder();
            }
            catch (Exception ex)
            {
                Log.Error("Recording failed", ex);
                MessageBox.Show(ex.Message + (working == null ? "" : "\nCaptured frames retained at: " + working), "Region recording", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                status?.Close();
                _stop = null;
                stop.Dispose();
                RefreshPreferences();
            }
        }
    }
}
