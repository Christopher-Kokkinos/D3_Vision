using D3Vision.Infrastructure;

static class SkillInputChecks
{
    public static async Task<int> RunAsync()
    {
        int count = 0;
        void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
            count++;
        }
        var fake = new Backend();
        var caster = new GameSkillCaster(fake);
        Check(
            await caster.CastAsync(0, CancellationToken.None) == SkillCastResult.Skipped
                && fake.Events.Count == 0,
            "No input outside the game"
        );
        fake.Foreground = true;
        Check(
            await caster.CastAsync(0, CancellationToken.None) == SkillCastResult.Sent
                && fake.Events.SequenceEqual(new[] { (0, false), (0, true) }),
            "A cast balances press and release"
        );
        fake.Events.Clear();
        fake.Held = true;
        Check(
            await caster.CastAsync(0, CancellationToken.None) == SkillCastResult.Skipped
                && fake.Events.Count == 0,
            "Do not override held physical keys"
        );
        fake.Held = false;
        Check(
            await caster.CastAsync(7, CancellationToken.None) == SkillCastResult.Skipped
                && fake.Events.Count == 0,
            "Reject unsupported skill slots"
        );
        fake.LeftMouseHeld = true;
        Check(
            await caster.CastAsync(5, CancellationToken.None, requireReleasedMouse: true)
                == SkillCastResult.Skipped
                && fake.Events.Count == 0,
            "Kadala cannot purchase while the player drags an inventory item"
        );
        fake.LeftMouseHeld = false;
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Check(
            await caster.CastAsync(0, cancelled.Token) == SkillCastResult.Skipped
                && fake.Events.Count == 0,
            "Cancelled actions send no input"
        );
        using var stopDuringPress = new CancellationTokenSource();
        fake.OnPress = () => stopDuringPress.Cancel();
        bool stopped = false;
        try
        {
            await caster.CastAsync(5, stopDuringPress.Token);
        }
        catch (OperationCanceledException)
        {
            stopped = true;
        }
        Check(
            stopped && fake.Events.SequenceEqual(new[] { (5, false), (5, true) }),
            "Cancellation during a press still releases the mouse button"
        );
        fake.OnPress = null;
        fake.Events.Clear();
        fake.FailRelease = true;
        Check(
            await caster.CastAsync(1, CancellationToken.None) == SkillCastResult.Failed
                && fake.Events.Count(e => e.Release) == 2,
            "Release failure retries and reports failure"
        );
        fake.FailRelease = false;
        fake.Events.Clear();
        fake.LoseFocusOnRecheck = true;
        fake.FocusChecks = 0;
        Check(
            await caster.CastAsync(0, CancellationToken.None) == SkillCastResult.Skipped
                && fake.Events.Count == 0,
            "Focus is rechecked before input dispatch"
        );
        fake.LoseFocusOnRecheck = false;
        fake.Events.Clear();
        Check(
            await caster.CastAsync(0, CancellationToken.None, virtualKey: 0x51)
                == SkillCastResult.Sent
                && fake.KeyEvents.SequenceEqual(
                    new[] { ((ushort)0x51, false), ((ushort)0x51, true) }
                ),
            "A Q binding sends Q down/up rather than the default 1 key"
        );
        fake.KeyEvents.Clear();
        fake.Held = true;
        Check(
            await caster.CastAsync(0, CancellationToken.None, virtualKey: 0x57)
                == SkillCastResult.Skipped
                && fake.KeyEvents.Count == 0,
            "Held custom bindings are never overridden"
        );
        Check(
            D3Vision.Core.SkillBindings.TryKey(" q ", out var mapped) && mapped == 0x51,
            "Configured letter bindings are normalised"
        );
        Check(
            !D3Vision.Core.SkillBindings.TryKey("F10", out _)
                && !D3Vision.Core.SkillBindings.TryKey("", out _),
            "Invalid and reserved multi-key bindings cannot be saved"
        );
        fake.Held = false;
        fake.KeyEvents.Clear();
        Check(
            await caster.CastAsync(
                D3Vision.Core.SkillSlots.Potion,
                CancellationToken.None,
                virtualKey: 0x44
            ) == SkillCastResult.Sent
                && fake.KeyEvents.SequenceEqual(
                    new[] { ((ushort)0x44, false), ((ushort)0x44, true) }
                ),
            "Potion sends a balanced D press/release through the keyboard path"
        );
        fake.KeyEvents.Clear();
        fake.Foreground = false;
        Check(
            await caster.CastAsync(
                D3Vision.Core.SkillSlots.Potion,
                CancellationToken.None,
                virtualKey: 0x44
            ) == SkillCastResult.Skipped
                && fake.KeyEvents.Count == 0,
            "Potion cannot send D outside the foreground game"
        );
        foreach (ushort virtualKey in new ushort[] { 0x51, 0x57, 0x45, 0x52, 0x44 })
        {
            var down = GameSkillCaster.KeyboardFor(virtualKey, false);
            var up = GameSkillCaster.KeyboardFor(virtualKey, true);
            Check(
                down.VirtualKey == 0 && down.ScanCode > 0 && down.Flags == 8,
                "Game key-down uses a mapped hardware scan code, not virtual-key-only injection"
            );
            Check(
                up.VirtualKey == 0 && up.ScanCode == down.ScanCode && up.Flags == 10,
                "Game key-up releases the identical hardware scan code"
            );
        }
        Check(
            System.Runtime.InteropServices.Marshal.SizeOf<GameSkillCaster.Input>() == 40,
            "The native x64 INPUT structure has the correct Windows size"
        );
        Check(
            GameSkillCaster.InputFor(4, false).Data.Mouse.Flags == 2
                && GameSkillCaster.InputFor(4, true).Data.Mouse.Flags == 4,
            "Keyboard scan-code changes preserve left mouse button input"
        );
        Check(
            GameSkillCaster.InputFor(5, false).Data.Mouse.Flags == 8
                && GameSkillCaster.InputFor(5, true).Data.Mouse.Flags == 16,
            "Keyboard scan-code changes preserve right mouse button input"
        );
        return count;
    }

    sealed class Backend : ISkillInputBackend
    {
        public bool Foreground,
            Held,
            FailRelease,
            LoseFocusOnRecheck;
        public bool LeftMouseHeld;
        public int FocusChecks;
        public Action? OnPress;
        public List<(int Slot, bool Release)> Events = [];

        public List<(ushort Key, bool Release)> KeyEvents = [];

        public bool SendKey(ushort key, bool release)
        {
            KeyEvents.Add((key, release));
            return true;
        }

        public bool IsGameForeground() => Foreground && (!LoseFocusOnRecheck || ++FocusChecks < 2);

        public bool IsKeyDown(int key) => Held || (key == 1 && LeftMouseHeld);

        public bool Send(int slot, bool release)
        {
            Events.Add((slot, release));
            if (!release)
                OnPress?.Invoke();
            return !release || !FailRelease;
        }
    }
}
