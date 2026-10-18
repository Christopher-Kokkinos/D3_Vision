namespace D3Vision.Infrastructure;

internal static class InventoryReader
{
    public static IReadOnlyList<D3Vision.Core.BackpackItem> Items(
        IGameMemoryReader memory,
        nint moduleBase,
        D3Vision.Core.RiftMapSnapshot snapshot,
        IDictionary<int, string>? definitionNames = null
    )
    {
        int owner = BitConverter.ToInt32(
            PlayerPositionReader.ReadActor(memory, moduleBase, snapshot)
        );
        var root = memory.Pointer(moduleBase + 0x16F5250);
        var container = memory.Pointer(memory.Pointer(root + 0x7B8 + 0x198));
        var header = memory.Read(container, 0x170);
        int last = BitConverter.ToUInt16(header, 0x108),
            shift = BitConverter.ToInt32(header, 0x168);
        if (last == 65535)
            return [];
        if (last >= 8192 || shift is < 1 or > 10)
            throw new InvalidOperationException("Invalid inventory actor container.");
        var table = (nint)BitConverter.ToInt64(header, 0x120);
        var attributes = new GameAttributeReader(memory, root);
        var definitions = new ItemDefinitionReader(memory, moduleBase);
        var result = new List<D3Vision.Core.BackpackItem>();
        for (int first = 0; first <= last; first += 1 << shift)
        {
            var block = memory.Pointer(table + (first >> shift) * 8);
            var bytes = memory.Read(block, Math.Min(1 << shift, last - first + 1) * 0x3C0);
            for (int offset = 0; offset < bytes.Length; offset += 0x3C0)
            {
                int Int(int field) => BitConverter.ToInt32(bytes, offset + field);
                int id = Int(0);
                if (
                    id == -1
                    || (id & 65535) != first + offset / 0x3C0
                    || Int(0xB0) != 2
                    || Int(0x120) != owner
                    || Int(0x124) != 0
                )
                    continue;
                int column = Int(0x128),
                    row = Int(0x12C);
                if (column is < 0 or >= 10 || row is < 0 or >= 6)
                    throw new InvalidOperationException("Backpack coordinates failed validation.");
                uint? quality = attributes.Read(Int(0x130), 0xFFFFF17F);
                uint? ancient = attributes.Read(Int(0x130), 0xFFFFF19D);
                // Native Ancient_Rank descriptor ID 0x19D, packed with parameter -1.
                // A missing sparse entry has the verified native default 0.
                var rank = D3Vision.Core.InventorySelection.NativeRank(quality, ancient);
                var check = memory.Read(block + offset + 0x120, 16);
                if (
                    BitConverter.ToInt32(memory.Read(block + offset, 4)) != id
                    || BitConverter.ToInt32(check) != owner
                    || BitConverter.ToInt32(check, 4) != 0
                    || BitConverter.ToInt32(check, 8) != column
                    || BitConverter.ToInt32(check, 12) != row
                )
                    throw new InvalidOperationException("Backpack changed during capture.");
                int gbid = Int(0xB4);
                string name;
                if (definitionNames is null || !definitionNames.TryGetValue(gbid, out name!))
                {
                    name = definitions.Name(gbid);
                    if (definitionNames is not null && name.Length > 0)
                        definitionNames[gbid] = name;
                }
                result.Add(new(id, column, row, quality, rank, name));
            }
        }
        if (
            memory.Pointer(container + 0x120) != table
            || memory.Pointer(memory.Pointer(root + 0x7B8 + 0x198)) != container
            || BitConverter.ToInt32(PlayerPositionReader.ReadActor(memory, moduleBase, snapshot))
                != owner
        )
            throw new InvalidOperationException("Inventory owner changed during capture.");
        if (result.GroupBy(item => (item.Column, item.Row)).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Ambiguous backpack coordinates.");
        return result;
    }

    public static int Count(IGameMemoryReader memory, nint moduleBase)
    {
        var root = memory.Pointer(moduleBase + 0x16F5250);
        var container = memory.Pointer(memory.Pointer(root + 0x7B8 + 0x198));
        var header = memory.Read(container, 0x170);
        int last = BitConverter.ToUInt16(header, 0x108),
            shift = BitConverter.ToInt32(header, 0x168);
        if (last == 65535)
            return 0;
        if (last >= 8192 || shift is < 1 or > 10)
            throw new InvalidOperationException("Invalid inventory actor container.");
        var table = (nint)BitConverter.ToInt64(header, 0x120);
        int result = 0;
        for (int first = 0; first <= last; first += 1 << shift)
        {
            var block = memory.Pointer(table + (first >> shift) * 8);
            var bytes = memory.Read(block, Math.Min(1 << shift, last - first + 1) * 0x3C0);
            for (int offset = 0; offset < bytes.Length; offset += 0x3C0)
            {
                int id = BitConverter.ToInt32(bytes, offset);
                if (
                    id != -1
                    && (id & 65535) == first + offset / 0x3C0
                    && BitConverter.ToInt32(bytes, offset + 0xB0) == 2
                    && BitConverter.ToInt32(bytes, offset + 0x124) == 0
                )
                    result++;
            }
        }
        if (memory.Pointer(container + 0x120) != table)
            throw new InvalidOperationException("Inventory changed during capture.");
        return result;
    }
}
