using System.ComponentModel;
using System.Runtime.InteropServices;

namespace D3Vision.Infrastructure;

/// <summary>Short, foreground-only client-coordinate clicks without cursor movement or input holds.</summary>
public sealed class GameWindowInventoryInput
{
    readonly Func<nint> foreground;
    readonly Func<nint, uint, nuint, nint, bool> post;
    readonly nint window;
    readonly Native.Rect bounds;

    public GameWindowInventoryInput(Native.Rect bounds)
        : this(bounds, GetForegroundWindow, PostMessage)
    {
        if (!GameInput.IsDiabloForeground())
            throw new InvalidOperationException("Diablo III must be foreground.");
    }

    internal GameWindowInventoryInput(
        Native.Rect bounds,
        Func<nint> foreground,
        Func<nint, uint, nuint, nint, bool> post
    )
    {
        this.bounds = bounds;
        this.foreground = foreground;
        this.post = post;
        window = foreground();
        if (window == 0)
            throw new InvalidOperationException("No foreground game window.");
    }

    public void Click(
        int clientX,
        int clientY,
        bool right,
        Func<bool> allowed,
        CancellationToken token
    )
    {
        token.ThrowIfCancellationRequested();
        if (!allowed() || foreground() != window)
            throw new OperationCanceledException("Game focus or automation state changed.", token);
        if (
            clientX < 0
            || clientY < 0
            || clientX >= bounds.Right - bounds.Left
            || clientY >= bounds.Bottom - bounds.Top
            || clientX > short.MaxValue
            || clientY > short.MaxValue
        )
            throw new ArgumentOutOfRangeException(
                nameof(clientX),
                "Click is outside the captured game client."
            );
        nint position = (nint)((clientY << 16) | clientX);
        uint down = right ? 0x204u : 0x201u;
        uint up = right ? 0x205u : 0x202u;
        if (!post(window, down, right ? 2u : 1u, position))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        // Always balance an accepted down, even when cancellation/focus changes meanwhile.
        if (!post(window, up, 0, position))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [DllImport("user32.dll")]
    static extern nint GetForegroundWindow();

    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool PostMessage(nint window, uint message, nuint wParam, nint lParam);
}
