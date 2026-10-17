using D3Vision.Core;
using D3Vision.Infrastructure;
using D3Vision.Services;

internal static class RiftMapChecks
{
    public static int Run()
    {
        int count = 0;
        void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
            count++;
        }
        var scene = new SceneRecord(
            0,
            10,
            20,
            30,
            0,
            "0",
            [10, 20, 12, 24],
            new(10, 20, 0),
            [0, 0, 0, 1]
        );
        var snapshot = new RiftMapSnapshot(
            DateTimeOffset.UtcNow,
            1,
            "test",
            "",
            "",
            0,
            1,
            [],
            [scene],
            [
                new(30, 10, 20, 0, [10, 20, 12, 24]),
                new(30, 11, 20, 0, [12, 24, 14, 28]),
                new(30, 12, 21, 0, [100, 100, 200, 200]),
            ],
            3,
            [new(30, [new(new(0, 0, 0), new(2, 4, 0), 1), new(new(0, 0, 0), new(2, 4, 0), 2)])]
        );
        var drawing = RiftMapGeometry.Build(snapshot);
        Check(
            drawing.Polygons.Count == 2
                && !drawing.Polygons[0].CachedFootprint
                && drawing.Polygons[1].CachedFootprint,
            "Map excludes non-walkable cells, duplicate cache entries and other worlds"
        );
        var point = drawing.Polygons[0].Points[0];
        var expected = RiftMapGeometry.Project(new(10, 20, 0));
        Check(
            Math.Abs(point.X - expected.X) < 0.001 && Math.Abs(point.Y - expected.Y) < 0.001,
            "Map applies the scene origin before projection"
        );
        var rotated = scene with { RotationQuaternion = [0, 0, MathF.Sqrt(.5f), MathF.Sqrt(.5f)] };
        var rotatedDrawing = RiftMapGeometry.Build(
            snapshot with
            {
                Scenes = [rotated],
                CachedScenes = [],
            }
        );
        expected = RiftMapGeometry.Project(new(10, 22, 0));
        point = rotatedDrawing.Polygons[0].Points[1];
        Check(
            Math.Abs(point.X - expected.X) < .001 && Math.Abs(point.Y - expected.Y) < .001,
            "Map rotates local navigation cells before translation"
        );
        Check(
            RiftMapGeometry
                .Build(snapshot with { Scenes = [scene, scene with { WorldId = 999 }] })
                .Polygons.Count == 0,
            "Map does not mix floor geometry during a world transition"
        );
        Check(
            RiftMapGeometry
                .Build(snapshot with { Assets = [], CachedScenes = [], MissingAssetSnos = [30] })
                .Polygons.Count == 0,
            "Missing assets must not fabricate walkable geometry"
        );

        var old = new Settings { HudLayoutVersion = HudLayout.CurrentVersion };
        old.Widgets.RemoveAll(w => w.Kind == WidgetKind.RiftMap);
        old.Widgets[0].X = 321;
        HudLayout.Upgrade(old);
        var map = old.Widgets.Single(w => w.Kind == WidgetKind.RiftMap);
        Check(
            !map.Enabled && map.Width == 320 && old.Widgets[0].X == 321,
            "Map migration is opt-in and preserves existing positions"
        );
        map.Enabled = true;
        map.X = -70;
        HudLayout.Upgrade(old);
        Check(map.Enabled && map.X == -70, "Map reload preserves activation and placement");

        Check(
            SceneRevealCache.Read(CacheMemory.Create(), CacheMemory.Module).Single().SceneSno
                == 123,
            "Reveal cache traverses a valid linked record"
        );
        foreach (
            string invalid in new[] { "cycle", "bounds", "magic", "count", "previous", "truncated" }
        )
        {
            bool rejected = false;
            try
            {
                SceneRevealCache.Read(CacheMemory.Create(invalid), CacheMemory.Module);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.IO.IOException)
            {
                rejected = true;
            }
            Check(rejected, $"Reveal cache rejects {invalid} data");
        }
        Check(
            SceneRevealCache.Read(CacheMemory.Create("empty"), CacheMemory.Module).Count == 0,
            "An empty reveal cache yields no geometry"
        );
        var viewport = MinimapProjection.Viewport(1920, 1080);
        Check(
            Math.Abs(viewport.Left - 1579.7) < .001
                && Math.Abs(viewport.Top - 48.6) < .001
                && viewport.Size == 315,
            "Native minimap matches the measured 1080p client layout"
        );
        var playerPoint = RiftMapGeometry.Project(new(10, 20, 0));
        var anchored = MinimapProjection.Position(playerPoint, new(10, 20), 315);
        Check(
            Math.Abs(anchored.X - 157.5) < .001 && Math.Abs(anchored.Y - 157.5) < .001,
            "Player remains centered without fitting map extents"
        );
        var moved = MinimapProjection.Position(playerPoint, new(12, 20), 315);
        Check(
            moved.X > anchored.X && moved.Y < anchored.Y,
            "Map follows player movement using native axes"
        );
        var at1440 = MinimapProjection.Viewport(2560, 1440);
        Check(
            Math.Abs(at1440.Size / viewport.Size - 4d / 3) < .001,
            "Minimap scales with client height"
        );
        Check(
            MinimapProjection.Position(playerPoint, new(10, 20), 315, double.NaN) == anchored,
            "Invalid calibration uses a finite default"
        );
        var validPlayer = PlayerMemory.Create();
        Check(
            PlayerPositionReader.Read(validPlayer, CacheMemory.Module, snapshot)
                == new MapPoint(11, 22),
            "Player actor is resolved through the native container"
        );
        foreach (var invalid in new[] { "id", "world", "index", "shift", "position" })
        {
            bool rejected = false;
            try
            {
                PlayerPositionReader.Read(
                    PlayerMemory.Create(invalid),
                    CacheMemory.Module,
                    snapshot
                );
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            Check(rejected, $"Player sampler rejects invalid {invalid}");
        }
        return count;
    }

    private sealed class PlayerMemory : IGameMemoryReader
    {
        readonly Dictionary<nint, byte[]> blocks = [];

        public static PlayerMemory Create(string invalid = "")
        {
            var m = new PlayerMemory();
            void Put(nint a, long value) => m.blocks[a] = BitConverter.GetBytes(value);
            Put(CacheMemory.Module + 0x16F5250, 0x20000000);
            Put(0x20000B28, 0x30000000);
            Put(0x30000000, invalid == "index" ? -1 : 0);
            Put(0x200008F8, 0x40000000);
            Put(0x40000064, 0x10002);
            Put(0x20000950, 0x50000000);
            Put(0x50000000, 0x60000000);
            Put(0x60000168, invalid == "shift" ? 32 : 8);
            Put(0x60000120, 0x70000000);
            Put(0x70000000, 0x71000000);
            var data = new byte[0x120];
            BitConverter.GetBytes(invalid == "id" ? 7 : 0x10002).CopyTo(data, 0);
            BitConverter.GetBytes(invalid == "world" ? 999 : 20).CopyTo(data, 0x118);
            BitConverter.GetBytes(invalid == "position" ? float.NaN : 11f).CopyTo(data, 0xD8);
            BitConverter.GetBytes(22f).CopyTo(data, 0xDC);
            m.blocks[0x71000000 + 2 * 0x3C0] = data;
            return m;
        }

        public byte[] Read(nint address, int count) => blocks[address][..count];

        public nint Pointer(nint address) => (nint)BitConverter.ToInt64(Read(address, 8));
    }

    private sealed class CacheMemory : IGameMemoryReader
    {
        public static readonly nint Module = unchecked((nint)0x140000000);
        private readonly Dictionary<nint, byte[]> blocks = [];

        public static CacheMemory Create(string invalid = "")
        {
            var memory = new CacheMemory();
            nint area = 0x20000000,
                node = 0x30000000;
            memory.blocks[Module + 0x1672A68] = BitConverter.GetBytes((long)area);
            var header = new byte[0x50];
            BitConverter.GetBytes((long)node).CopyTo(header, 0);
            BitConverter.GetBytes((long)node).CopyTo(header, 8);
            BitConverter.GetBytes(invalid == "count" ? 5000 : 1).CopyTo(header, 0x10);
            BitConverter.GetBytes(88).CopyTo(header, 0x20);
            BitConverter.GetBytes(invalid == "magic" ? 0 : 0x600DF00D).CopyTo(header, 0x48);
            if (invalid == "empty")
            {
                Array.Clear(header, 0, 0x20);
            }
            memory.blocks[area + 0x20] = header;
            var record = new byte[88];
            BitConverter.GetBytes(123).CopyTo(record, 0);
            BitConverter.GetBytes(10).CopyTo(record, 4);
            BitConverter.GetBytes(20).CopyTo(record, 8);
            BitConverter.GetBytes(240f).CopyTo(record, 24);
            BitConverter.GetBytes(240f).CopyTo(record, 28);
            if (invalid == "bounds")
                BitConverter.GetBytes(float.NaN).CopyTo(record, 16);
            if (invalid == "cycle")
                BitConverter.GetBytes((long)node).CopyTo(record, 80);
            if (invalid == "previous")
                BitConverter.GetBytes((long)node).CopyTo(record, 72);
            memory.blocks[node] = invalid == "truncated" ? record[..80] : record;
            return memory;
        }

        public byte[] Read(nint address, int count)
        {
            foreach (var (start, bytes) in blocks)
                if (address >= start && address - start + count <= bytes.Length)
                    return bytes.AsSpan((int)(address - start), count).ToArray();
            throw new System.IO.IOException("Synthetic read outside a mapped test region");
        }

        public nint Pointer(nint address) => (nint)BitConverter.ToInt64(Read(address, 8));
    }
}
