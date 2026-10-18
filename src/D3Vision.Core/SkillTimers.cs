using System.Diagnostics;

namespace D3Vision.Core;

public readonly record struct SkillCountdown(
    string Key,
    double RemainingSeconds,
    double Fraction,
    bool IsEstimated = false,
    bool IsUnknown = false
);

/// <summary>Input-based estimates, not observed game cooldown state. Uses a monotonic clock.</summary>
public sealed class SkillTimers(TimeProvider? timeProvider = null)
{
    readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    readonly Dictionary<int, (long Started, double Duration)> running = [];

    public void Start(int slot, SkillSettings skill)
    {
        double duration = skill.DurationSeconds;
        if (!double.IsFinite(duration) || duration <= 0 || duration > 3600)
            return;
        if (Remaining(slot) > 0)
            return; // Holding/repeating a skill must not restart its countdown.
        running[slot] = (clock.GetTimestamp(), duration);
    }

    public double Remaining(int slot)
    {
        if (!running.TryGetValue(slot, out var timer))
            return 0;
        return Math.Max(0, timer.Duration - clock.GetElapsedTime(timer.Started).TotalSeconds);
    }

    public IReadOnlyList<SkillCountdown> Snapshot(IReadOnlyList<SkillSettings> skills) =>
        skills
            .Take(SkillSlots.Count)
            .Select(
                (skill, i) =>
                {
                    double remaining = Remaining(i);
                    double duration = running.TryGetValue(i, out var timer) ? timer.Duration : 1;
                    return new SkillCountdown(skill.Key, remaining, remaining / duration);
                }
            )
            .ToArray();

    public void Reset() => running.Clear();
}
