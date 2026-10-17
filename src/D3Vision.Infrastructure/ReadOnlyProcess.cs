using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace D3Vision.Infrastructure;

internal interface IGameMemoryReader
{
    byte[] Read(nint address, int count);
    nint Pointer(nint address);
}

internal sealed class ReadOnlyProcess : IGameMemoryReader, IDisposable
{
    private readonly SafeProcessHandle handle;
    private int bytesRead;

    public ReadOnlyProcess(int processId)
    {
        handle = OpenProcess(0x1000 | 0x0010, false, processId);
        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public byte[] Read(nint address, int count)
    {
        if (address < 0x10000 || count <= 0 || count > 0x100000 || bytesRead + count > 0x1000000)
            throw new InvalidOperationException("Memory read bounds failed validation.");
        var bytes = new byte[count];
        if (
            !ReadProcessMemory(handle, address, bytes, (nuint)count, out var actual)
            || actual != (nuint)count
        )
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not read a complete scene snapshot."
            );
        bytesRead += count;
        return bytes;
    }

    public nint Pointer(nint address) => (nint)BitConverter.ToInt64(Read(address, 8));

    public void Dispose() => handle.Dispose();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(
        uint access,
        [MarshalAs(UnmanagedType.Bool)] bool inherit,
        int processId
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(
        SafeProcessHandle process,
        nint address,
        [Out] byte[] buffer,
        nuint size,
        out nuint bytesRead
    );
}
