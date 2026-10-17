using D3Vision.Core;

namespace D3Vision.Infrastructure;

internal static class ActorMarkerReader
{
    public static MapMarkerSnapshot Capture(
        IGameMemoryReader memory,
        nint moduleBase,
        RiftMapSnapshot snapshot,
        bool elites = true,
        bool loot = true
    )
    {
        var worlds = snapshot.Scenes.Select(s => s.WorldId).Distinct().ToArray();
        if (worlds.Length != 1 || (!elites && !loot))
            return new([], "Waiting for a stable marker area");
        var root = memory.Pointer(moduleBase + 0x16F5250);
        var container = memory.Pointer(memory.Pointer(root + 0x7B8 + 0x198));
        var header = memory.Read(container, 0x170);
        int last = BitConverter.ToUInt16(header, 0x108),
            shift = BitConverter.ToInt32(header, 0x168);
        if (last == 65535)
            return new([], "No active actors");
        if (last >= 8192 || shift is < 1 or > 10)
            throw new InvalidOperationException("Actor container bounds failed validation.");
        var table = (nint)BitConverter.ToInt64(header, 0x120);
        var attributes = new GameAttributeReader(memory, root);
        var items = new ItemDefinitionReader(memory, moduleBase);
        var markers = new List<MapMarker>();
        int pending = 0;
        for (int first = 0; first <= last; first += 1 << shift)
        {
            var block = memory.Pointer(table + (first >> shift) * 8);
            if (block == 0)
                throw new InvalidOperationException("Missing actor block.");
            int count = Math.Min(1 << shift, last - first + 1);
            var bytes = memory.Read(block, count * 0x3C0);
            for (int index = 0; index < count; index++)
            {
                int offset = index * 0x3C0;
                int Int(int field) => BitConverter.ToInt32(bytes, offset + field);
                int id = Int(0),
                    balanceType = Int(0xB0);
                if (id == -1 || (id & 65535) != first + index || Int(0x118) != worlds[0])
                    continue;
                MapMarkerKind? kind = elites
                    ? MapMarkerClassification.Elite(balanceType, Int(0xB8))
                    : null;
                if (kind is null && !(loot && balanceType == 2 && Int(0x124) == -1))
                    continue;
                int group = Int(0x130);
                if (group == -1)
                    continue;
                uint? deleted = attributes.Read(group, 0xFFFFF296);
                if (deleted is > 0)
                    continue;
                LootRank rank = LootRank.Normal;
                if (kind is not null)
                {
                    var hp = attributes.Read(group, 0xFFFFF073);
                    if (
                        hp is null
                        || !float.IsFinite(BitConverter.UInt32BitsToSingle(hp.Value))
                        || BitConverter.UInt32BitsToSingle(hp.Value) <= 0
                    )
                        continue;
                }
                else
                {
                    uint? quality = attributes.Read(group, 0xFFFFF17F);
                    if (quality != 9)
                        continue;
                    bool? isSet = items.IsSet(Int(0xB4));
                    if (isSet is null)
                    {
                        pending++;
                        continue;
                    }
                    uint ancient = attributes.Read(group, 0xFFFFF19D) ?? 0;
                    if (ancient > 2)
                    {
                        pending++;
                        continue;
                    }
                    rank = (LootRank)ancient;
                    kind = MapMarkerClassification.Loot(
                        balanceType,
                        Int(0x124),
                        (int)quality.Value,
                        isSet.Value
                    );
                }
                if (kind is null)
                    continue;
                var position = MapVector3.Read(bytes, offset + 0xD8);
                var address = block + offset;
                if (
                    BitConverter.ToInt32(memory.Read(address, 4)) != id
                    || BitConverter.ToInt32(memory.Read(address + 0x118, 4)) != worlds[0]
                    || (
                        balanceType == 2
                        && BitConverter.ToInt32(memory.Read(address + 0x124, 4)) != -1
                    )
                )
                    continue;
                markers.Add(new(id, new(position.X, position.Y), kind.Value, rank));
            }
        }
        if (
            memory.Pointer(memory.Pointer(root + 0x7B8 + 0x198)) != container
            || memory.Pointer(container + 0x120) != table
        )
            throw new InvalidOperationException("Actor container changed during capture.");
        return new(
            markers,
            pending == 0
                ? $"{markers.Count} elite/loot markers"
                : $"{markers.Count} markers; {pending} item definitions pending"
        );
    }
}
