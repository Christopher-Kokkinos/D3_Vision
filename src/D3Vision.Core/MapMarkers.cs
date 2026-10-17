namespace D3Vision.Core;

public enum MapMarkerKind
{
    Champion,
    RareElite,
    RareMinion,
    Unique,
    Boss,
    Legendary,
    SetItem,
}

public enum LootRank
{
    Normal,
    Ancient,
    Primal,
}

public sealed record MapMarker(
    int ActorId,
    MapPoint Position,
    MapMarkerKind Kind,
    LootRank Rank = LootRank.Normal
);

public sealed record MapMarkerSnapshot(IReadOnlyList<MapMarker> Markers, string Status);

public static class MapMarkerClassification
{
    public static MapMarkerKind? Elite(int balanceType, int rarity) =>
        // ACD +0xB0 is a GameBalance handle type, not an ActorType enum.
        // Monsters use no balance handle (-1); items use 2 and heroes use 7.
        balanceType != -1
            ? null
            : rarity switch
            {
                1 => MapMarkerKind.Champion,
                2 => MapMarkerKind.RareElite,
                3 => MapMarkerKind.RareMinion,
                4 => MapMarkerKind.Unique,
                7 => MapMarkerKind.Boss,
                _ => null,
            };

    public static MapMarkerKind? Loot(int actorType, int location, int quality, bool isSet) =>
        actorType == 2 && location == -1 && quality == 9
            ? (isSet ? MapMarkerKind.SetItem : MapMarkerKind.Legendary)
            : null;
}
