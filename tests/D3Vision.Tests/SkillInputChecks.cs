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
