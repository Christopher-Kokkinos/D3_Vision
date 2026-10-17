using System.Diagnostics;
using D3Vision.Core;

namespace D3Vision.Infrastructure;

// Captures visible client pixels only; borderless/windowed mode is required.
public sealed class ScreenCapture : IScreenCapture
{
    nint memory,
        bitmap,
        old;
    int width,
        height;
    byte[] pixels = [];
    nint game;
    DateTime nextLookup;

    public CaptureFrame? Capture(bool wholeScreen, int monitor)
    {
        Native.Rect rect;
        if (wholeScreen)
        {
            var screens = Native.Monitors();
            if (screens.Count == 0)
                return null;
            rect = screens[Math.Clamp(monitor, 0, screens.Count - 1)];
        }
        else
        {
            if (DateTime.UtcNow >= nextLookup)
            {
                game = 0;
                Native.EnumWindows(
                    (h, p) =>
                    {
                        Native.GetWindowThreadProcessId(h, out var id);
                        try
                        {
                            using var process = Process.GetProcessById((int)id);
                            if (
                                process.ProcessName.StartsWith(
                                    "Diablo III",
                                    StringComparison.OrdinalIgnoreCase
                                ) && Native.IsWindowVisible(h)
                            )
                            {
                                game = h;
                                return false;
                            }
                        }
                        catch (ArgumentException) { }
                        return true;
                    },
                    0
                );
                nextLookup = DateTime.UtcNow.AddSeconds(2);
            }
            if (game == 0 || Native.IsIconic(game) || !Native.GetClientRect(game, out rect))
                return null;
            var point = new Native.Point();
            if (!Native.ClientToScreen(game, ref point))
                return null;
            rect.Right += point.X;
            rect.Bottom += point.Y;
            rect.Left = point.X;
            rect.Top = point.Y;
        }
        int w = rect.Right - rect.Left,
            h = rect.Bottom - rect.Top;
        if (w <= 0 || h <= 0 || w > 16384 || h > 16384)
            return null;
        var dc = Native.GetDC(0);
        if (dc == 0)
            return null;
        try
        {
            if (w != width || h != height)
            {
                Dispose();
                width = w;
                height = h;
                memory = Native.CreateCompatibleDC(dc);
                bitmap = Native.CreateCompatibleBitmap(dc, w, h);
                if (memory == 0 || bitmap == 0)
                    throw new InvalidOperationException("Cannot allocate capture surface.");
                old = Native.SelectObject(memory, bitmap);
                pixels = new byte[checked(w * h * 4)];
            }
            if (!Native.BitBlt(memory, 0, 0, w, h, dc, rect.Left, rect.Top, 0x00CC0020))
                return null;
            Native.SelectObject(memory, old);
            var info = new Native.BitmapInfo
            {
                Size = 40,
                Width = w,
                Height = -h,
                Planes = 1,
                BitCount = 32,
            };
            try
            {
                if (Native.GetDIBits(memory, bitmap, 0, (uint)h, pixels, ref info, 0) == 0)
                    return null;
            }
            finally
            {
                Native.SelectObject(memory, bitmap);
            }
            return new(pixels, w, h);
        }
        finally
        {
            Native.ReleaseDC(0, dc);
        }
    }

    public void Dispose()
    {
        if (memory != 0)
        {
            Native.SelectObject(memory, old);
            Native.DeleteDC(memory);
        }
        if (bitmap != 0)
            Native.DeleteObject(bitmap);
        memory = bitmap = old = 0;
        width = height = 0;
        pixels = [];
    }
}
