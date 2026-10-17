using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using D3Vision.Core;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using WinRT;

namespace D3Vision.Infrastructure;

/// <summary>GPU-backed Windows Graphics Capture. Call and dispose from one worker.</summary>
public sealed class GraphicsCapture : IScreenCapture
{
    IDirect3DDevice? device;
    GraphicsCaptureItem? item;
    Direct3D11CaptureFramePool? pool;
    GraphicsCaptureSession? session;
    nint currentTarget;
    bool currentWhole;
    int currentMonitor = -1;
    DateTime nextSearch;
    byte[] buffer = [];
    int width,
        height;
    volatile bool closed;

    public CaptureFrame? Capture(bool wholeScreen, int monitor)
    {
        if (!GraphicsCaptureSession.IsSupported())
            throw new PlatformNotSupportedException("Windows Graphics Capture is unavailable.");
        if (closed)
        {
            Reset();
            closed = false;
        }
        if (
            item is null
            && currentMonitor == monitor
            && currentWhole == wholeScreen
            && DateTime.UtcNow < nextSearch
        )
            return null;
        if (
            item is null
            || wholeScreen != currentWhole
            || monitor != currentMonitor
            || DateTime.UtcNow >= nextSearch
        )
        {
            nint target = wholeScreen ? FindMonitor(monitor) : FindGame();
            nextSearch = DateTime.UtcNow.AddSeconds(2);
            if (
                target != currentTarget
                || wholeScreen != currentWhole
                || monitor != currentMonitor
                || item is null
            )
            {
                Reset();
                currentWhole = wholeScreen;
                currentMonitor = monitor;
                if (target == 0)
                    return null;
                currentTarget = target;
                device ??= CreateDevice();
                item = CreateItem(target, wholeScreen);
                item.Closed += OnClosed;
                width = item.Size.Width;
                height = item.Size.Height;
                pool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    device,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    item.Size
                );
                session = pool.CreateCaptureSession(item);
                session.IsCursorCaptureEnabled = false;
                session.StartCapture();
            }
        }
        if (!wholeScreen && Native.IsIconic(currentTarget))
            return null;
        using var frame = pool!.TryGetNextFrame();
        if (frame is null)
            return null;
        if (frame.ContentSize.Width <= 0 || frame.ContentSize.Height <= 0)
            return null;
        // Recreate before processing the next frame when the game changes resolution.
        if (frame.ContentSize.Width != width || frame.ContentSize.Height != height)
        {
            var size = frame.ContentSize;
            width = size.Width;
            height = size.Height;
            pool.Recreate(device!, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, size);
            return null;
        }
        using var bitmap = SoftwareBitmap
            .CreateCopyFromSurfaceAsync(frame.Surface, BitmapAlphaMode.Ignore)
            .AsTask()
            .GetAwaiter()
            .GetResult();
        int length = checked(bitmap.PixelWidth * bitmap.PixelHeight * 4);
        if (buffer.Length != length)
            buffer = new byte[length];
        bitmap.CopyToBuffer(buffer.AsBuffer());
        return new(buffer, bitmap.PixelWidth, bitmap.PixelHeight);
    }

    void OnClosed(GraphicsCaptureItem sender, object args) => closed = true;

    static nint FindGame()
    {
        nint found = 0;
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
                        found = h;
                        return false;
                    }
                }
                catch (Exception ex)
                    when (ex
                            is ArgumentException
                                or InvalidOperationException
                                or System.ComponentModel.Win32Exception
                    ) { }
                return true;
            },
            0
        );
        return found;
    }

    static nint FindMonitor(int index)
    {
        List<nint> monitors = [];
        Native.EnumDisplayMonitors(
            0,
            0,
            (nint m, nint dc, ref Native.Rect r, nint data) =>
            {
                monitors.Add(m);
                return true;
            },
            0
        );
        return monitors.Count == 0 ? 0 : monitors[Math.Clamp(index, 0, monitors.Count - 1)];
    }

    static GraphicsCaptureItem CreateItem(nint target, bool monitor)
    {
        const string runtimeClass = "Windows.Graphics.Capture.GraphicsCaptureItem";
        Marshal.ThrowExceptionForHR(
            WindowsCreateString(runtimeClass, runtimeClass.Length, out var text)
        );
        nint factory = 0,
            result = 0;
        try
        {
            var iid = typeof(ICaptureItemInterop).GUID;
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(text, ref iid, out factory));
            var interop = (ICaptureItemInterop)Marshal.GetObjectForIUnknown(factory);
            var itemId = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
            try
            {
                Marshal.ThrowExceptionForHR(
                    monitor
                        ? interop.CreateForMonitor(target, ref itemId, out result)
                        : interop.CreateForWindow(target, ref itemId, out result)
                );
                return MarshalInterface<GraphicsCaptureItem>.FromAbi(result);
            }
            finally
            {
                Marshal.ReleaseComObject(interop);
            }
        }
        finally
        {
            if (result != 0)
                Marshal.Release(result);
            if (factory != 0)
                Marshal.Release(factory);
            WindowsDeleteString(text);
        }
    }

    static IDirect3DDevice CreateDevice()
    {
        nint native = 0,
            context = 0,
            dxgi = 0,
            inspectable = 0;
        try
        {
            int hr = D3D11CreateDevice(0, 1, 0, 0x20, 0, 0, 7, out native, out _, out context);
            if (hr < 0)
                Marshal.ThrowExceptionForHR(
                    D3D11CreateDevice(0, 5, 0, 0x20, 0, 0, 7, out native, out _, out context)
                );
            var id = new Guid("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(native, ref id, out dxgi));
            Marshal.ThrowExceptionForHR(
                CreateDirect3D11DeviceFromDXGIDevice(dxgi, out inspectable)
            );
            return MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
        }
        finally
        {
            if (inspectable != 0)
                Marshal.Release(inspectable);
            if (dxgi != 0)
                Marshal.Release(dxgi);
            if (context != 0)
                Marshal.Release(context);
            if (native != 0)
                Marshal.Release(native);
        }
    }

    void Reset()
    {
        session?.Dispose();
        session = null;
        pool?.Dispose();
        pool = null;
        if (item is not null)
            item.Closed -= OnClosed;
        item = null;
        currentTarget = 0;
        width = height = 0;
        buffer = [];
    }

    public void Dispose()
    {
        Reset();
        (device as IDisposable)?.Dispose();
        device = null;
    }

    [
        ComImport,
        Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"),
        InterfaceType(ComInterfaceType.InterfaceIsIUnknown)
    ]
    interface ICaptureItemInterop
    {
        [PreserveSig]
        int CreateForWindow(nint window, ref Guid iid, out nint result);

        [PreserveSig]
        int CreateForMonitor(nint monitor, ref Guid iid, out nint result);
    }

    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    static extern int WindowsCreateString(string source, int length, out nint result);

    [DllImport("combase.dll")]
    static extern int WindowsDeleteString(nint value);

    [DllImport("combase.dll")]
    static extern int RoGetActivationFactory(nint name, ref Guid iid, out nint factory);

    [DllImport("d3d11.dll")]
    static extern int D3D11CreateDevice(
        nint adapter,
        int driver,
        nint software,
        uint flags,
        nint levels,
        uint levelCount,
        uint sdk,
        out nint device,
        out int featureLevel,
        out nint context
    );

    [DllImport("d3d11.dll")]
    static extern int CreateDirect3D11DeviceFromDXGIDevice(nint device, out nint result);
}
