using D3Vision.Core;

namespace D3Vision.Infrastructure;

internal static class SceneRevealCache
{
    private const int AreaPointerRva = 0x1672A68;

    public static List<CachedScene> Read(IGameMemoryReader memory, nint moduleBase)
    {
        var area = memory.Pointer(moduleBase + AreaPointerRva);
        if (area == 0)
            return [];
        var header = memory.Read(area + 0x20, 0x50);
        int count = BitConverter.ToInt32(header, 0x10);
        if (
            count is < 0 or > 4096
            || BitConverter.ToInt32(header, 0x20) != 88
            || BitConverter.ToInt32(header, 0x48) != 0x600DF00D
        )
            throw new InvalidOperationException("Scene reveal cache layout failed validation.");
        var head = (nint)BitConverter.ToInt64(header);
        var tail = (nint)BitConverter.ToInt64(header, 8);
        var address = head;
        nint previous = 0;
        var seen = new HashSet<nint>();
        var result = new List<CachedScene>();
        for (int i = 0; i < count; i++)
        {
            if (address == 0 || !seen.Add(address))
                throw new InvalidOperationException(
                    "Scene reveal cache has incomplete or cyclic links."
                );
            var record = memory.Read(address, 88);
            if ((nint)BitConverter.ToInt64(record, 72) != previous)
                throw new InvalidOperationException("Scene reveal cache changed during capture.");
            float[] bounds = Enumerable
                .Range(0, 4)
                .Select(n => BitConverter.ToSingle(record, 16 + n * 4))
                .ToArray();
            if (
                bounds.Any(v => !float.IsFinite(v) || Math.Abs(v) > 1_000_000)
                || bounds[0] > bounds[2]
                || bounds[1] > bounds[3]
            )
                throw new InvalidOperationException("Cached scene bounds failed validation.");
            result.Add(
                new CachedScene(
                    BitConverter.ToInt32(record),
                    BitConverter.ToInt32(record, 4),
                    BitConverter.ToInt32(record, 8),
                    BitConverter.ToInt32(record, 12),
                    bounds
                )
            );
            previous = address;
            address = (nint)BitConverter.ToInt64(record, 80);
        }
        if (
            address != 0
            || previous != tail
            || count == 0 && head != 0
            || memory.Pointer(moduleBase + AreaPointerRva) != area
            || !memory.Read(area + 0x20, 0x20).SequenceEqual(header[..0x20])
        )
            throw new InvalidOperationException("Scene reveal cache changed during capture.");
        return result;
    }
}
