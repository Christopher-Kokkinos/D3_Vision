using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using D3Vision.Core;
using D3Vision.Infrastructure;

internal static class BuffRecording
{
    // Explicit diagnostic mode: five minutes, at most 16 MB, no input or writes to the game.
    public static async Task RecordAsync(DiabloMapReader reader, string output)
    {
        string path = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read
        );
        using var writer = new StreamWriter(stream) { AutoFlush = true };
        var tracker = new StrickenProcTracker();
        RiftMapSnapshot? snapshot = null;
        MapMarkerSnapshot elites = new([], "");
        IReadOnlyList<CombatActorSample> actors = [];
        long started = Environment.TickCount64,
            nextScene = 0,
            nextMarkers = 0;
        int samples = 0;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        Console.WriteLine($"Recording buffs for up to five minutes: {path}");
        while (
            Environment.TickCount64 - started < 300000
            && stream.Length < 16 * 1024 * 1024
            && await timer.WaitForNextTickAsync()
        )
        {
            try
            {
                if (snapshot is null || Environment.TickCount64 >= nextScene)
                {
                    snapshot = reader.Capture();
                    nextScene = Environment.TickCount64 + 1000;
                }
                var buffs = reader.ReadBuffs(snapshot);
                tracker.Apply(buffs);
                if (Environment.TickCount64 >= nextMarkers)
                {
                    elites = reader.ReadMarkers(snapshot, elites: true, loot: false);
                    using var process = Process.GetProcessById(buffs.ProcessId);
                    using var memory = new ReadOnlyProcess(process.Id);
                    actors = CombatActorDiagnostics.Capture(
                        memory,
                        process.MainModule!.BaseAddress,
                        buffs
                    );
                    nextMarkers = Environment.TickCount64 + 200;
                }
                await writer.WriteLineAsync(
                    JsonSerializer.Serialize(
                        new
                        {
                            Utc = DateTimeOffset.UtcNow,
                            Buffs = buffs,
                            Counters = buffs.Players.Select(p => new
                            {
                                p.PlayerSlot,
                                Observed = tracker.Count(p.PlayerSlot),
                            }),
                            Elites = elites.Markers.Take(32),
                            Actors = actors,
                        }
                    )
                );
                samples++;
            }
            catch (Exception ex)
                when (ex
                        is Win32Exception
                            or IOException
                            or InvalidOperationException
                            or UnauthorizedAccessException
                            or ArgumentException
                )
            {
                tracker.Apply(CombatBuffSnapshot.Waiting);
                snapshot = null;
                nextMarkers = 0;
                await writer.WriteLineAsync(
                    JsonSerializer.Serialize(
                        new { Utc = DateTimeOffset.UtcNow, Waiting = ex.Message }
                    )
                );
                await Task.Delay(500);
            }
        }
        Console.WriteLine($"Recording complete: {samples} samples, {stream.Length} bytes.");
    }
}
