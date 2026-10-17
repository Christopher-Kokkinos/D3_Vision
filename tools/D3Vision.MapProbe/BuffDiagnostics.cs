using System.Text.Json;
using D3Vision.Core;
using D3Vision.Infrastructure;

internal static class BuffDiagnostics
{
    public static void Print(IGameMemoryReader memory, nint moduleBase, RiftMapSnapshot snapshot)
    {
        var root = memory.Pointer(moduleBase + 0x16F5250);
        var container = memory.Pointer(memory.Pointer(root + 0x7B8 + 0x198));
        int last = BitConverter.ToUInt16(memory.Read(container + 0x108, 2));
        int shift = BitConverter.ToInt32(memory.Read(container + 0x168, 4));
        if (last >= 8192 || shift is < 1 or > 10)
            throw new InvalidOperationException("Invalid diagnostic actor table.");
        var table = memory.Pointer(container + 0x120);
        for (int index = 0; index <= last; index++)
        {
            var address =
                memory.Pointer(table + (index >> shift) * 8) + (index & ((1 << shift) - 1)) * 0x3C0;
            var data = memory.Read(address, 0x134);
            int Int(int o) => BitConverter.ToInt32(data, o);
            if (Int(0) == -1 || (Int(0) & 65535) != index)
                continue;
            if (Int(0xB0) == 2 && Int(0x124) is >= 1 and <= 13)
                Console.WriteLine(
                    $"Equipped candidate id={Int(0)} gbid={Int(0xB4):X8} owner={Int(0x120)} slot={Int(0x124)}"
                );
            if (Int(0xB0) == 2 && Int(0xB4) == BuffDecoding.ItemId("Unique_Gem_018_x1"))
                Console.WriteLine($"Stricken gem id={Int(0)} owner={Int(0x120)} slot={Int(0x124)}");
            if (Int(0xB0) != 7 || HeroIdentity.FromActorSno(Int(0x90)) is null)
                continue;
            int groupId = Int(0x130);
            var groups = memory.Pointer(memory.Pointer(root + 0x7B8 + 0x180) + 0x90);
            int groupShift = BitConverter.ToInt32(memory.Read(groups + 0x168, 4));
            if (groupShift is < 1 or > 16)
                throw new InvalidOperationException("Invalid diagnostic group shift.");
            int low = groupId & 65535;
            var group =
                memory.Pointer(memory.Pointer(groups + 0x120) + (low >> groupShift) * 8)
                + (low & ((1 << groupShift) - 1)) * 0x12E8;
            var header = memory.Read(group, 0x30);
            bool external = (BitConverter.ToInt32(header, 4) & 4) != 0;
            var buckets = external
                ? memory.Pointer(memory.Pointer(group + 0x10) + 0x10)
                : memory.Pointer(group + 0x28);
            var pointers = memory.Read(buckets, (external ? 512 : 256) * 8);
            Console.WriteLine($"Hero actor={Int(0)} group={groupId}");
            for (int bucket = 0; bucket < pointers.Length / 8; bucket++)
            {
                var node = (nint)BitConverter.ToInt64(pointers, bucket * 8);
                var visited = new HashSet<nint>();
                while (node != 0)
                {
                    if (!visited.Add(node) || visited.Count > 128)
                        throw new InvalidOperationException("Invalid diagnostic attribute chain.");
                    var value = memory.Read(node, 16);
                    uint key = BitConverter.ToUInt32(value, 8),
                        raw = BitConverter.ToUInt32(value, 12);
                    int attr = (int)(key & 4095),
                        power = (int)(key >> 12);
                    if (attr is >= 767 and <= 798 && raw > 0)
                        Console.WriteLine($"  icon power={power} index={attr - 767} raw={raw}");
                    else if (power is 428348 or 428349)
                        Console.WriteLine($"  Stricken attribute={attr} raw={raw}");
                    node = (nint)BitConverter.ToInt64(value);
                }
            }
        }
    }
}
