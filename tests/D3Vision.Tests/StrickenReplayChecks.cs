using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using D3Vision.Core;

internal static class StrickenReplayChecks
{
    // Normalized live input and recorded estimates, not authoritative enemy stack counts.
    sealed record Sample(
        [property: JsonPropertyName("elapsedMs")] long ElapsedMs,
        [property: JsonPropertyName("bossPresent")] bool BossPresent,
        [property: JsonPropertyName("proc")] bool Proc,
        [property: JsonPropertyName("expected")] int? Expected
    );

    public static int Run()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "solo-stricken-procs.json");
        var samples =
            JsonSerializer.Deserialize<Sample[]>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("Missing solo Stricken capture.");
        var tracker = new StrickenProcTracker();
        var actual = new List<int?>();
        foreach (var sample in samples)
        {
            var player = new PlayerBuffState(
                0,
                1,
                HeroClass.Barbarian,
                null,
                false,
                true,
                null,
                true,
                sample.Proc,
                sample.BossPresent ? 2 : null
            );
            tracker.Apply(new(1, 1, 1000 + sample.ElapsedMs, [player], "Normalized solo capture"));
            actual.Add(tracker.Count(0));
        }
        if (!actual.SequenceEqual(samples.Select(s => s.Expected)))
            throw new Exception("Live solo Stricken replay changed its recorded proc estimates.");
        if (actual.Max() != 16)
            throw new Exception(
                "Live solo sequence must reproduce the observed peak estimate of sixteen."
            );
        if (actual[^1] is not null || !samples.Any(s => !s.BossPresent && s.Proc))
            throw new Exception(
                "Counter must clear when the boss disappears, even while a proc flag lingers."
            );
        return 3;
    }
}
