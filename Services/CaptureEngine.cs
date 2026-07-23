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
            if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd))
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
                            bool captured = false;
                            IntPtr foregroundHwnd = NativeMethods.GetForegroundWindow();
                            bool isForeground = (hWnd == foregroundHwnd);

                            // Primary strategy for Foreground / Active game windows:
                            // GetDC(IntPtr.Zero) screen BitBlt directly captures GPU/DirectX compositor output
                            if (isForeground)
                            {
                                IntPtr hdcScreen = NativeMethods.GetDC(IntPtr.Zero);
                                if (hdcScreen != IntPtr.Zero)
                                {
                                    try
                                    {
                                        captured = NativeMethods.BitBlt(
                                            hdcDest, 0, 0, width, height,
                                            hdcScreen, rect.Left, rect.Top,
                                            NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT);
                                    }
                                    finally
                                    {
                                        NativeMethods.ReleaseDC(IntPtr.Zero, hdcScreen);
                                    }
                                }
                            }

                            // Secondary strategy: PrintWindow with PW_RENDERFULLCONTENT for background windows
                            if (!captured)
                            {
                                try
                                {
                                    captured = NativeMethods.PrintWindow(hWnd, hdcDest, NativeMethods.PW_RENDERFULLCONTENT);
                                }
                                catch { }
                            }

                            // Fallback 1: Screen DC BitBlt for non-foreground windows
                            if (!captured)
                            {
                                IntPtr hdcScreen = NativeMethods.GetDC(IntPtr.Zero);
                                if (hdcScreen != IntPtr.Zero)
                                {
                                    try
                                    {
                                        captured = NativeMethods.BitBlt(
                                            hdcDest, 0, 0, width, height,
                                            hdcScreen, rect.Left, rect.Top,
                                            NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT);
                                    }
                                    finally
                                    {
                                        NativeMethods.ReleaseDC(IntPtr.Zero, hdcScreen);
                                    }
                                }
                            }

                            // Fallback 2: GetWindowDC
                            if (!captured)
                            {
                                IntPtr hdcWindow = NativeMethods.GetWindowDC(hWnd);
                                if (hdcWindow != IntPtr.Zero)
                                {
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
