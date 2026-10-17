using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using D3Vision.Core;
using D3Vision.Infrastructure;

try
{
    if (
        args.Length < 1
        || args.Skip(1)
            .Any(a =>
                a
                    is not (
                        "--contexts"
                        or "--map-cache"
                        or "--alignment"
                        or "--markers"
                        or "--elite-diagnostics"
                        or "--buffs"
                        or "--buff-details"
                        or "--buff-watch"
                        or "--record-buffs"
                    )
            )
    )
        throw new ArgumentException(
            "Usage: D3Vision.MapProbe <output.json> [--contexts] [--map-cache] [--alignment] [--markers] [--elite-diagnostics] [--buffs] [--buff-details] [--buff-watch] [--record-buffs]"
        );
    var reader = new DiabloMapReader();
    if (args.Contains("--record-buffs"))
    {
        await BuffRecording.RecordAsync(reader, args[0]);
        return 0;
    }
    var snapshot = reader.Capture();
    if (args.Contains("--buff-watch"))
    {
        string last = "";
        var tracker = new StrickenProcTracker();
        for (int i = 0; i < 200; i++)
        {
            string state;
            try
            {
                if (i % 10 == 0)
                    snapshot = reader.Capture();
                var buffs = reader.ReadBuffs(snapshot);
                tracker.Apply(buffs);
                state = string.Join(
                    " | ",
                    buffs.Players.Select(p =>
                        $"slot={p.PlayerSlot}{(p.PlayerSlot == buffs.LocalPlayerSlot ? " (local)" : "")} {p.Class} CoE={p.ActiveElement} Squirt={p.SquirtsStacks} worn={p.SquirtsEquipped} Stricken={p.StrickenEquipped} proc={p.StrickenProc} boss={p.BossActorId} observed={tracker.Count(p.PlayerSlot)}"
                    )
                );
            }
            catch (Exception ex)
                when (ex
                        is Win32Exception
                            or IOException
                            or InvalidOperationException
                            or ArgumentException
                )
            {
                tracker.Apply(CombatBuffSnapshot.Waiting);
                state = "Waiting for stable buff world: " + ex.Message;
            }
            if (state != last)
            {
                Console.WriteLine(state);
                last = state;
            }
            await Task.Delay(100);
        }
    }
    if (args.Contains("--buffs"))
        Console.WriteLine(
            JsonSerializer.Serialize(
                reader.ReadBuffs(snapshot),
                new JsonSerializerOptions { WriteIndented = true }
            )
        );
    if (args.Contains("--markers"))
        Console.WriteLine(
            JsonSerializer.Serialize(
                reader.ReadMarkers(snapshot),
                new JsonSerializerOptions { WriteIndented = true }
            )
        );
    if (args.Length > 1)
    {
        using var process = Process.GetProcessById(snapshot.ProcessId);
        var module =
            process.MainModule ?? throw new InvalidOperationException("Game module unavailable.");
        using var memory = new ReadOnlyProcess(process.Id);
        if (args.Contains("--buff-details"))
            BuffDiagnostics.Print(memory, module.BaseAddress, snapshot);
        if (args.Contains("--elite-diagnostics"))
            EliteDiagnostics.Print(memory, module.BaseAddress, snapshot);
        if (args.Contains("--alignment"))
            AlignmentDiagnostics.Print(memory, module.BaseAddress, module.FileName, snapshot);
        if (args.Contains("--contexts"))
            ContextDiagnostics.Print(process, memory, module.BaseAddress);
        if (args.Contains("--map-cache"))
            MapCacheDiagnostics.Print(memory, module.BaseAddress, module.FileName);
    }
    string output = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    File.WriteAllText(
        output,
        JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true })
    );
    Console.WriteLine(
        $"Read {snapshot.SceneCount} active scenes, {snapshot.CachedScenes.Count} cached scenes in active worlds, and {snapshot.Assets.Sum(a => a.Cells.Count)} cells. Saved {output}"
    );
    return 0;
}
catch (Exception ex)
    when (ex is Win32Exception or IOException or InvalidOperationException or ArgumentException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
