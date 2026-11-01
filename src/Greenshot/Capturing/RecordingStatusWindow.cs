using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Dapplo.Windows.User32;
using Dapplo.Windows.User32.Enums;

namespace Greenshot.Capturing
{
    internal sealed class RecordingStatusWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly TextBlock _text;
        private readonly RecordingProgress _progress;
        private readonly Func<TimeSpan> _limit;
        private readonly string _stopKey;
        private bool _finalizing;
        public RecordingStatusWindow(System.Drawing.Rectangle region, RecordingProgress progress, Func<TimeSpan> limit, string stopKey)
        {
            _progress = progress; _limit = limit; _stopKey = stopKey;
            Title = "Greenshot recording status";
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false; ShowActivated = false; Topmost = true; IsHitTestVisible = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            Background = new SolidColorBrush(Color.FromRgb(28, 28, 32));
            _text = new TextBlock { Foreground = Brushes.White, FontFamily = new FontFamily("Consolas"), FontSize = 13, Margin = new Thickness(12, 9, 12, 9) };
            Content = new Border { Child = _text, BorderBrush = Brushes.OrangeRed, BorderThickness = new Thickness(1) };
            SourceInitialized += (_, __) =>
            {
                var handle = new WindowInteropHelper(this).Handle;
                var style = (ExtendedWindowStyleFlags)User32Api.GetWindowLongWrapper(handle, WindowLongIndex.GWL_EXSTYLE);
                User32Api.SetWindowLongWrapper(handle, WindowLongIndex.GWL_EXSTYLE,
                    new IntPtr((uint)(style | ExtendedWindowStyleFlags.WS_EX_TRANSPARENT | ExtendedWindowStyleFlags.WS_EX_NOACTIVATE | ExtendedWindowStyleFlags.WS_EX_TOOLWINDOW)));
                if (!User32Api.SetWindowDisplayAffinity(handle, WindowDisplayAffinity.ExcludeFromCapture))
                    throw new InvalidOperationException("Windows could not exclude the recording status overlay from captures.");
            };
            ContentRendered += (_, __) =>
            {
                var work = System.Windows.Forms.Screen.FromRectangle(region).WorkingArea;
                var dpi = VisualTreeHelper.GetDpi(this);
                int width = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX), height = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);
                int x = Math.Max(work.Left, work.Right - width - 12), y = work.Top + 12;
                if (region.IntersectsWith(new System.Drawing.Rectangle(x, y, width, height))) y = Math.Max(work.Top, work.Bottom - height - 12);
                User32Api.SetWindowPos(new WindowInteropHelper(this).Handle, IntPtr.Zero, x, y, width, height, WindowPos.SWP_NOZORDER | WindowPos.SWP_NOACTIVATE);
            };
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _timer.Tick += (_, __) => Refresh();
            Closed += (_, __) => _timer.Stop();
            Refresh();
            _timer.Start();
        }
        public void Finalizing() { _finalizing = true; Refresh(); }
        private void Refresh()
        {
            var state = _progress.Snapshot();
            var remaining = _limit() - state.Elapsed;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            string status = _finalizing ? "FINALIZING OUTPUT" : state.CaptureStopped ? "SAVING FRAMES" : "RECORDING  (" + _stopKey + " to stop)";
            _text.Text = status + "\nElapsed " + Clock(state.Elapsed) + "  Left " + Clock(remaining) +
                "\nActual FPS " + state.Fps.ToString("F1", CultureInfo.InvariantCulture) +
                "\nFrames " + state.Captured.ToString("N0") + "  Saved " + state.Saved.ToString("N0");
        }
        internal static string Clock(TimeSpan value) => ((int)value.TotalHours).ToString("00") + ":" + value.Minutes.ToString("00") + ":" + value.Seconds.ToString("00") + "." + (value.Milliseconds / 100).ToString();
    }
}
