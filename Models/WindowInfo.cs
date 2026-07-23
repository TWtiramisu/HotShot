using System;
using System.Windows.Media.Imaging;

namespace ScreenshotApp.Models
{
    public class WindowInfo
    {
        public IntPtr Hwnd { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public BitmapSource? Icon { get; set; }

        public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? $"[{ProcessName}]" : Title;

        public override string ToString()
        {
            return DisplayTitle;
        }
    }
}
