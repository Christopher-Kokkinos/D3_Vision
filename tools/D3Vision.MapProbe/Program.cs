using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
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
                    )
            )
    )
        throw new ArgumentException(
            "Usage: D3Vision.MapProbe <output.json> [--contexts] [--map-cache] [--alignment] [--markers] [--elite-diagnostics]"
        );
    var reader = new DiabloMapReader();
    var snapshot = reader.Capture();
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
