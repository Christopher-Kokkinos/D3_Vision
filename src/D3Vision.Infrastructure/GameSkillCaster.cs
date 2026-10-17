using System.Runtime.InteropServices;

namespace D3Vision.Infrastructure;

public enum SkillCastResult
{
    Sent,
    Skipped,
    Failed,
}

public interface ISkillInputBackend
{
    bool IsGameForeground();
    bool IsKeyDown(int key);
    bool Send(int slot, bool release);
}

/// <summary>Sends a short balanced press to the foreground game; never moves the mouse.</summary>
public sealed class GameSkillCaster(ISkillInputBackend? inputBackend = null)
{
    readonly ISkillInputBackend backend = inputBackend ?? new Win32Backend();

    public async Task<SkillCastResult> CastAsync(
        int slot,
        CancellationToken token,
        bool requireReleasedMouse = false
    )
    {
        if (slot is < 0 or >= 6 || token.IsCancellationRequested || !backend.IsGameForeground())
            return SkillCastResult.Skipped;
        int key =
            slot < 4 ? 0x31 + slot
            : slot == 4 ? 1
            : 2;
        if (
            (requireReleasedMouse && (backend.IsKeyDown(1) || backend.IsKeyDown(2)))
            || backend.IsKeyDown(key)
            || backend.IsKeyDown(0x10)
            || backend.IsKeyDown(0x11)
            || backend.IsKeyDown(0x12)
        )
            return SkillCastResult.Skipped;
        // Recheck immediately before dispatch; no focus activation or background-window messages.
        if (!backend.IsGameForeground())
            return SkillCastResult.Skipped;
        if (!backend.Send(slot, false))
            return SkillCastResult.Failed;
        bool released = false;
        try
        {
            await Task.Delay(35, token);
        }
        finally
        {
            released = backend.Send(slot, true);
            if (!released)
                released = backend.Send(slot, true);
        }
        return released ? SkillCastResult.Sent : SkillCastResult.Failed;
    }

    sealed class Win32Backend : ISkillInputBackend
    {
        public bool IsGameForeground() => GameInput.IsDiabloForeground();

        public bool IsKeyDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

        public bool Send(int slot, bool release) =>
            SendInput(1, [InputFor(slot, release)], Marshal.SizeOf<Input>()) == 1;
    }

    static Input InputFor(int slot, bool release) =>
        slot < 4
            ? new Input
            {
                Type = 1,
                Data = new InputUnion
                {
                    Keyboard = new KeyboardInput
                    {
                        VirtualKey = (ushort)(0x31 + slot),
                        Flags = release ? 2u : 0u,
                    },
                },
            }
            : new Input
            {
                Type = 0,
                Data = new InputUnion
                {
                    Mouse = new MouseInput
                    {
                        Flags = slot == 4 ? (release ? 4u : 2u) : (release ? 16u : 8u),
                    },
                },
            };

    [StructLayout(LayoutKind.Sequential)]
    struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KeyboardInput
    {
        public ushort VirtualKey,
            ScanCode;
        public uint Flags,
            Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MouseInput
    {
        public int X,
            Y;
        public uint MouseData,
            Flags,
            Time;
        public nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint count, Input[] input, int size);

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int key);
}
