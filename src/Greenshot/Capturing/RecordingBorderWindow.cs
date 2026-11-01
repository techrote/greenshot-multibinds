using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Dapplo.Windows.Common.Structs;
using Dapplo.Windows.User32;
using Dapplo.Windows.User32.Enums;

namespace Greenshot.Capturing
{
    internal sealed class RecordingBorderWindow : Window
    {
        public RecordingBorderWindow(NativeRect region)
        {
            Title = "Recording region border";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            IsHitTestVisible = false;
            Content = new Border { BorderBrush = Brushes.OrangeRed, BorderThickness = new Thickness(2) };
            SourceInitialized += (_, __) =>
            {
                var handle = new WindowInteropHelper(this).Handle;
                var style = (ExtendedWindowStyleFlags)User32Api.GetWindowLongWrapper(handle, WindowLongIndex.GWL_EXSTYLE);
                User32Api.SetWindowLongWrapper(handle, WindowLongIndex.GWL_EXSTYLE,
                    new IntPtr((uint)(style | ExtendedWindowStyleFlags.WS_EX_TRANSPARENT | ExtendedWindowStyleFlags.WS_EX_NOACTIVATE | ExtendedWindowStyleFlags.WS_EX_TOOLWINDOW)));
                if (!User32Api.SetWindowDisplayAffinity(handle, WindowDisplayAffinity.ExcludeFromCapture))
                    throw new InvalidOperationException("Windows could not exclude the recording border from captured frames.");
            };
            ContentRendered += (_, __) =>
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                ((Border)Content).BorderThickness = new Thickness(2 / dpi.DpiScaleX, 2 / dpi.DpiScaleY, 2 / dpi.DpiScaleX, 2 / dpi.DpiScaleY);
                User32Api.SetWindowPos(new WindowInteropHelper(this).Handle, IntPtr.Zero, region.X - 2, region.Y - 2, region.Width + 4, region.Height + 4,
                    WindowPos.SWP_NOZORDER | WindowPos.SWP_NOACTIVATE);
            };
            Width = region.Width + 4;
            Height = region.Height + 4;
            Left = region.X - 2;
            Top = region.Y - 2;
        }
    }
}
