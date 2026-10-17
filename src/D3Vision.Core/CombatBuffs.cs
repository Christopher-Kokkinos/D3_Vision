namespace D3Vision.Core;

public sealed record PlayerBuffState(
    int PlayerSlot,
    int ActorId,
    HeroClass Class,
    Element? ActiveElement,
    bool ConventionEquipped,
    bool? SquirtsEquipped,
    int? SquirtsStacks,
    bool StrickenEquipped,
    bool StrickenProc,
    int? BossActorId
);

public sealed record CombatBuffSnapshot(
    int ProcessId,
    int WorldId,
    long SampledAt,
    IReadOnlyList<PlayerBuffState> Players,
    string Status,
    int LocalPlayerSlot = 0
)
{
    public static CombatBuffSnapshot Waiting { get; } = new(0, 0, 0, [], "Waiting for live buffs");

    public bool IsFresh(long now) => SampledAt > 0 && now >= SampledAt && now - SampledAt <= 750;

    public PlayerBuffState? Portrait(int index, int gameSlot = -1) =>
        gameSlot >= 0
            ? Players.FirstOrDefault(p => p.PlayerSlot == gameSlot)
            : Players
                .OrderBy(p => p.PlayerSlot == LocalPlayerSlot ? -1 : p.PlayerSlot)
                .ElementAtOrDefault(index);
}

public static class StrickenReadout
{
    public static string? Text(PlayerBuffState? player, int? observedProcs) =>
        player switch
        {
            null or { StrickenEquipped: false } => null,
            { BossActorId: null } => "Stricken ready",
            _ => observedProcs is >= 0 ? $"Stricken ~{observedProcs}" : "Stricken ?",
        };
}

public static class BuffDecoding
{
    public const int ConventionPower = 430674;
    public const int SquirtsPower = 483552;
    public const int StrickenPower = 428348;

    // Native descriptor IDs are one-based; packed attribute keys use ID - 1.
    public const int IconCountAttribute = 0x2FF;

    public static uint IconKey(int power, int icon)
    {
        if (power is < 0 or > 0xFFFFF || icon is < 0 or > 31)
            throw new ArgumentOutOfRangeException(nameof(icon));
        return ((uint)power << 12) | (uint)(IconCountAttribute + icon);
    }

    public static Element? ConventionElement(HeroClass hero, IReadOnlyList<uint?> counts)
    {
        if (counts.Count < 8 || counts[0] is not > 0)
            return null;
        Element[] order =
        [
            Element.Arcane,
            Element.Cold,
            Element.Fire,
            Element.Holy,
            Element.Lightning,
            Element.Physical,
            Element.Poison,
        ];
        var active = Enumerable.Range(1, 7).Where(i => counts[i] is > 0).ToArray();
        return active.Length == 1 && ElementCycles.ForClass(hero).Contains(order[active[0] - 1])
            ? order[active[0] - 1]
            : null;
    }

    public static int ItemId(string name)
    {
        uint hash = 0;
        foreach (char c in name.ToLowerInvariant())
            hash = unchecked(hash * 33 + c);
        return unchecked((int)hash);
    }
}

/// <summary>Observed Stricken proc edges near a single identified boss, never an exact target stack claim.</summary>
public sealed class StrickenProcTracker
{
    readonly Dictionary<
        int,
        (int Process, int World, int Actor, int Boss, long Seen, bool Proc, int Count)
    > states = [];

    public void Apply(CombatBuffSnapshot snapshot)
    {
        foreach (
            int absent in states.Keys.Except(snapshot.Players.Select(p => p.PlayerSlot)).ToArray()
        )
            states.Remove(absent);
        foreach (var player in snapshot.Players)
        {
            if (!player.StrickenEquipped || player.BossActorId is not { } boss)
            {
                states.Remove(player.PlayerSlot);
                continue;
            }
            int count = 0;
            if (
                states.TryGetValue(player.PlayerSlot, out var previous)
                && previous.Process == snapshot.ProcessId
                && previous.World == snapshot.WorldId
                && previous.Actor == player.ActorId
                && previous.Boss == boss
                && snapshot.SampledAt > previous.Seen
                && snapshot.SampledAt - previous.Seen <= 350
            )
                count = previous.Count + (player.StrickenProc && !previous.Proc ? 1 : 0);
            // First sample establishes a baseline; an already-active proc is not counted.
            states[player.PlayerSlot] = (
                snapshot.ProcessId,
                snapshot.WorldId,
                player.ActorId,
                boss,
                snapshot.SampledAt,
                player.StrickenProc,
                count
            );
        }
    }

    public int? Count(int slot) => states.TryGetValue(slot, out var state) ? state.Count : null;
}
