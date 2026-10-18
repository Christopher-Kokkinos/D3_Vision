using System.Diagnostics;
using System.Runtime.InteropServices;

namespace D3Vision.Infrastructure;

public static class GameInput
{
    public static bool TryGetGameBounds(out Native.Rect bounds)
    {
        bounds = default;
        if (!IsDiabloForeground())
            return false;
        var hwnd = GetForegroundWindow();
        if (
            DwmGetWindowAttribute(hwnd, 9, out bounds, Marshal.SizeOf<Native.Rect>()) != 0
            && !GetWindowRect(hwnd, out bounds)
        )
            return false;
        return bounds.Right > bounds.Left && bounds.Bottom > bounds.Top;
    }

    public static bool TryGetGameClientBounds(out Native.Rect bounds)
    {
        bounds = default;
        if (!IsDiabloForeground())
            return false;
        var hwnd = GetForegroundWindow();
        if (!Native.GetClientRect(hwnd, out var rect))
            return false;
        var origin = new Native.Point();
        if (!Native.ClientToScreen(hwnd, ref origin))
            return false;
        bounds = new Native.Rect
        {
            Left = origin.X,
            Top = origin.Y,
            Right = origin.X + rect.Right,
            Bottom = origin.Y + rect.Bottom,
        };
        return rect.Right > 0 && rect.Bottom > 0;
    }

    public static bool TryGetCursor(out Native.Point point) => GetPhysicalCursorPos(out point);

    public static bool IsDiabloForeground()
    {
        Native.GetWindowThreadProcessId(GetForegroundWindow(), out var id);
        try
        {
            using var process = Process.GetProcessById((int)id);
            return process.ProcessName.StartsWith("Diablo III", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
            when (ex
                    is ArgumentException
                        or InvalidOperationException
                        or System.ComponentModel.Win32Exception
            )
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern bool GetPhysicalCursorPos(out Native.Point point);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(nint hwnd, out Native.Rect rect);

    [DllImport("dwmapi.dll")]
    static extern int DwmGetWindowAttribute(
        nint hwnd,
        int attribute,
        out Native.Rect value,
        int size
    );
}
