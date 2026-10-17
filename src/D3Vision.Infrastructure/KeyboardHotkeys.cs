using System.ComponentModel;
using System.Runtime.InteropServices;

namespace D3Vision.Infrastructure;

/// <summary>Low-level hook supports F12, which Windows reserves from RegisterHotKey.</summary>
public sealed class KeyboardHotkeys : IDisposable
{
    readonly HookProc callback;
    readonly HookProc mouseCallback;
    readonly Action<int>? skillPressed;
    readonly Action<int, int>? physicalLeftClick;
    nint mouseHook;
    readonly HashSet<int> pressed = [];
    readonly Action<int> dispatch;
    nint hook;

    public KeyboardHotkeys(
        Action<int> dispatch,
        Action<int>? skillPressed = null,
        Action<int, int>? physicalLeftClick = null
    )
    {
        this.physicalLeftClick = physicalLeftClick;
        this.dispatch = dispatch;
        this.skillPressed = skillPressed;
        mouseCallback = HandleMouse;
        callback = Handle;
        hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
        if (hook == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (skillPressed is not null || physicalLeftClick is not null)
        {
            mouseHook = SetWindowsHookEx(14, mouseCallback, GetModuleHandle(null), 0);
            if (mouseHook == 0)
            {
                int error = Marshal.GetLastWin32Error();
                Dispose();
                throw new Win32Exception(error);
            }
        }
    }

    nint Handle(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            int key = Marshal.ReadInt32(data);
            // Menu/chat keys stop auto-cast, but still reach the game normally.
            if (
                (message == 0x100 || message == 0x104)
                && key
                    is 0x0D
                        or 0x1B
                        or 0x49
                        or 0x43
                        or 0x53
                        or 0x50
                        or 0x4A
                        or 0x4D
                        or 0x4F
                        or 0x54
            )
                dispatch(3);
            if (key is >= 0x31 and <= 0x34)
            {
                if (message == 0x100 && pressed.Add(key))
                    skillPressed?.Invoke(key);
                if (message == 0x101)
                    pressed.Remove(key);
                // Skill inputs are observed and always pass through to the game.
            }
            if (key is >= 0x79 and <= 0x7B)
            {
                if (message == 0x100 || message == 0x104)
                {
                    if (pressed.Add(key))
                        dispatch(key - 0x79);
                    return 1;
                }
                if (message == 0x101 || message == 0x105)
                {
                    pressed.Remove(key);
                    return 1;
                }
            }
        }
        return CallNextHookEx(hook, code, message, data);
    }

    nint HandleMouse(int code, nint message, nint data)
    {
        // MSLLHOOKSTRUCT flags +12: ignore SendInput and other injected clicks.
        if (code >= 0 && IsPhysicalLeftRelease(message, Marshal.ReadInt32(data, 12)))
            physicalLeftClick?.Invoke(Marshal.ReadInt32(data), Marshal.ReadInt32(data, 4));
        if (code >= 0 && (message == 0x201 || message == 0x204))
            skillPressed?.Invoke(message == 0x201 ? 1 : 2);
        return CallNextHookEx(mouseHook, code, message, data);
    }

    internal static bool IsPhysicalLeftRelease(nint message, int flags) =>
        message == 0x202 && (flags & 3) == 0;

    public void Dispose()
    {
        if (hook != 0)
        {
            UnhookWindowsHookEx(hook);
            hook = 0;
        }
        if (mouseHook != 0)
        {
            UnhookWindowsHookEx(mouseHook);
            mouseHook = 0;
        }
        GC.KeepAlive(callback);
        GC.KeepAlive(mouseCallback);
    }

    delegate nint HookProc(int code, nint message, nint data);

    [DllImport("user32.dll", SetLastError = true)]
    static extern nint SetWindowsHookEx(int id, HookProc callback, nint module, uint threadId);

    [DllImport("user32.dll")]
    static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern nint GetModuleHandle(string? name);
}
