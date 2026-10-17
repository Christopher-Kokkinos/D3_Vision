using System.Runtime.InteropServices;

namespace D3Vision.Infrastructure;

public static class Native
{
    public delegate bool EnumProc(nint hwnd, nint param);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left,
            Top,
            Right,
            Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X,
            Y;
    }

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumProc callback, nint param);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hwnd, out uint id);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(nint hwnd);

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(nint hwnd, out Rect rect);

    [DllImport("user32.dll")]
    public static extern bool ClientToScreen(nint hwnd, ref Point point);

    [DllImport("user32.dll")]
    public static extern nint GetDC(nint hwnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(nint hwnd, nint dc);

    [DllImport("gdi32.dll")]
    public static extern nint CreateCompatibleDC(nint dc);

    [DllImport("gdi32.dll")]
    public static extern nint CreateCompatibleBitmap(nint dc, int width, int height);

    [DllImport("gdi32.dll")]
    public static extern nint SelectObject(nint dc, nint obj);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(nint obj);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(nint dc);

    [DllImport("gdi32.dll")]
    public static extern bool BitBlt(
        nint target,
        int x,
        int y,
        int width,
        int height,
        nint source,
        int sx,
        int sy,
        uint flags
    );

    [StructLayout(LayoutKind.Sequential)]
    public struct BitmapInfo
    {
        public uint Size;
        public int Width,
            Height;
        public ushort Planes,
            BitCount;
        public uint Compression,
            ImageSize;
        public int XPels,
            YPels;
        public uint Used,
            Important;
    }

    [DllImport("gdi32.dll")]
    public static extern int GetDIBits(
        nint dc,
        nint bitmap,
        uint start,
        uint lines,
        byte[] data,
        ref BitmapInfo info,
        uint usage
    );

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(nint hwnd, int id);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern nint GetWindowLongPtr(nint hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);

    [DllImport("user32.dll")]
    public static extern bool SetWindowDisplayAffinity(nint hwnd, uint affinity);

    [DllImport("user32.dll")]
    public static extern bool EnumDisplayMonitors(
        nint dc,
        nint clip,
        MonitorProc callback,
        nint data
    );

    public delegate bool MonitorProc(nint monitor, nint dc, ref Rect rect, nint data);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(
        nint hwnd,
        nint after,
        int x,
        int y,
        int width,
        int height,
        uint flags
    );

    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(nint hwnd);

    public static List<Rect> Monitors()
    {
        List<Rect> list = [];
        EnumDisplayMonitors(
            0,
            0,
            (nint m, nint d, ref Rect r, nint p) =>
            {
                list.Add(r);
                return true;
            },
            0
        );
        return list;
    }
}
