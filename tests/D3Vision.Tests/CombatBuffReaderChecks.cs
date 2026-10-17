using D3Vision.Core;
using D3Vision.Infrastructure;

internal static class CombatBuffReaderChecks
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
            checks++;
        }
        var snapshot = new RiftMapSnapshot(
            DateTimeOffset.UtcNow,
            1,
            "test",
            "",
            "",
            0,
            1,
            [],
            [new(0, 10, 20, 30, 0, "0", [0, 0, 100, 100], new(0, 0, 0), [0, 0, 0, 1])],
            [],
            0,
            []
        );
        var active = CombatBuffReader.Capture(Memory.Create(), Memory.Module, snapshot);
        var player = active.Players.Single();
        Check(
            player.SquirtsEquipped == true && player.SquirtsStacks == 10,
            "Native Squirt equipment and count5 resolve ten stacks"
        );
        Check(
            player.ConventionEquipped && player.ActiveElement == Element.Fire,
            "Native CoE packed attributes resolve active element"
        );
        Check(
            player.StrickenEquipped && player.StrickenProc,
            "Native Stricken master icon and proc icon use distinct zero-based attribute keys"
        );
        Check(
            active.LocalPlayerSlot == 3 && player.PlayerSlot == 3,
            "Native manager identifies nonzero local player slot"
        );
        var inactive = CombatBuffReader
            .Capture(Memory.Create(stacks: 0), Memory.Module, snapshot)
            .Players.Single();
        Check(
            inactive.SquirtsEquipped == true && inactive.SquirtsStacks == 0,
            "Equipped but inactive Squirt provides red-cross state"
        );
        var absent = CombatBuffReader
            .Capture(Memory.Create(stacks: null), Memory.Module, snapshot)
            .Players.Single();
        Check(
            absent.SquirtsStacks == 0,
            "Missing default-zero attribute still distinguishes inactive equipped necklace"
        );
        var backpack = CombatBuffReader
            .Capture(Memory.Create(location: 0), Memory.Module, snapshot)
            .Players.Single();
        Check(backpack.SquirtsEquipped is null, "Backpack necklace does not establish equipment");
        var unequipped = CombatBuffReader
            .Capture(Memory.Create(item: 123), Memory.Module, snapshot)
            .Players.Single();
        Check(unequipped.SquirtsEquipped == false, "Other worn necklace is not Squirt");
        var foreign = CombatBuffReader
            .Capture(Memory.Create(owner: 999), Memory.Module, snapshot)
            .Players.Single();
        Check(
            foreign.SquirtsEquipped is null && foreign.SquirtsStacks == 10,
            "Remote active power is available without claiming equipment knowledge"
        );
        var remoteInactive = CombatBuffReader
            .Capture(
                Memory.Create(owner: 999, stacks: 0, powerPresent: true),
                Memory.Module,
                snapshot
            )
            .Players.Single();
        Check(
            remoteInactive.SquirtsEquipped is null && remoteInactive.SquirtsStacks == 0,
            "Remote zero-stack Squirt remains visible when the power is confirmed present"
        );
        var remoteUnknown = CombatBuffReader
            .Capture(Memory.Create(owner: 999, stacks: null), Memory.Module, snapshot)
            .Players.Single();
        Check(
            remoteUnknown.SquirtsStacks is null,
            "No remote equipment, power marker or stacks stays unknown rather than inventing inactive state"
        );
        var invalid = CombatBuffReader
            .Capture(Memory.Create(stacks: 100), Memory.Module, snapshot)
            .Players.Single();
        Check(
            invalid.SquirtsStacks is null,
            "Invalid Squirt count is unknown, never clamped into active state"
        );
        var boss = CombatBuffReader
            .Capture(Memory.Create(bossSno: 360281), Memory.Module, snapshot)
            .Players.Single();
        Check(
            boss.BossActorId == 65538,
            "Known guardian with zero rarity is selected despite nearby positive-HP scenery"
        );
        Check(
            CombatBuffReader
                .Capture(Memory.Create(bossSno: 360281, bossAlive: false), Memory.Module, snapshot)
                .Players.Single()
                .BossActorId
                is null,
            "Dead guardian cannot establish an encounter"
        );
        Check(
            CombatBuffReader
                .Capture(Memory.Create(bossSno: 488485), Memory.Module, snapshot)
                .Players.Single()
                .BossActorId
                is null,
            "Cosmetic pet with positive HP is never a guardian"
        );
        Check(
            MapMarkerClassification.Elite(-1, 0, 360281) == MapMarkerKind.Boss,
            "Known guardian maps correctly without a rarity flag"
        );
        Check(
            !RiftGuardianIdentity.IsGuardian(360327) && !RiftGuardianIdentity.IsGuardian(428323),
            "Guardian minions and UI actors do not become bosses"
        );
        Check(
            CombatBuffReader
                .Capture(Memory.Create(bossSno: 426943, bossDistance: 60), Memory.Module, snapshot)
                .Players.Single()
                .BossActorId == 65538,
            "Ranged Stricken users can track a guardian beyond the old forty-unit cutoff"
        );
        Check(
            CombatBuffReader
                .Capture(Memory.Create(bossSno: 426943, bossDistance: 121), Memory.Module, snapshot)
                .Players.Single()
                .BossActorId
                is null,
            "Distant guardians outside the bounded tracking radius do not establish an encounter"
        );
        return checks;
    }

    sealed class Memory : IGameMemoryReader
    {
        public static readonly nint Module = unchecked((nint)0x140000000);
        readonly Dictionary<nint, byte[]> blocks = [];

        void Put(nint address, byte[] data) => blocks[address] = data;

        void Ptr(nint address, long value) => Put(address, BitConverter.GetBytes(value));

        public static Memory Create(
            int location = 11,
            uint? stacks = 10,
            int? item = null,
            int owner = 65536,
            bool powerPresent = false,
            int? bossSno = null,
            bool bossAlive = true,
            float bossDistance = 0
        )
        {
            var m = new Memory();
            const int root = 0x10000000,
                container = 0x11000000,
                table = 0x12000000,
                actorBlock = 0x13000000;
            m.Ptr(Module + 0x16F5250, root);
            m.Ptr(root + 0x7B8 + 0x198, 0x14000000);
            m.Ptr(0x14000000, container);
            var header = new byte[0x170];
            BitConverter.GetBytes((ushort)(bossSno is null ? 1 : 3)).CopyTo(header, 0x108);
            BitConverter.GetBytes(4).CopyTo(header, 0x168);
            BitConverter.GetBytes((long)table).CopyTo(header, 0x120);
            m.Put(container, header);
            m.Ptr(table, actorBlock);
            var actors = new byte[(bossSno is null ? 2 : 4) * 0x3C0];
            void Field(int actor, int offset, int value) =>
                BitConverter.GetBytes(value).CopyTo(actors, actor * 0x3C0 + offset);
            Field(0, 0, 65536);
            Field(0, 0xB0, 7);
            Field(0, 0x90, 3301);
            Field(0, 0x118, 20);
            Field(0, 0x130, 0x20000);
            Field(1, 0, 65537);
            Field(1, 0xB0, 2);
            Field(1, 0xB4, item ?? BuffDecoding.ItemId("P66_Unique_Amulet_010"));
            Field(1, 0x120, owner);
            Field(1, 0x124, location);
            if (bossSno is { } sno)
            {
                Field(2, 0, 65538);
                Field(2, 0xB0, -1);
                Field(2, 0x90, sno);
                Field(2, 0x118, 20);
                Field(2, 0x130, 0x20000);
                Field(2, 0xD8, BitConverter.SingleToInt32Bits(bossDistance));
                Field(3, 0, 65539);
                Field(3, 0xB0, -1);
                Field(3, 0x90, 54952);
                Field(3, 0x118, 20);
                Field(3, 0x130, 0x20000);
            }
            m.Put(actorBlock, actors);
            m.Ptr(root + 0xB28, 0x15000000);
            m.Put(0x15000000, BitConverter.GetBytes(3));
            m.Ptr(root + 0x7B8 + 0x140, 0x16000000);
            var manager = new byte[0x60 + 4 * 0xE7F0];
            for (int i = 0; i < 4; i++)
                BitConverter.GetBytes(i == 3 ? 65536 : -1).CopyTo(manager, 0x60 + i * 0xE7F0 + 4);
            m.Put(0x16000000, manager);
            m.Ptr(root + 0x7B8 + 0x180, 0x17000000);
            m.Ptr(0x17000090, 0x18000000);
            var groupHeader = new byte[0x170];
            BitConverter.GetBytes(4).CopyTo(groupHeader, 0x168);
            BitConverter.GetBytes(0x19000000L).CopyTo(groupHeader, 0x120);
            m.Put(0x18000000, groupHeader);
            m.Ptr(0x19000000, 0x20000000);
            var group = new byte[0x30];
            BitConverter.GetBytes(0x20000).CopyTo(group, 0);
            BitConverter.GetBytes(0x21000000L).CopyTo(group, 0x28);
            m.Put(0x20000000, group);
            var buckets = new byte[256 * 8];
            int nodes = 0;
            void Icon(int power, int icon, uint value)
            {
                Attribute(BuffDecoding.IconKey(power, icon), value);
            }
            void Attribute(uint key, uint value)
            {
                int bucket = (int)((unchecked((uint)((int)key >> 12)) ^ key) & 255u);
                nint address = 0x22000000 + nodes++ * 32;
                var node = new byte[16];
                BitConverter.GetBytes(BitConverter.ToInt64(buckets, bucket * 8)).CopyTo(node, 0);
                BitConverter.GetBytes(key).CopyTo(node, 8);
                BitConverter.GetBytes(value).CopyTo(node, 12);
                m.Put(address, node);
                BitConverter.GetBytes((long)address).CopyTo(buckets, bucket * 8);
            }
            Icon(BuffDecoding.ConventionPower, 0, 1);
            Icon(BuffDecoding.ConventionPower, 3, 1);
            Icon(BuffDecoding.StrickenPower, 0, 1);
            Icon(BuffDecoding.StrickenPower, 2, 1);
            if (stacks is { } count)
                Icon(BuffDecoding.SquirtsPower, 5, count);
            if (powerPresent)
                Icon(BuffDecoding.SquirtsPower, 0, 1);
            if (bossSno is not null)
                Attribute(0xFFFFF073, BitConverter.SingleToUInt32Bits(bossAlive ? 100 : 0));
            m.Put(0x21000000, buckets);
            return m;
        }

        public byte[] Read(nint address, int count)
        {
            foreach (var (start, data) in blocks)
                if (address >= start && address - start + count <= data.Length)
                    return data.AsSpan((int)(address - start), count).ToArray();
            throw new System.IO.IOException($"Unmapped synthetic buff address {address:X}");
        }

        public nint Pointer(nint address) => (nint)BitConverter.ToInt64(Read(address, 8));
    }
}
