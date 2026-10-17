using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using D3Vision.Infrastructure;
using Microsoft.Win32.SafeHandles;

internal static class ContextDiagnostics
{
    // Diagnostic only: x64 TEB TLS slot offsets, bounded to the first 64 slots.
    // Querying threads does not suspend them or run code inside the game.
    public static void Print(Process process, ReadOnlyProcess memory, nint module)
    {
        int slot = BitConverter.ToInt32(memory.Read(module + 0x1707B10, 4));
        Console.WriteLine($"Game TLS index: {slot}");
        if (slot is < 0 or >= 64)
            return;
        var seen = new HashSet<nint>();
        foreach (ProcessThread thread in process.Threads)
        {
            using var handle = OpenThread(0x40, false, thread.Id);
            if (handle.IsInvalid)
                continue;
            if (
                NtQueryInformationThread(handle, 0, out var info, Marshal.SizeOf<ThreadInfo>(), 0)
                != 0
            )
                continue;
            try
            {
                var tls = memory.Pointer(info.Teb + 0x1480 + slot * 8);
                if (tls == 0)
                    continue;
                var holder = memory.Pointer(tls);
                if (holder == 0)
                    continue;
                var context = memory.Pointer(holder);
                if (context == 0 || !seen.Add(context))
                    continue;
                Console.WriteLine(
                    $"Thread {thread.Id}, context 0x{context:X}, scene-related storage 0x{memory.Pointer(context + 0x148):X}"
                );
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                Console.WriteLine($"Thread {thread.Id}: TLS changed or was unavailable.");
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ThreadInfo
    {
        public nint ExitStatus,
            Teb,
            ProcessId,
            ThreadId,
            Affinity;
        public int Priority,
            BasePriority;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeWaitHandle OpenThread(
        uint access,
        [MarshalAs(UnmanagedType.Bool)] bool inherit,
        int threadId
    );

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationThread(
        SafeWaitHandle thread,
        int informationClass,
        out ThreadInfo information,
        int length,
        nint returnLength
    );
}
