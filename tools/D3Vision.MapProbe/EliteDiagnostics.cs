using System.Text.Json;
using D3Vision.Core;
using D3Vision.Infrastructure;

internal static class EliteDiagnostics
{
    public static void Print(ReadOnlyProcess m, nint b, RiftMapSnapshot snapshot)
    {
        var root = m.Pointer(b + 0x16F5250);
        var c = m.Pointer(m.Pointer(root + 0x7B8 + 0x198));
        var h = m.Read(c, 0x170);
        int last = BitConverter.ToUInt16(h, 0x108),
            shift = BitConverter.ToInt32(h, 0x168);
        if (last >= 8192 || shift is < 1 or > 10)
            throw new InvalidOperationException("Invalid actor bounds");
        var table = m.Pointer(c + 0x120);
        var attr = new GameAttributeReader(m, root);
        var rows = new List<object>();
        for (int i = 0; i <= last; i++)
        {
            var address = m.Pointer(table + (i >> shift) * 8) + (i & ((1 << shift) - 1)) * 0x3C0;
            var a = m.Read(address, 0x198);
            int I(int o) => BitConverter.ToInt32(a, o);
            if (I(0) == -1 || (I(0) & 65535) != i)
                continue;
            if (!snapshot.Scenes.Any(s => s.WorldId == I(0x118)))
                continue;
            uint? hp = null,
                deleted = null;
            string? error = null;
            try
            {
                hp = attr.Read(I(0x130), 0xFFFFF073);
                deleted = attr.Read(I(0x130), 0xFFFFF296);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            rows.Add(
                new
                {
                    Id = I(0),
                    Type = I(0xB0),
                    Rarity = I(0xB8),
                    Sno = I(0x90),
                    HpBits = hp,
                    Hp = hp.HasValue ? (double?)BitConverter.UInt32BitsToSingle(hp.Value) : null,
                    Deleted = deleted,
                    Error = error,
                }
            );
        }
        Console.WriteLine(
            JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true })
        );
    }
}
