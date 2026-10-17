namespace D3Vision.Infrastructure;

internal static class InventoryReader
{
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
