namespace D3Vision.Core;

/// <summary>Session-only arming, continuous-hover dwell and rate-limited ready-skill selection.</summary>
public sealed class AutoCastController(TimeProvider? timeProvider = null)
{
    readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    readonly bool[] enabled = new bool[6];
    readonly long?[] lastAttempt = new long?[6];
    int? hoverSlot;
    long hoverStarted;
    long? lastHoverSample;
    bool hoverLatched;
    int nextSlot;
    long? lastDispatch;

    public bool IsEnabled(int slot) => slot is >= 0 and < 6 && enabled[slot];

    public IReadOnlyList<bool> EnabledSnapshot() => enabled.ToArray();

    public int? HoverSlot => hoverSlot;
    public double HoverFraction { get; private set; }

    public bool UpdateHover(int? slot, bool allowed)
    {
        long now = clock.GetTimestamp();
        if (!allowed || slot is null or < 0 or >= 6)
        {
            ResetHover();
            return false;
        }
        if (
            hoverSlot != slot
            || lastHoverSample is not { } previous
            || (!hoverLatched && clock.GetElapsedTime(previous, now).TotalSeconds > .5)
        )
        {
            hoverSlot = slot;
            hoverStarted = now;
            hoverLatched = false;
            HoverFraction = 0;
        }
        lastHoverSample = now;
        if (hoverLatched)
            return false;
        HoverFraction = Math.Clamp(clock.GetElapsedTime(hoverStarted, now).TotalSeconds / 3, 0, 1);
        if (HoverFraction < 1)
            return false;
        enabled[slot.Value] = !enabled[slot.Value];
        lastAttempt[slot.Value] = null;
        hoverLatched = true;
        HoverFraction = 0;
        return true;
    }

    /// <summary>Consumes an attempt before dispatch so a failed input cannot cause a tight retry loop.</summary>
    public int? TakeReadySkill(IReadOnlyList<SkillCountdown> skills, bool allowed)
    {
        if (!allowed || hoverSlot is not null)
            return null;
        long now = clock.GetTimestamp();
        if (
            lastDispatch is { } dispatched
            && clock.GetElapsedTime(dispatched, now).TotalSeconds < .2
        )
            return null;
        for (int offset = 0; offset < 6; offset++)
        {
            int slot = (nextSlot + offset) % 6;
            if (slot >= skills.Count || !enabled[slot])
                continue;
            var skill = skills[slot];
            if (
                skill.IsUnknown
                || skill.IsEstimated
                || !double.IsFinite(skill.RemainingSeconds)
                || skill.RemainingSeconds != 0
                || !double.IsFinite(skill.Fraction)
                || skill.Fraction != 0
            )
                continue;
            if (
                lastAttempt[slot] is { } attempt
                && clock.GetElapsedTime(attempt, now).TotalSeconds < 1
            )
                continue;
            lastAttempt[slot] = now;
            lastDispatch = now;
            nextSlot = (slot + 1) % 6;
            return slot;
        }
        return null;
    }

    public void StopAll()
    {
        Array.Clear(enabled);
        Array.Clear(lastAttempt);
        lastDispatch = null;
        nextSlot = 0;
        ResetHover();
    }

    void ResetHover()
    {
        hoverSlot = null;
        lastHoverSample = null;
        hoverLatched = false;
        HoverFraction = 0;
    }
}
