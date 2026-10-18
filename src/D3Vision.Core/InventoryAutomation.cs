namespace D3Vision.Core;

public enum InventoryAction
{
    SalvageAll,
    SalvageNormal,
    DropAll,
}

public sealed record BackpackItem(
    int ActorId,
    int Column,
    int Row,
    uint? Quality,
    LootRank? Rank,
    string DefinitionName
);

public static class InventorySelection
{
    // An explicit equipment allow-list excludes gems, materials, keys and unknown definitions.
    static readonly string[] Equipment =
    [
        "axe",
        "sword",
        "mace",
        "staff",
        "wand",
        "bow",
        "flail",
        "shield",
        "quiver",
        "mojo",
        "source",
        "phylactery",
        "scythe",
        "dagger",
        "spear",
        "fistweapon",
        "polearm",
        "daibo",
        "mightyweapon",
        "belt",
        "boots",
        "bracer",
        "chest",
        "glove",
        "helm",
        "pants",
        "shoulder",
        "ring",
        "amulet",
        "spiritstone",
        "voodoomask",
        "wizardhat",
        "cloak",
        "orb",
        "enchantress_special",
        "templar_special",
        "scoundrel_special",
    ];

    // Call only after validating the native attribute group. Its absent rank entry defaults to 0.
    public static LootRank? NativeRank(uint? quality, uint? ancient) =>
        quality is <= 9 && (ancient ?? 0) <= 2 ? (LootRank)(ancient ?? 0) : null;

    public static bool Eligible(BackpackItem item, InventoryAction action)
    {
        if (item.Column is < 0 or >= 10 || item.Row is < 0 or >= 6)
            return false;
        if (action == InventoryAction.DropAll)
            return true;
        string name = item.DefinitionName.ToLowerInvariant();
        if (
            name.Contains("gem")
            || name.Contains("potion")
            || name.Contains("quest")
            || name.Contains("crafting")
            || !Equipment.Any(name.Contains)
        )
            return false;
        if (item.Quality is null or > 9 || item.Rank is null)
            return false;
        return action == InventoryAction.SalvageAll || item.Rank == LootRank.Normal;
    }
}
