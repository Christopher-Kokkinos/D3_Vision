using System.Numerics;
using D3Vision.Core;

namespace D3Vision.Services;

public static class RiftMapGeometry
{
    public static RiftMapDrawing Build(RiftMapSnapshot snapshot)
    {
        var worlds = snapshot.Scenes.Select(s => s.WorldId).Distinct().ToArray();
        if (worlds.Length != 1)
            return new([], "Waiting for a stable game area");
        var assets = snapshot.Assets.ToDictionary(a => a.SceneSno);
        var polygons = new List<MapPolygon>();
        foreach (var scene in snapshot.Scenes)
        {
            if (!assets.TryGetValue(scene.SceneSno, out var asset))
                continue;
            var q = scene.RotationQuaternion;
            var rotation = new Quaternion(q[0], q[1], q[2], q[3]);
            var origin = new Vector3(scene.Origin.X, scene.Origin.Y, scene.Origin.Z);
            foreach (var cell in asset.Cells)
            {
                if ((cell.Flags & 1) == 0)
                    continue;
                var a = cell.Min;
                var b = cell.Max;
                Vector3[] corners =
                [
                    new(a.X, a.Y, a.Z),
                    new(b.X, a.Y, a.Z),
                    new(b.X, b.Y, a.Z),
                    new(a.X, b.Y, a.Z),
                ];
                polygons.Add(
                    new(
                        corners
                            .Select(p => Project(Vector3.Transform(p, rotation) + origin))
                            .ToArray()
                    )
                );
            }
        }
        // Cached records do not contain a verified rotation. Show their extent
        // as an outline, never fabricate rotated walkable cells from them.
        var active = snapshot.Scenes.Select(s => s.SceneId).ToHashSet();
        foreach (
            var scene in snapshot.CachedScenes.Where(s =>
                s.WorldId == worlds[0] && !active.Contains(s.SceneId)
            )
        )
        {
            var b = scene.WorldBoundsXY;
            polygons.Add(
                new(
                    [
                        Project(new(b[0], b[1], 0)),
                        Project(new(b[2], b[1], 0)),
                        Project(new(b[2], b[3], 0)),
                        Project(new(b[0], b[3], 0)),
                    ],
                    true
                )
            );
        }
        return new(
            polygons,
            snapshot.MissingAssetSnos.Length == 0
                ? "Nearby geometry; outlines are cached areas"
                : "Some nearby map geometry is unavailable"
        );
    }

    public static MapPoint Project(Vector3 point) =>
        new((point.Y - point.X) * 0.7071067811865476, (point.X + point.Y) * 0.3535533905932738);
}
