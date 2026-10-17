using System.Text.Json.Serialization;

namespace D3Vision.Core;

public readonly record struct MapPoint(double X, double Y);

public sealed record MapPolygon(MapPoint[] Points, bool CachedFootprint = false);

public sealed record RiftMapDrawing(IReadOnlyList<MapPolygon> Polygons, string Message)
{
    public MapPoint? Player { get; init; }
    public IReadOnlyList<MapMarker> Markers { get; init; } = [];
    public static RiftMapDrawing Waiting { get; } = new([], "Waiting for nearby map data");
}

public interface IMapDataSource
{
    RiftMapSnapshot Capture();
}

public sealed record RiftMapSnapshot(
    [property: JsonPropertyName("capturedUtc")] DateTimeOffset CapturedUtc,
    [property: JsonPropertyName("processId")] int ProcessId,
    [property: JsonPropertyName("fileVersion")] string FileVersion,
    [property: JsonPropertyName("executableSha256")] string ExecutableSha256,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("lastSlot")] int LastSlot,
    [property: JsonPropertyName("sceneCount")] int SceneCount,
    [property: JsonPropertyName("missingAssetSnos")] int[] MissingAssetSnos,
    [property: JsonPropertyName("scenes")] List<SceneRecord> Scenes,
    [property: JsonPropertyName("cachedScenes")] List<CachedScene> CachedScenes,
    [property: JsonPropertyName("cachedSceneCountAllWorlds")] int CachedSceneCountAllWorlds,
    [property: JsonPropertyName("assets")] List<SceneAsset> Assets
);

public sealed record CachedScene(
    int SceneSno,
    int SceneId,
    int WorldId,
    int TextureSno,
    float[] WorldBoundsXY
);

public sealed record SceneRecord(
    int Slot,
    int SceneId,
    int WorldId,
    int SceneSno,
    int CandidateFlags,
    string CandidateReferencePointer,
    float[] WorldBoundsXY,
    MapVector3 Origin,
    float[] RotationQuaternion
);

public sealed record SceneAsset(int SceneSno, List<NavCell> Cells);

public sealed record NavCell(MapVector3 Min, MapVector3 Max, ushort Flags);

public sealed record MapVector3(float X, float Y, float Z)
{
    public static MapVector3 Read(byte[] bytes, int offset)
    {
        var value = new MapVector3(
            BitConverter.ToSingle(bytes, offset),
            BitConverter.ToSingle(bytes, offset + 4),
            BitConverter.ToSingle(bytes, offset + 8)
        );
        if (
            !float.IsFinite(value.X)
            || !float.IsFinite(value.Y)
            || !float.IsFinite(value.Z)
            || Math.Abs(value.X) > 1_000_000
            || Math.Abs(value.Y) > 1_000_000
            || Math.Abs(value.Z) > 1_000_000
        )
            throw new InvalidOperationException("Navigation coordinate bounds failed validation.");
        return value;
    }
}
