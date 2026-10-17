using D3Vision.Core;
using D3Vision.Infrastructure;

internal sealed record CombatActorSample(
    int Id,
    int Sno,
    int Type,
    int Rarity,
    float Hp,
    float X,
    float Y
);

internal static class CombatActorDiagnostics
{
    public static IReadOnlyList<CombatActorSample> Capture(
        IGameMemoryReader memory,
        nint module,
        CombatBuffSnapshot buffs
    )
    {
        var root = memory.Pointer(module + 0x16F5250);
        var container = memory.Pointer(memory.Pointer(root + 0x7B8 + 0x198));
        var header = memory.Read(container, 0x170);
        int last = BitConverter.ToUInt16(header, 0x108),
            shift = BitConverter.ToInt32(header, 0x168);
        if (last == 65535)
            return [];
        if (last >= 8192 || shift is < 1 or > 10)
            throw new InvalidOperationException("Invalid diagnostic actor bounds.");
        var table = (nint)BitConverter.ToInt64(header, 0x120);
        var rows = new List<(int Id, int Sno, int Type, int Rarity, int Group, float X, float Y)>();
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
                    || Int(0x118) != buffs.WorldId
                    || Int(0xB0) == 2
                )
                    continue;
                float x = BitConverter.ToSingle(bytes, offset + 0xD8),
                    y = BitConverter.ToSingle(bytes, offset + 0xDC);
                if (float.IsFinite(x) && float.IsFinite(y))
                    rows.Add((id, Int(0x90), Int(0xB0), Int(0xB8), Int(0x130), x, y));
            }
        }
        var heroes = rows.Where(r =>
                buffs.Players.Any(p => p.ActorId == r.Id && p.StrickenEquipped)
            )
            .ToArray();
        if (heroes.Length == 0)
            return [];
        var attributes = new GameAttributeReader(memory, root);
        var result = new List<CombatActorSample>();
        foreach (
            var actor in rows.Where(r =>
                    r.Type != 7
                    && r.Group != -1
                    && heroes.Any(p => Math.Pow(p.X - r.X, 2) + Math.Pow(p.Y - r.Y, 2) <= 120 * 120)
                )
                .Take(256)
        )
        {
            var raw = attributes.Read(actor.Group, 0xFFFFF073);
            if (raw is not { } value)
                continue;
            float hp = BitConverter.UInt32BitsToSingle(value);
            if (float.IsFinite(hp) && hp > 0 && attributes.Read(actor.Group, 0xFFFFF296) is not > 0)
                result.Add(
                    new(actor.Id, actor.Sno, actor.Type, actor.Rarity, hp, actor.X, actor.Y)
                );
        }
        return result.Take(64).ToArray();
    }
}
