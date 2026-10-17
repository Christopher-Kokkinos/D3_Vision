using D3Vision.Core;
using D3Vision.Infrastructure;

internal static class AlignmentDiagnostics
{
    public static void Print(
        ReadOnlyProcess memory,
        nint moduleBase,
        string executable,
        RiftMapSnapshot snapshot
    )
    {
        var position = PlayerPositionReader.Read(memory, moduleBase, snapshot);
        Console.WriteLine(
            $"Validated local player world position: ({position.X:F3}, {position.Y:F3})"
        );
        Console.WriteLine(
            $"Native 1920x1080 minimap viewport: {MinimapProjection.Viewport(1920, 1080)}"
        );
    }
}
