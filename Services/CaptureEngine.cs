using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ScreenshotApp.Native;

namespace ScreenshotApp.Services
{
    public static class CaptureEngine
    {
        public static async Task<string?> CaptureWindowAsync(IntPtr hWnd, string windowTitle, string outputDirectory)
        {
            if (hWnd == IntPtr.Zero || !NativeMethods.IsWindowVisible(hWnd))
            {
                return null;
            }

            return await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(outputDirectory))
                    {
                        Directory.CreateDirectory(outputDirectory);
                    }

                    // Get window bounds using DWM Extended Frame Bounds (excludes invisible shadows)
                    NativeMethods.RECT rect;
                    int dwmResult = NativeMethods.DwmGetWindowAttribute(
                        hWnd,
                        NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                        out rect,
                        (uint)Marshal.SizeOf<NativeMethods.RECT>());

                    if (dwmResult != 0 || rect.Width <= 0 || rect.Height <= 0)
                    {
                        NativeMethods.GetWindowRect(hWnd, out rect);
                    }

                    int width = rect.Width;
                    int height = rect.Height;

                    if (width <= 0 || height <= 0)
                    {
                        return null;
                    }

                    using Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        IntPtr hdcDest = g.GetHdc();

                        try
                        {
                            // PRIMARY: Capture from Screen DC (GetDC(IntPtr.Zero)) using target window's rect.
                            // This captures live DWM compositor output for borderless fullscreen games in real-time,
                            // avoiding the static frame freeze issue caused by GDI PrintWindow.
                            IntPtr hdcScreen = NativeMethods.GetDC(IntPtr.Zero);
                            bool capturedFromScreen = false;

                            if (hdcScreen != IntPtr.Zero)
                            {
                                try
                                {
                                    capturedFromScreen = NativeMethods.BitBlt(
                                        hdcDest, 0, 0, width, height,
                                        hdcScreen, rect.Left, rect.Top,
                                        NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT);
                                }
                                finally
                                {
                                    NativeMethods.ReleaseDC(IntPtr.Zero, hdcScreen);
                                }
                            }

                            // FALLBACK: If screen DC failed, fallback to GetWindowDC
                            if (!capturedFromScreen)
                            {
                                IntPtr hdcWindow = NativeMethods.GetWindowDC(hWnd);
                                try
                                {
                                    NativeMethods.BitBlt(
                                        hdcDest, 0, 0, width, height,
                                        hdcWindow, 0, 0,
                                        NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT);
                                }
                                finally
                                {
                                    NativeMethods.ReleaseDC(hWnd, hdcWindow);
                                }
                            }
                        }
                        finally
                        {
                            g.ReleaseHdc(hdcDest);
                        }
                    }

                    // Format filename: <window_name>_<timestamp_YYYYMMDD.HHmmSS.fff>.png
                    string safeWindowName = SanitizeFileName(windowTitle);
                    if (string.IsNullOrWhiteSpace(safeWindowName))
                    {
                        safeWindowName = "Window";
                    }

                    string timestamp = DateTime.Now.ToString("yyyyMMdd.HHmmssfff");
                    string fileName = $"{safeWindowName}_{timestamp}.png";
                    string fullPath = Path.Combine(outputDirectory, fileName);

                    bitmap.Save(fullPath, ImageFormat.Png);
                    return fullPath;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Capture Error: {ex.Message}");
                    return null;
                }
            });
        }

        private static string SanitizeFileName(string name)
        {
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"[{0}]", invalidChars);
            string sanitized = Regex.Replace(name, invalidRegStr, "_");
            return sanitized.Replace(" ", "").Trim();
        }
    }
}
