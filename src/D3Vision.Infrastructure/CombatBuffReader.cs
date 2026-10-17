using D3Vision.Core;

namespace D3Vision.Infrastructure;

internal static class CombatBuffReader
{
    static readonly int SquirtsId = BuffDecoding.ItemId("P66_Unique_Amulet_010");

    public static CombatBuffSnapshot Capture(
        IGameMemoryReader memory,
        nint moduleBase,
        RiftMapSnapshot snapshot
    )
    {
        int[] worlds = snapshot.Scenes.Select(s => s.WorldId).Distinct().ToArray();
        if (worlds.Length != 1)
            throw new InvalidOperationException("Buffs require a stable world.");
        int world = worlds[0];
        var root = memory.Pointer(moduleBase + 0x16F5250);
        var container = memory.Pointer(memory.Pointer(root + 0x7B8 + 0x198));
        var header = memory.Read(container, 0x170);
        int last = BitConverter.ToUInt16(header, 0x108),
            shift = BitConverter.ToInt32(header, 0x168);
        if (last == 65535)
            return new(snapshot.ProcessId, world, Environment.TickCount64, [], "No player actors");
        if (last >= 8192 || shift is < 1 or > 10)
            throw new InvalidOperationException("Invalid buff actor container.");
        var table = (nint)BitConverter.ToInt64(header, 0x120);
        var attributes = new GameAttributeReader(memory, root);
        var actors = new Dictionary<int, (nint Address, byte[] Data)>();
        var equippedSquirtsOwners = new HashSet<int>();
        var equipmentOwners = new HashSet<int>();
        var monsters = new List<(int Id, int Sno, int Rarity, int Group, float X, float Y)>();
        var squirtsItems = new List<(nint Address, int Id, int Owner)>();
        for (int first = 0; first <= last; first += 1 << shift)
        {
            var block = memory.Pointer(table + (first >> shift) * 8);
            var bytes = memory.Read(block, Math.Min(1 << shift, last - first + 1) * 0x3C0);
            for (int offset = 0; offset < bytes.Length; offset += 0x3C0)
            {
                int Int(int field) => BitConverter.ToInt32(bytes, offset + field);
                int id = Int(0),
                    type = Int(0xB0);
                if (id == -1 || (id & 65535) != first + offset / 0x3C0)
                    continue;
                // ACD inventory owner +0x120; positive equipment slots 1..13, backpack 0.
                if (type == 2 && Int(0x124) is >= 1 and <= 13)
                {
                    equipmentOwners.Add(Int(0x120));
                    if (Int(0xB4) == SquirtsId)
                    {
                        equippedSquirtsOwners.Add(Int(0x120));
                        squirtsItems.Add((block + offset, id, Int(0x120)));
                    }
                }
                if (Int(0x118) != world)
                    continue;
                if (type == 7)
                    actors[id] = (block + offset, bytes.AsSpan(offset, 0x134).ToArray());
                else if (type == -1 && Int(0x130) != -1)
                {
                    float x = BitConverter.ToSingle(bytes, offset + 0xD8),
                        y = BitConverter.ToSingle(bytes, offset + 0xDC);
                    if (float.IsFinite(x) && float.IsFinite(y))
                        monsters.Add((id, Int(0x90), Int(0xB8), Int(0x130), x, y));
                }
            }
        }
        var manager = memory.Pointer(root + 0x7B8 + 0x140);
        var players = new List<PlayerBuffState>();
        for (int slot = 0; slot < 4; slot++)
        {
            var entry = manager + 0x60 + slot * 0xE7F0 + 4;
            int id = BitConverter.ToInt32(memory.Read(entry, 4));
            if (id == -1 || !actors.TryGetValue(id, out var actor))
                continue;
            var data = actor.Data;
            var hero = HeroIdentity.FromActorSno(BitConverter.ToInt32(data, 0x90));
            int group = BitConverter.ToInt32(data, 0x130);
            if (hero is null || group == -1)
                continue;
            uint? Icon(int power, int index) =>
                attributes.Read(group, BuffDecoding.IconKey(power, index));
            var coe = Enumerable
                .Range(0, 8)
                .Select(i => Icon(BuffDecoding.ConventionPower, i))
                .ToArray();
            // Live 2.8.0.99920: packed attribute 0x304 carries the 0..10 stack count.
            var squirts = Icon(BuffDecoding.SquirtsPower, 5);
            var squirtsPresent = Icon(BuffDecoding.SquirtsPower, 0);
            var stricken = Icon(BuffDecoding.StrickenPower, 0);
            var proc = Icon(BuffDecoding.StrickenPower, 2);
            float x = BitConverter.ToSingle(data, 0xD8),
                y = BitConverter.ToSingle(data, 0xDC);
            // Pets and scenery share this balance type and can have positive HP. Only explicit bosses
            // establish the encounter. Observed procs may still hit adds, so the count stays estimated.
            var nearby = stricken is > 0
                ? monsters
                    .Where(m =>
                    {
                        if (
                            (m.Rarity != 7 && !RiftGuardianIdentity.IsGuardian(m.Sno))
                            || Math.Pow(m.X - x, 2) + Math.Pow(m.Y - y, 2)
                                > RiftGuardianIdentity.TrackingRadius
                                    * RiftGuardianIdentity.TrackingRadius
                        )
                            return false;
                        var hp = attributes.Read(m.Group, 0xFFFFF073);
                        return hp is { } raw
                            && float.IsFinite(BitConverter.UInt32BitsToSingle(raw))
                            && BitConverter.UInt32BitsToSingle(raw) > 0
                            && attributes.Read(m.Group, 0xFFFFF296) is not > 0;
                    })
                    .ToArray()
                : [];
            int? boss = nearby.Length == 1 ? nearby[0].Id : null;
            var recheck = memory.Read(actor.Address, 0x134);
            if (
                BitConverter.ToInt32(recheck) != id
                || BitConverter.ToInt32(recheck, 0x118) != world
                || BitConverter.ToInt32(recheck, 0x130) != group
                || BitConverter.ToInt32(memory.Read(entry, 4)) != id
            )
                throw new InvalidOperationException("Buff player identity changed.");
            bool? equipped = equipmentOwners.Contains(id)
                ? equippedSquirtsOwners.Contains(id)
                : null;
            // Remote equipment may not be replicated. A live buff is usable without claiming gear knowledge.
            int? stacks =
                (squirts ?? 0) <= 10
                && (equipped == true || squirtsPresent is > 0 || squirts is > 0)
                    ? (int)(squirts ?? 0)
                    : null;
            players.Add(
                new(
                    slot,
                    id,
                    hero.Value,
                    BuffDecoding.ConventionElement(hero.Value, coe),
                    coe.Any(c => c is > 0),
                    equipped,
                    stacks,
                    stricken is > 0,
                    proc is > 0,
                    boss
                )
            );
        }
        if (memory.Pointer(container + 0x120) != table)
            throw new InvalidOperationException("Buff actors changed during capture.");
        foreach (var item in squirtsItems)
        {
            var check = memory.Read(item.Address, 0x128);
            if (
                BitConverter.ToInt32(check) != item.Id
                || BitConverter.ToInt32(check, 0x120) != item.Owner
                || BitConverter.ToInt32(check, 0xB4) != SquirtsId
                || BitConverter.ToInt32(check, 0x124) is < 1 or > 13
            )
                throw new InvalidOperationException("Squirt equipment changed during capture.");
        }
        int localSlot = BitConverter.ToInt32(memory.Read(memory.Pointer(root + 0xB28), 4));
        if (localSlot is < 0 or > 3)
            throw new InvalidOperationException("Local buff player slot is invalid.");
        if (!players.Any(p => p.PlayerSlot == localSlot))
            throw new InvalidOperationException("Local player is outside the sampled buff world.");
        return new(
            snapshot.ProcessId,
            world,
            Environment.TickCount64,
            players,
            $"Live buffs: {players.Count} player(s)",
            localSlot
        );
    }
}
