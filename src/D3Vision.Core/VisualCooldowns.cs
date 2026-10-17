namespace D3Vision.Core;

public enum CooldownMode
{
    VisualRead,
    InputEstimate,
}

public enum CooldownVisibility
{
    Unknown,
    Ready,
    Cooling,
}

/// <summary>Normalised capture coordinates, independent of the display widget's position.</summary>
public sealed record SkillCalibration(
    double X,
    double Y,
    double Width,
    double Height,
    byte[] ReadyBgra,
    double CaptureAspectRatio = 0,
    bool? WholeScreen = null,
    int? Monitor = null
);

public readonly record struct CooldownObservation(
    int Slot,
    CooldownVisibility Visibility,
    double Fraction
);

/// <summary>Infers seconds from the observed sweep's slope; never claims exact game telemetry.</summary>
public sealed class VisualCooldownTimers(TimeProvider? timeProvider = null)
{
    readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    readonly State[] states = Enumerable.Range(0, 6).Select(_ => new State()).ToArray();

    sealed class State
    {
        public long Seen;
        public bool HasObservation;
        public CooldownVisibility Visibility;
        public double Fraction;
        public double? Duration;
        public int ReadyFrames;
        public Queue<(long Time, double Fraction)> Samples = [];
    }

    public void Apply(CooldownObservation observation)
    {
        if (observation.Slot is < 0 or >= 6)
            return;
        var state = states[observation.Slot];
        long now = clock.GetTimestamp();
        if (state.HasObservation && clock.GetElapsedTime(state.Seen, now).TotalSeconds > 1)
        {
            state.Samples.Clear();
            state.Duration = null;
            state.ReadyFrames = 0;
        }
        state.Seen = now;
        state.HasObservation = true;
        if (observation.Visibility == CooldownVisibility.Unknown)
        {
            state.Visibility = CooldownVisibility.Unknown;
            state.Samples.Clear();
            state.Duration = null;
            state.ReadyFrames = 0;
            return;
        }
        if (observation.Visibility == CooldownVisibility.Ready)
        {
            state.ReadyFrames++;
            // A single bright frame must not clear an active cooldown.
            if (state.ReadyFrames >= 2)
            {
                state.Visibility = CooldownVisibility.Ready;
                state.Fraction = 0;
                state.Samples.Clear();
                state.Duration = null;
            }
            return;
        }
        state.ReadyFrames = 0;
        if (!double.IsFinite(observation.Fraction) || observation.Fraction is <= 0 or > 1)
        {
            state.Visibility = CooldownVisibility.Unknown;
            state.Duration = null;
            state.Samples.Clear();
            return;
        }
        if (
            state.Visibility != CooldownVisibility.Cooling
            || observation.Fraction > state.Fraction + .12
        )
        {
            state.Samples.Clear();
            state.Duration = null;
        }
        // Abrupt reductions are not slope samples: retain the previous rate, update the fraction.
        bool jump = observation.Fraction < state.Fraction - .12;
        if (jump)
            state.Samples.Clear();
        state.Visibility = CooldownVisibility.Cooling;
        state.Fraction = observation.Fraction;
        state.Samples.Enqueue((now, state.Fraction));
        while (state.Samples.Count > 60)
            state.Samples.Dequeue();
        Fit(state);
    }

    void Fit(State state)
    {
        if (state.Samples.Count < 4)
            return;
        var points = state.Samples.ToArray();
        if (clock.GetElapsedTime(points[0].Time, points[^1].Time).TotalSeconds < .5)
            return;
        double meanX = points.Average(p =>
            clock.GetElapsedTime(points[0].Time, p.Time).TotalSeconds
        );
        double meanY = points.Average(p => p.Fraction);
        double xx = 0,
            xy = 0,
            yy = 0;
        foreach (var p in points)
        {
            double x = clock.GetElapsedTime(points[0].Time, p.Time).TotalSeconds - meanX;
            double y = p.Fraction - meanY;
            xx += x * x;
            xy += x * y;
            yy += y * y;
        }
        double slope = xx > 0 ? xy / xx : 0;
        if (slope >= -.0001 || yy <= 0 || xy * xy / (xx * yy) < .85)
            return;
        double duration = -1 / slope;
        if (duration is >= .2 and <= 3600)
            state.Duration = duration;
    }

    public IReadOnlyList<SkillCountdown> Snapshot(IReadOnlyList<SkillSettings> skills) =>
        skills
            .Take(6)
            .Select(
                (skill, slot) =>
                {
                    var state = states[slot];
                    double age = state.HasObservation
                        ? clock.GetElapsedTime(state.Seen).TotalSeconds
                        : double.PositiveInfinity;
                    if (
                        age > 1
                        || state.Visibility == CooldownVisibility.Unknown
                        || !state.HasObservation
                    )
                        return new SkillCountdown(skill.Key, 0, 0, IsUnknown: true);
                    if (state.Visibility == CooldownVisibility.Ready)
                        return new SkillCountdown(skill.Key, 0, 0);
                    if (state.Duration is not { } duration)
                        return new SkillCountdown(skill.Key, 0, state.Fraction, IsUnknown: true);
                    // Do not run to an apparently ready state without a fresh visual confirmation.
                    double remaining = Math.Max(.1, state.Fraction * duration - age);
                    return new SkillCountdown(
                        skill.Key,
                        remaining,
                        Math.Clamp(remaining / duration, 0, 1),
                        IsEstimated: true
                    );
                }
            )
            .ToArray();

    public void Reset()
    {
        for (int i = 0; i < states.Length; i++)
            states[i] = new State();
    }
}
