using System.Runtime.InteropServices;
using D3Vision.Core;

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
    bool SendKey(ushort key, bool release) => false;
}

/// <summary>Sends a short balanced press to the foreground game; never moves the mouse.</summary>
public sealed class GameSkillCaster(ISkillInputBackend? inputBackend = null)
{
    public string LastReason { get; private set; } = "No input attempted.";

    SkillCastResult Result(SkillCastResult result, string reason)
    {
        LastReason = reason;
        return result;
    }

    readonly ISkillInputBackend backend = inputBackend ?? new Win32Backend();

    public async Task<SkillCastResult> CastAsync(
        int slot,
        CancellationToken token,
        bool requireReleasedMouse = false,
        ushort? virtualKey = null
    )
    {
        if (
            slot is < 0 or >= SkillSlots.Count
            || token.IsCancellationRequested
            || !backend.IsGameForeground()
        )
            return Result(
                SkillCastResult.Skipped,
                "Game focus, cancellation or slot validation blocked input."
            );
        if (
            (!SkillSlots.IsKeyboard(slot) && virtualKey is not null)
            || (
                virtualKey is { } configured
                && configured is not (>= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A)
            )
        )
            return Result(SkillCastResult.Skipped, "Invalid keyboard binding.");
        int key =
            SkillSlots.IsKeyboard(slot)
                ? virtualKey ?? (ushort)(slot == SkillSlots.Potion ? 0x44 : 0x31 + slot)
            : slot == 4 ? 1
            : 2;
        if (
            (requireReleasedMouse && (backend.IsKeyDown(1) || backend.IsKeyDown(2)))
            || backend.IsKeyDown(key)
            || backend.IsKeyDown(0x10)
            || backend.IsKeyDown(0x11)
            || backend.IsKeyDown(0x12)
        )
            return Result(
                SkillCastResult.Skipped,
                "The skill key, mouse button or a modifier is held."
            );
        // Recheck immediately before dispatch; no focus activation or background-window messages.
        if (!backend.IsGameForeground())
            return Result(SkillCastResult.Skipped, "Game focus changed before dispatch.");
        bool Send(bool release) =>
            SkillSlots.IsKeyboard(slot) && virtualKey is { } selected
                ? backend.SendKey(selected, release)
                : backend.Send(slot, release);
        if (!Send(false))
            return Result(
                SkillCastResult.Failed,
                "Windows rejected the press; check that the game and overlay use the same elevation level."
            );
        bool released = false;
        try
        {
            await Task.Delay(100, token);
        }
        finally
        {
            released = Send(true);
            if (!released)
                released = Send(true);
        }
        return released
            ? Result(
                SkillCastResult.Sent,
                "Press and release sent; game activation is not yet confirmed."
            )
            : Result(SkillCastResult.Failed, "Windows rejected the key/button release.");
    }

    sealed class Win32Backend : ISkillInputBackend
    {
        public bool IsGameForeground() => GameInput.IsDiabloForeground();

        public bool IsKeyDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

        public bool Send(int slot, bool release) =>
            SendInput(1, [InputFor(slot, release)], Marshal.SizeOf<Input>()) == 1;

        public bool SendKey(ushort key, bool release) =>
            SendInput(1, [InputFor(0, release, key)], Marshal.SizeOf<Input>()) == 1;
    }

    internal static Input InputFor(int slot, bool release, ushort? key = null)
    {
        if (SkillSlots.IsKeyboard(slot))
        {
            ushort virtualKey = key ?? (ushort)(slot == SkillSlots.Potion ? 0x44 : 0x31 + slot);
            return new Input
            {
                Type = 1,
                Data = new InputUnion { Keyboard = KeyboardFor(virtualKey, release) },
            };
        }
        return new Input
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
    }

    internal static KeyboardInput KeyboardFor(ushort key, bool release)
    {
        // KEYEVENTF_SCANCODE makes wScan identify the physical key. Send identical
        // scan codes on key-down and key-up; never use Unicode/text injection.
        uint scan = MapVirtualKey(key, 0);
        if (scan == 0)
            throw new InvalidOperationException("The skill key has no keyboard scan code.");
        return new KeyboardInput { ScanCode = (ushort)scan, Flags = 8u | (release ? 2u : 0u) };
    }

    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
    static extern uint MapVirtualKey(uint code, uint mapType);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput
    {
        public ushort VirtualKey,
            ScanCode;
        public uint Flags,
            Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput
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
