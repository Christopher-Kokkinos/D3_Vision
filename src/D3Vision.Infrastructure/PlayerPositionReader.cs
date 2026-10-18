using D3Vision.Core;

namespace D3Vision.Infrastructure;

// Layout verified against native player/ACD accessors in build 2.8.0.99920.
internal static class PlayerPositionReader
{
    public static MapPoint Read(IGameMemoryReader m, nint moduleBase, RiftMapSnapshot snapshot)
    {
        var data = ReadActor(m, moduleBase, snapshot);
        var position = MapVector3.Read(data, 0xD8);
        return new(position.X, position.Y);
    }

    public static HeroClass ReadClass(
        IGameMemoryReader m,
        nint moduleBase,
        RiftMapSnapshot snapshot
    ) =>
        HeroIdentity.FromActorSno(BitConverter.ToInt32(ReadActor(m, moduleBase, snapshot), 0x90))
        ?? throw new InvalidOperationException("The local hero class could not be identified.");

    internal static byte[] ReadActor(IGameMemoryReader m, nint moduleBase, RiftMapSnapshot snapshot)
    {
        var root = m.Pointer(moduleBase + 0x16F5250);
        var player = m.Pointer(root + 0xB28);
        int index = BitConverter.ToInt32(m.Read(player, 4));
        if (index is < 0 or > 3)
            throw new InvalidOperationException("No local player.");
        var manager = m.Pointer(root + 0x7B8 + 0x140);
        int id = BitConverter.ToInt32(m.Read(manager + 0x60 + index * 0xE7F0 + 4, 4));
        if (id == -1)
            throw new InvalidOperationException("No player actor.");
        var container = m.Pointer(m.Pointer(root + 0x7B8 + 0x198));
        int shift = BitConverter.ToInt32(m.Read(container + 0x168, 4));
        if (shift is < 1 or > 16)
            throw new InvalidOperationException("Invalid actor container.");
        var table = m.Pointer(container + 0x120);
        int low = id & 65535;
        var acd = m.Pointer(table + (low >> shift) * 8) + (low & ((1 << shift) - 1)) * 0x3C0;
        var data = m.Read(acd, 0x120);
        int world = BitConverter.ToInt32(data, 0x118);
        if (BitConverter.ToInt32(data) != id || !snapshot.Scenes.Any(s => s.WorldId == world))
            throw new InvalidOperationException("Player and map area do not match.");
        return data;
    }
}
