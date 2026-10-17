namespace D3Vision.Infrastructure;

/// <summary>Resolves set membership from the game's loaded item balance definition.</summary>
internal sealed class ItemDefinitionReader(IGameMemoryReader memory, nint moduleBase)
{
    readonly Dictionary<int, bool> cache = [];

    public bool? IsSet(int gbid)
    {
        if (cache.TryGetValue(gbid, out bool cached))
            return cached;
        uint hash = 2166136261;
        foreach (byte value in BitConverter.GetBytes(gbid))
            hash = unchecked((hash ^ value) * 16777619);
        var manager = memory.Pointer(moduleBase + 0x16F64C0);
        var map = memory.Pointer(manager) + 2 * 128;
        int mask = BitConverter.ToInt32(memory.Read(map, 4));
        if (mask is < 0 or > 1048575 || (mask & (mask + 1)) != 0)
            throw new InvalidOperationException("Invalid balance hash table.");
        var buckets = memory.Pointer(map + 0x10);
        var node = memory.Pointer(buckets + (int)(hash & (uint)mask) * 8);
        var visited = new HashSet<nint>();
        while (node != 0)
        {
            if (visited.Count >= 128 || !visited.Add(node))
                throw new InvalidOperationException("Invalid item balance chain.");
            var entry = memory.Read(node, 20);
            if (BitConverter.ToInt32(entry, 8) == gbid)
            {
                int sno = BitConverter.ToInt32(entry, 12),
                    row = BitConverter.ToInt32(entry, 16);
                int count = BitConverter.ToInt32(memory.Read(moduleBase + 0x1704C48, 4));
                if (sno < 0 || sno >= count || row is < 0 or > 100000)
                    throw new InvalidOperationException("Invalid item definition index.");
                var lookup = memory.Pointer(moduleBase + 0x1704C50);
                int handle = BitConverter.ToInt32(memory.Read(lookup + sno * 4, 4));
                if (handle == -1)
                    return null;
                var group = memory.Pointer(moduleBase + 0x1714180);
                var container = memory.Pointer(group + 0x20);
                var header = memory.Read(container, 0x128);
                int slot = handle & 0x3FFFF;
                if (slot > (BitConverter.ToInt32(header, 0x108) & 0x3FFFF))
                    throw new InvalidOperationException("Invalid item asset slot.");
                var table = (nint)BitConverter.ToInt64(header, 0x120);
                var asset = memory.Pointer(table + slot * 24 + 16);
                var assetHeader = memory.Read(asset, 0x40);
                if (
                    BitConverter.ToInt32(assetHeader) != sno
                    || BitConverter.ToInt32(assetHeader, 12) != 2
                )
                    throw new InvalidOperationException(
                        "Item balance asset identity failed validation."
                    );
                int byteLength = BitConverter.ToInt32(assetHeader, 0x2C);
                int rows = byteLength / 0x580;
                if (byteLength < 0 || byteLength % 0x580 != 0 || rows <= row || rows > 100000)
                    throw new InvalidOperationException("Item balance row is outside its asset.");
                var address = (nint)BitConverter.ToInt64(assetHeader, 0x30) + row * 0x580;
                var definition = memory.Read(address, 0x178);
                if (BitConverter.ToInt32(definition, 0x100) != gbid)
                    throw new InvalidOperationException(
                        "Item definition identity failed validation."
                    );
                int setId = BitConverter.ToInt32(definition, 0x174);
                bool result = setId != 0 && setId != -1;
                if (memory.Pointer(table + slot * 24 + 16) != asset)
                    throw new InvalidOperationException("Item asset changed during capture.");
                cache[gbid] = result;
                return result;
            }
            node = (nint)BitConverter.ToInt64(entry);
        }
        return null;
    }
}
