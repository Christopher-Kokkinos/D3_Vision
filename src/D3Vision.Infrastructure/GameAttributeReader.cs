using D3Vision.Core;

namespace D3Vision.Infrastructure;

/// <summary>Bounded read-only lookup matching the inspected native attribute map.</summary>
internal sealed class GameAttributeReader(IGameMemoryReader memory, nint root)
{
    public uint? Read(int groupId, uint key)
    {
        if (groupId == -1)
            return null;
        var container = memory.Pointer(memory.Pointer(root + 0x7B8 + 0x180) + 0x90);
        var header = memory.Read(container, 0x170);
        int shift = BitConverter.ToInt32(header, 0x168);
        if (shift is < 1 or > 16)
            throw new InvalidOperationException("Invalid attribute container shift.");
        int index = groupId & 65535;
        int last = BitConverter.ToUInt16(header, 0x108);
        if (last == 65535 || index > last)
            throw new InvalidOperationException("Attribute group index is outside its container.");
        var table = (nint)BitConverter.ToInt64(header, 0x120);
        var group =
            memory.Pointer(table + (index >> shift) * 8) + (index & ((1 << shift) - 1)) * 0x12E8;
        var data = memory.Read(group, 0x30);
        if (BitConverter.ToInt32(data) != groupId)
            throw new InvalidOperationException("Attribute group identity changed.");
        int flags = BitConverter.ToInt32(data, 4);
        bool external = (flags & 4) != 0;
        nint buckets = external
            ? memory.Pointer(memory.Pointer(group + 0x10) + 0x10)
            : memory.Pointer(group + 0x28);
        uint mask = external ? 511u : 255u;
        int bucket = (int)((unchecked((uint)((int)key >> 12)) ^ key) & mask);
        var node = memory.Pointer(buckets + bucket * 8);
        var visited = new HashSet<nint>();
        uint? value = null;
        while (node != 0)
        {
            if (visited.Count >= 128 || !visited.Add(node))
                throw new InvalidOperationException("Invalid attribute hash chain.");
            var entry = memory.Read(node, 16);
            if (BitConverter.ToUInt32(entry, 8) == key)
            {
                value = BitConverter.ToUInt32(entry, 12);
                break;
            }
            node = (nint)BitConverter.ToInt64(entry);
        }
        if (!memory.Read(group, 8).SequenceEqual(data[..8]))
            throw new InvalidOperationException("Attribute group changed during capture.");
        return value;
    }
}
