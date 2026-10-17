using System.Diagnostics;
using System.Security.Cryptography;
using D3Vision.Core;

namespace D3Vision.Infrastructure;

/// <summary>Read-only scene geometry source for the explicitly validated game executable.</summary>
public sealed class DiabloMapReader : IMapDataSource
{
    private const string SupportedHash =
        "753D5F79D12B035B7268FDB255B5B2502EC858D149B6EBB62969A6E92DD23CC4";
    private const int RootPointerRva = 0x16F5250;
    private const int SceneContainerOffset = 0xAA0;
    private const int SceneStride = 0x7B8;
    private const int MaxSlots = 8192;

    private readonly object sync = new();
    private int checkedProcessId;
    private long checkedStartTime;

    public RiftMapSnapshot Capture()
    {
        lock (sync)
            return CaptureCore();
    }

    public MapPoint ReadPlayer(RiftMapSnapshot snapshot)
    {
        lock (sync)
        {
            using var process = Process.GetProcessById(snapshot.ProcessId);
            if (
                checkedProcessId != process.Id
                || checkedStartTime != process.StartTime.ToUniversalTime().Ticks
            )
                throw new InvalidOperationException(
                    "Player sample belongs to a different game session."
                );
            using var m = new ReadOnlyProcess(process.Id);
            return PlayerPositionReader.Read(m, process.MainModule!.BaseAddress, snapshot);
        }
    }

    public MapMarkerSnapshot ReadMarkers(
        RiftMapSnapshot snapshot,
        bool elites = true,
        bool loot = true
    )
    {
        lock (sync)
        {
            using var process = Process.GetProcessById(snapshot.ProcessId);
            if (
                checkedProcessId != process.Id
                || checkedStartTime != process.StartTime.ToUniversalTime().Ticks
            )
                throw new InvalidOperationException(
                    "Marker sample belongs to a different game session."
                );
            using var memory = new ReadOnlyProcess(process.Id);
            return ActorMarkerReader.Capture(
                memory,
                process.MainModule!.BaseAddress,
                snapshot,
                elites,
                loot
            );
        }
    }

    public int ReadInventoryCount(RiftMapSnapshot snapshot)
    {
        lock (sync)
        {
            using var process = Process.GetProcessById(snapshot.ProcessId);
            if (
                checkedProcessId != process.Id
                || checkedStartTime != process.StartTime.ToUniversalTime().Ticks
            )
                throw new InvalidOperationException(
                    "Inventory sample belongs to another game session."
                );
            using var memory = new ReadOnlyProcess(process.Id);
            return InventoryReader.Count(memory, process.MainModule!.BaseAddress);
        }
    }

    public HeroClass ReadHeroClass(RiftMapSnapshot snapshot)
    {
        lock (sync)
        {
            using var process = Process.GetProcessById(snapshot.ProcessId);
            if (
                checkedProcessId != process.Id
                || checkedStartTime != process.StartTime.ToUniversalTime().Ticks
            )
                throw new InvalidOperationException("Hero sample belongs to another game session.");
            using var memory = new ReadOnlyProcess(process.Id);
            return PlayerPositionReader.ReadClass(
                memory,
                process.MainModule!.BaseAddress,
                snapshot
            );
        }
    }

    private RiftMapSnapshot CaptureCore()
    {
        using var process =
            Process.GetProcessesByName("Diablo III64").SingleOrDefault()
            ?? throw new InvalidOperationException(
                "Exactly one Diablo III64 process must be running."
            );
        var module =
            process.MainModule
            ?? throw new InvalidOperationException("Cannot locate the game module.");
        long startTime = process.StartTime.ToUniversalTime().Ticks;
        if (checkedProcessId != process.Id || checkedStartTime != startTime)
        {
            using var file = File.OpenRead(module.FileName);
            if (Convert.ToHexString(SHA256.HashData(file)) != SupportedHash)
                throw new InvalidOperationException("Map reader does not support this game build.");
            checkedProcessId = process.Id;
            checkedStartTime = startTime;
        }

        using var memory = new ReadOnlyProcess(process.Id);
        // Verify the critical iterator instruction also matches the live image.
        byte[] expected = [0x48, 0x8B, 0x15, 0x99, 0x17, 0xF8, 0x00];
        if (!memory.Read(module.BaseAddress + 0x773AB0, expected.Length).SequenceEqual(expected))
            throw new InvalidOperationException(
                "The live scene iterator does not match the inspected binary."
            );

        var root = memory.Pointer(module.BaseAddress + RootPointerRva);
        var container = memory.Pointer(root + SceneContainerOffset);
        if (container == 0)
            throw new InvalidOperationException(
                "Scene container is not available in the current game state."
            );
        var header = memory.Read(container, 0x128);
        int lastSlot = BitConverter.ToUInt16(header, 0x108);
        var array = (nint)BitConverter.ToInt64(header, 0x120);
        if (lastSlot != ushort.MaxValue && (lastSlot >= MaxSlots || array == 0))
            throw new InvalidOperationException("Scene container bounds failed validation.");

        var scenes = new List<SceneRecord>();
        var assetGroup = memory.Pointer(module.BaseAddress + 0x1708970);
        var assetContainer = memory.Pointer(assetGroup + 0x20);
        var assetHeader = memory.Read(assetContainer, 0x128);
        if (lastSlot != ushort.MaxValue)
        {
            for (int slot = 0; slot <= lastSlot; slot++)
            {
                var bytes = memory.Read(array + slot * SceneStride, SceneStride);
                if (BitConverter.ToInt32(bytes) == -1)
                    continue;
                // Engine iterator returns the record + 8. The scene diagnostic
                // reads ID at +0, world at +4 and SceneSNO at +E4 from that pointer.
                float[] bounds = Enumerable
                    .Range(0, 4)
                    .Select(i => BitConverter.ToSingle(bytes, 0x170 + i * 4))
                    .ToArray();
                float[] rotation = Enumerable
                    .Range(0, 4)
                    .Select(i => BitConverter.ToSingle(bytes, 0xF0 + i * 4))
                    .ToArray();
                if (
                    bounds.Any(v => !float.IsFinite(v) || Math.Abs(v) > 1_000_000)
                    || bounds[0] > bounds[2]
                    || bounds[1] > bounds[3]
                    || rotation.Any(v => !float.IsFinite(v))
                    || Math.Abs(rotation.Sum(v => v * v) - 1) > 0.01
                )
                    throw new InvalidOperationException("Scene transform failed validation.");
                scenes.Add(
                    new SceneRecord(
                        slot,
                        BitConverter.ToInt32(bytes, 8),
                        BitConverter.ToInt32(bytes, 12),
                        BitConverter.ToInt32(bytes, 0xEC),
                        BitConverter.ToInt32(bytes, 0x1B0),
                        $"0x{BitConverter.ToUInt64(bytes, 0x190):X}",
                        bounds,
                        MapVector3.Read(bytes, 0x100),
                        rotation
                    )
                );
            }
        }
        var allCachedScenes = SceneRevealCache.Read(memory, module.BaseAddress);
        var activeWorlds = scenes.Select(s => s.WorldId).ToHashSet();
        var cachedScenes = allCachedScenes.Where(s => activeWorlds.Contains(s.WorldId)).ToList();
        int assetLastSlot = BitConverter.ToInt32(assetHeader, 0x108) & 0x3FFFF;
        if (assetLastSlot >= 32768)
            throw new InvalidOperationException("Scene asset table bounds failed validation.");
        var assetTable = memory.Read(
            (nint)BitConverter.ToInt64(assetHeader, 0x120),
            (assetLastSlot + 1) * 24
        );
        var wantedSnos = scenes
            .Select(s => s.SceneSno)
            .Concat(cachedScenes.Select(s => s.SceneSno))
            .ToHashSet();
        int snoLimit = BitConverter.ToInt32(memory.Read(module.BaseAddress + 0x1704C48, 4));
        if (snoLimit is <= 0 or > 10_000_000)
            throw new InvalidOperationException("Asset lookup bounds failed validation.");
        var snoLookup = memory.Pointer(module.BaseAddress + 0x1704C50);
        var assets = new List<SceneAsset>();
        foreach (int sno in wantedSnos)
        {
            if (sno < 0 || sno >= snoLimit)
                continue;
            int handle = BitConverter.ToInt32(memory.Read(snoLookup + sno * 4, 4));
            if (handle == -1)
                continue;
            int i = handle & 0x3FFFF;
            if (i > assetLastSlot)
                continue;
            if (BitConverter.ToInt32(assetTable, i * 24) != handle)
                throw new InvalidOperationException(
                    "Scene asset slot identity changed during capture."
                );
            var address = (nint)BitConverter.ToInt64(assetTable, i * 24 + 16);
            var asset = memory.Read(address, 0x200);
            if (BitConverter.ToInt32(asset) != sno)
                throw new InvalidOperationException("Scene asset identity failed validation.");
            int cellCount = BitConverter.ToInt32(asset, 0x180);
            int cellByteLength = BitConverter.ToInt32(asset, 0x194);
            if (cellCount < 0 || cellCount > 32768 || cellByteLength != cellCount * 32)
                throw new InvalidOperationException(
                    "Navigation cell array bounds failed validation."
                );
            var cells = new List<NavCell>();
            if (cellCount > 0)
            {
                var cellData = memory.Read(
                    (nint)BitConverter.ToInt64(asset, 0x188),
                    cellByteLength
                );
                for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
                {
                    int offset = cellIndex * 32;
                    var min = MapVector3.Read(cellData, offset);
                    var max = MapVector3.Read(cellData, offset + 12);
                    if (min.X > max.X || min.Y > max.Y || min.Z > max.Z)
                        throw new InvalidOperationException("Navigation cell bounds are inverted.");
                    cells.Add(new NavCell(min, max, BitConverter.ToUInt16(cellData, offset + 24)));
                }
            }
            if (!memory.Read(address + 0x180, 24).SequenceEqual(asset[0x180..0x198]))
                throw new InvalidOperationException(
                    "Navigation cell array changed during capture."
                );
            assets.Add(new SceneAsset(sno, cells));
        }
        foreach (var scene in scenes)
        {
            var identity = memory.Read(array + scene.Slot * SceneStride + 8, 8);
            if (
                BitConverter.ToInt32(identity) != scene.SceneId
                || BitConverter.ToInt32(identity, 4) != scene.WorldId
                || BitConverter.ToInt32(memory.Read(array + scene.Slot * SceneStride + 0xEC, 4))
                    != scene.SceneSno
            )
                throw new InvalidOperationException(
                    "A scene slot changed during capture; retry when loading has finished."
                );
        }
        // Reject snapshots spanning a container replacement or slot range change.
        if (
            memory.Pointer(module.BaseAddress + RootPointerRva) != root
            || memory.Pointer(root + SceneContainerOffset) != container
            || !memory.Read(container + 0x108, 2).SequenceEqual(header[0x108..0x10A])
            || memory.Pointer(container + 0x120) != array
            || memory.Pointer(module.BaseAddress + 0x1708970) != assetGroup
            || memory.Pointer(assetGroup + 0x20) != assetContainer
            || memory.Pointer(assetContainer + 0x120)
                != (nint)BitConverter.ToInt64(assetHeader, 0x120)
            || memory.Pointer(module.BaseAddress + 0x1704C50) != snoLookup
        )
            throw new InvalidOperationException(
                "Scenes changed during capture; retry when loading has finished."
            );

        return new RiftMapSnapshot(
            DateTimeOffset.UtcNow,
            process.Id,
            module.FileVersionInfo.FileVersion ?? "unknown",
            SupportedHash,
            "Nearby and cached scenes; complete unexplored floor coverage is not verified.",
            lastSlot,
            scenes.Count,
            wantedSnos.Except(assets.Select(a => a.SceneSno)).ToArray(),
            scenes,
            cachedScenes,
            allCachedScenes.Count,
            assets
        );
    }
}
