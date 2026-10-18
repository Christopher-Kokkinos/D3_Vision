using System.Runtime.InteropServices;

namespace D3Vision.Infrastructure;

/// <summary>Foreground-only balanced input. Never writes game memory.</summary>
public sealed class GameInventoryInput
{
    public static bool MouseReleased =>
        (GetAsyncKeyState(1) & 0x8000) == 0 && (GetAsyncKeyState(2) & 0x8000) == 0;

    public async Task ClickAsync(int x, int y, Func<bool> allowed, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!allowed() || !MouseReleased)
            throw new InvalidOperationException("Game input is unavailable.");
        if (!SetCursorPos(x, y))
            throw new InvalidOperationException("Could not position the cursor.");
        await Task.Delay(35, token);
        if (!allowed())
            throw new OperationCanceledException(token);
        SendMouse(2);
        try
        {
            await Task.Delay(35, token);
        }
        finally
        {
            SendMouse(4);
        }
    }

    public async Task DragAsync(
        int x,
        int y,
        int targetX,
        int targetY,
        Func<bool> allowed,
        CancellationToken token
    )
    {
        token.ThrowIfCancellationRequested();
        if (!allowed() || !MouseReleased)
            throw new InvalidOperationException("Game input is unavailable.");
        if (!SetCursorPos(x, y))
            throw new InvalidOperationException("Could not position the cursor.");
        await Task.Delay(35, token);
        if (!allowed())
            throw new OperationCanceledException(token);
        SendMouse(2);
        try
        {
            await Task.Delay(50, token);
            if (!allowed())
                throw new OperationCanceledException(token);
            if (!SetCursorPos(targetX, targetY))
                throw new InvalidOperationException("Could not drag the item.");
            await Task.Delay(50, token);
        }
        finally
        {
            SendMouse(4);
        }
    }

    public async Task KeyAsync(ushort key, Func<bool> allowed, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!allowed() || (GetAsyncKeyState(key) & 0x8000) != 0)
            throw new InvalidOperationException("Game input is unavailable.");
        Send(
            new Input
            {
                Type = 1,
                Data = new Union { Key = new Keyboard { VirtualKey = key } },
            }
        );
        try
        {
            await Task.Delay(35, token);
        }
        finally
        {
            Send(
                new Input
                {
                    Type = 1,
                    Data = new Union
                    {
                        Key = new Keyboard { VirtualKey = key, Flags = 2 },
                    },
                }
            );
        }
    }

    static void SendMouse(uint flags) =>
        Send(new Input { Data = new Union { Mouse = new Mouse { Flags = flags } } });

    static void Send(Input value)
    {
        if (SendInput(1, [value], Marshal.SizeOf<Input>()) != 1)
            throw new InvalidOperationException("Windows rejected game input.");
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Input
    {
        public uint Type;
        public Union Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct Union
    {
        [FieldOffset(0)]
        public Mouse Mouse;

        [FieldOffset(0)]
        public Keyboard Key;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Mouse
    {
        public int X,
            Y;
        public uint Data,
            Flags,
            Time;
        public nuint Extra;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Keyboard
    {
        public ushort VirtualKey,
            Scan;
        public uint Flags,
            Time;
        public nuint Extra;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int key);
}
