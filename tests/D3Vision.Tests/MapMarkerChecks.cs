using D3Vision.Core;
using D3Vision.Infrastructure;

internal static class MapMarkerChecks
{
    public static int Run()
    {
        int count = 0;
        void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
            count++;
        }
        foreach (
            var pair in new[]
            {
                (1, MapMarkerKind.Champion),
                (2, MapMarkerKind.RareElite),
                (3, MapMarkerKind.RareMinion),
                (4, MapMarkerKind.Unique),
                (7, MapMarkerKind.Boss),
            }
        )
            Check(
                MapMarkerClassification.Elite(-1, pair.Item1) == pair.Item2,
                "Elite rarity classification"
            );
        Check(
            MapMarkerClassification.Elite(7, 1) is null
                && MapMarkerClassification.Elite(-1, 0) is null,
            "Players and normal monsters are not elites"
        );
        Check(
            MapMarkerClassification.Loot(2, -1, 9, true) == MapMarkerKind.SetItem,
            "Ground legendary quality and verified set membership classify as set"
        );
        Check(
            MapMarkerClassification.Loot(2, 0, 9, true) is null
                && MapMarkerClassification.Loot(2, -1, 6, false) is null,
            "Inventory and rare items do not become loot stars"
        );
        Check(
            MapMarkerClassification.Elite(1, 1) is null
                && MapMarkerClassification.Elite(2, 2) is null,
            "Balance item/type records must not be mistaken for monsters"
        );
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
        Check(
            InventoryReader.Count(Memory.Create(), Memory.Module) == 1,
            "Inventory confirmation counts backpack items only"
        );
        Check(
            InventoryReader.Count(Memory.Create("picked-up"), Memory.Module) == 2,
            "A purchased/picked-up item increases the backpack count"
        );
        var valid = ActorMarkerReader.Capture(Memory.Create(), Memory.Module, snapshot);
        Check(
            ActorMarkerReader
                .Capture(Memory.Create("external"), Memory.Module, snapshot)
                .Markers.SequenceEqual(valid.Markers),
            "External attribute tables resolve the same loot and elite values as inline tables"
        );

        Check(
            valid.Markers.Count == 5,
            "Only live same-world elites and ground legendary/set items survive filtering"
        );
        Check(
            valid.Markers.Single(m => m.ActorId == 0x10000).Kind == MapMarkerKind.SetItem
                && valid.Markers.Single(m => m.ActorId == 0x10000).Rank == LootRank.Normal,
            "Normal set item retains its rank"
        );
        Check(
            valid.Markers.Single(m => m.ActorId == 0x10001).Rank == LootRank.Ancient,
            "Ancient set item reads rank 1"
        );
        Check(
            valid.Markers.Single(m => m.ActorId == 0x10002).Rank == LootRank.Primal
                && valid.Markers.Single(m => m.ActorId == 0x10002).Kind == MapMarkerKind.Legendary,
            "Primal legendary reads rank 2 independently of set membership"
        );
        Check(
            valid.Markers.All(m =>
                m.ActorId is not 0x10003 and not 0x10008 and not 0x10005 and not 0x10007
            ),
            "Dead, wrong-world, inventory and rare loot are excluded"
        );
        Check(
            ActorMarkerReader
                .Capture(Memory.Create(), Memory.Module, snapshot, false, true)
                .Markers.Count == 3,
            "Elite marker toggle preserves loot"
        );
        Check(
            ActorMarkerReader
                .Capture(Memory.Create(), Memory.Module, snapshot, true, false)
                .Markers.Count == 2,
            "Loot marker toggle preserves elites"
        );
        Check(
            ActorMarkerReader
                .Capture(Memory.Create(), Memory.Module, snapshot, false, false)
                .Markers.Count == 0,
            "Disabled marker sources produce no markers"
        );
        foreach (
            string invalid in new[]
            {
                "shift",
                "actor-count",
                "attribute-id",
                "attribute-cycle",
                "item-id",
                "item-bytes",
                "item-cycle",
            }
        )
        {
            bool rejected = false;
            try
            {
                ActorMarkerReader.Capture(Memory.Create(invalid), Memory.Module, snapshot);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            Check(rejected, $"Malformed {invalid} is rejected");
        }
        Check(
            ActorMarkerReader
                .Capture(Memory.Create("unknown-rank"), Memory.Module, snapshot)
                .Markers.All(m => m.ActorId != 0x10001),
            "Unknown rank is never promoted to Ancient or Primal"
        );
        Check(
            ActorMarkerReader
                .Capture(Memory.Create("picked-up"), Memory.Module, snapshot)
                .Markers.All(m => m.ActorId != 0x10000),
            "Picked-up loot disappears from the next snapshot"
        );
        return count;
    }

    sealed class Memory : IGameMemoryReader
    {
        public static readonly nint Module = unchecked((nint)0x140000000);
        readonly Dictionary<nint, byte[]> blocks = [];

        void Put(nint address, byte[] data) => blocks[address] = data;

        void Int(nint address, int value) => Put(address, BitConverter.GetBytes(value));

        void Ptr(nint address, nint value) => Put(address, BitConverter.GetBytes((long)value));

        public static Memory Create(string invalid = "")
        {
            var m = new Memory();
            nint root = 0x20000000,
                container = 0x30000000,
                actorBlock = 0x40000000,
                groupBlock = 0x50000000;
            m.Ptr(Module + 0x16F5250, root);
            m.Ptr(root + 0x7B8 + 0x198, 0x21000000);
            m.Ptr(0x21000000, container);
            var header = new byte[0x170];
            BitConverter
                .GetBytes((ushort)(invalid == "actor-count" ? 8192 : 8))
                .CopyTo(header, 0x108);
            BitConverter.GetBytes(invalid == "shift" ? 31 : 4).CopyTo(header, 0x168);
            BitConverter.GetBytes(0x31000000L).CopyTo(header, 0x120);
            m.Put(container, header);
            m.Ptr(0x31000000, actorBlock);
            var actors = new byte[9 * 0x3C0];
            void Field(int actor, int offset, int value) =>
                BitConverter.GetBytes(value).CopyTo(actors, actor * 0x3C0 + offset);
            for (int i = 0; i < 9; i++)
            {
                Field(i, 0, 0x10000 + i);
                Field(i, 0xB0, i >= 3 && i <= 6 ? -1 : 2);
                Field(i, 0xB4, i == 2 ? 456 : 123);
                Field(i, 0x118, i == 5 ? 21 : 20);
                Field(i, 0x124, i == 0 && invalid == "picked-up" || i == 8 ? 0 : -1);
                Field(i, 0x130, 0x20000 + i);
                Field(
                    i,
                    0xB8,
                    i == 3 ? 2
                        : i == 4 ? 1
                        : i == 5 ? 2
                        : i == 6 ? 4
                        : 0
                );
                BitConverter.GetBytes(10f + i).CopyTo(actors, i * 0x3C0 + 0xD8);
                BitConverter.GetBytes(20f).CopyTo(actors, i * 0x3C0 + 0xDC);
            }
            m.Put(actorBlock, actors);
            m.Ptr(root + 0x7B8 + 0x180, 0x22000000);
            m.Ptr(0x22000090, 0x23000000);
            var gh = new byte[0x170];
            BitConverter.GetBytes((ushort)8).CopyTo(gh, 0x108);
            BitConverter.GetBytes(4).CopyTo(gh, 0x168);
            BitConverter.GetBytes(0x24000000L).CopyTo(gh, 0x120);
            m.Put(0x23000000, gh);
            m.Ptr(0x24000000, groupBlock);
            var groups = new byte[9 * 0x12E8];
            for (int i = 0; i < 9; i++)
            {
                BitConverter
                    .GetBytes(invalid == "attribute-id" && i == 0 ? 99 : 0x20000 + i)
                    .CopyTo(groups, i * 0x12E8);
                var buckets = new byte[(invalid == "external" ? 512 : 256) * 8];
                nint bucketAddress = 0x60000000 + i * 0x10000;
                BitConverter.GetBytes((long)bucketAddress).CopyTo(groups, i * 0x12E8 + 0x28);
                if (invalid == "external")
                {
                    BitConverter.GetBytes(4).CopyTo(groups, i * 0x12E8 + 4);
                    nint external = 0x68000000 + i * 0x10000;
                    BitConverter.GetBytes((long)external).CopyTo(groups, i * 0x12E8 + 0x10);
                    m.Ptr(external + 0x10, bucketAddress);
                }
                void Attribute(uint key, uint value, int slot)
                {
                    int bucket = (int)(
                        (unchecked((uint)((int)key >> 12)) ^ key)
                        & (invalid == "external" ? 511u : 255u)
                    );
                    nint address = bucketAddress + 0x1000 + slot * 32;
                    var entry = new byte[16];
                    long previous = BitConverter.ToInt64(buckets, bucket * 8);
                    BitConverter
                        .GetBytes(invalid == "attribute-cycle" && i == 0 ? (long)address : previous)
                        .CopyTo(entry, 0);
                    BitConverter.GetBytes(key).CopyTo(entry, 8);
                    BitConverter.GetBytes(value).CopyTo(entry, 12);
                    m.Put(address, entry);
                    BitConverter.GetBytes((long)address).CopyTo(buckets, bucket * 8);
                }
                Attribute(0xFFFFF17F, (uint)(i == 7 ? 6 : 9), 0);
                Attribute(
                    0xFFFFF19D,
                    (uint)(
                        invalid == "unknown-rank" && i == 1 ? 3
                        : i == 1 ? 1
                        : i == 2 ? 2
                        : 0
                    ),
                    1
                );
                Attribute(0xFFFFF073, BitConverter.SingleToUInt32Bits(i == 3 ? 0 : 100), 2);
                if (invalid == "attribute-cycle" && i == 0)
                {
                    // Force the queried deleted flag into a self-referencing nonmatching chain.
                    int bucket = (int)(
                        (unchecked((uint)((int)0xFFFFF296u >> 12)) ^ 0xFFFFF296u) & 255
                    );
                    nint cycle = bucketAddress + 0x3000;
                    var entry = new byte[16];
                    BitConverter.GetBytes((long)cycle).CopyTo(entry, 0);
                    m.Put(cycle, entry);
                    BitConverter.GetBytes((long)cycle).CopyTo(buckets, bucket * 8);
                }
                m.Put(bucketAddress, buckets);
            }
            m.Put(groupBlock, groups);
            m.Ptr(Module + 0x16F64C0, 0x70000000);
            m.Ptr(0x70000000, 0x71000000);
            m.Int(0x71000100, 255);
            m.Ptr(0x71000110, 0x72000000);
            var balanceBuckets = new byte[256 * 8];
            foreach (var (gbid, row) in new[] { (123, 0), (456, 1) })
            {
                uint hash = 2166136261;
                foreach (byte b in BitConverter.GetBytes(gbid))
                    hash = unchecked((hash ^ b) * 16777619);
                nint address = 0x73000000 + row * 32;
                var entry = new byte[20];
                BitConverter
                    .GetBytes(invalid == "item-cycle" ? (long)address : 0L)
                    .CopyTo(entry, 0);
                BitConverter.GetBytes(invalid == "item-cycle" ? 999 : gbid).CopyTo(entry, 8);
                BitConverter.GetBytes(1).CopyTo(entry, 12);
                BitConverter.GetBytes(row).CopyTo(entry, 16);
                m.Put(address, entry);
                BitConverter.GetBytes((long)address).CopyTo(balanceBuckets, (int)(hash & 255) * 8);
            }
            m.Put(0x72000000, balanceBuckets);
            m.Int(Module + 0x1704C48, 2);
            m.Ptr(Module + 0x1704C50, 0x74000000);
            m.Int(0x74000004, 0);
            m.Ptr(Module + 0x1714180, 0x75000000);
            m.Ptr(0x75000020, 0x76000000);
            var ah = new byte[0x128];
            BitConverter.GetBytes(0x77000000L).CopyTo(ah, 0x120);
            m.Put(0x76000000, ah);
            m.Ptr(0x77000010, 0x78000000);
            var asset = new byte[0x40];
            BitConverter.GetBytes(1).CopyTo(asset, 0);
            BitConverter.GetBytes(2).CopyTo(asset, 12);
            BitConverter.GetBytes(invalid == "item-bytes" ? 1 : 2 * 0x580).CopyTo(asset, 0x2C);
            BitConverter.GetBytes(0x79000000L).CopyTo(asset, 0x30);
            m.Put(0x78000000, asset);
            var definitions = new byte[2 * 0x580];
            BitConverter.GetBytes(invalid == "item-id" ? 999 : 123).CopyTo(definitions, 0x100);
            BitConverter.GetBytes(456).CopyTo(definitions, 0x580 + 0x100);
            BitConverter.GetBytes(987).CopyTo(definitions, 0x174);
            BitConverter.GetBytes(-1).CopyTo(definitions, 0x580 + 0x174);
            m.Put(0x79000000, definitions);
            return m;
        }

        public byte[] Read(nint address, int count)
        {
            foreach (var (start, data) in blocks)
                if (address >= start && address - start + count <= data.Length)
                    return data.AsSpan((int)(address - start), count).ToArray();
            throw new System.IO.IOException($"Unmapped synthetic marker address {address:X}");
        }

        public nint Pointer(nint address) => (nint)BitConverter.ToInt64(Read(address, 8));
    }
}
