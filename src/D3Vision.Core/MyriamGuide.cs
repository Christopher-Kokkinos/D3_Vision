namespace D3Vision.Core;

public sealed record BuildGear(
    string Slot,
    string Name,
    IReadOnlyList<string> PrimaryAffixes,
    string SourceUrl
);

public sealed record LeaderboardBuild(
    HeroClass Class,
    int Season,
    int Rank,
    string Player,
    string Tier,
    string ClearTime,
    DateTimeOffset FetchedUtc,
    string LeaderboardUrl,
    string ProfileUrl,
    IReadOnlyList<BuildGear> Gear,
    string Notice,
    bool Offline = false
);

public interface ILeaderboardBuildProvider
{
    Task<LeaderboardBuild> GetAsync(
        HeroClass heroClass,
        bool allowFallback,
        CancellationToken token
    );
}

public static class HeroIdentity
{
    public static HeroClass? FromActorSno(int sno) =>
        sno switch
        {
            3285 or 3301 => HeroClass.Barbarian,
            238284 or 238286 => HeroClass.Crusader,
            74706 or 75207 => HeroClass.DemonHunter,
            4717 or 4721 => HeroClass.Monk,
            454021 or 454402 => HeroClass.Necromancer,
            6481 or 6485 => HeroClass.WitchDoctor,
            6526 or 6544 => HeroClass.Wizard,
            _ => null,
        };

    public static string LeaderboardSlug(HeroClass heroClass) =>
        heroClass switch
        {
            HeroClass.Barbarian => "barbarian",
            HeroClass.Crusader => "crusader",
            HeroClass.DemonHunter => "dh",
            HeroClass.Monk => "monk",
            HeroClass.Necromancer => "necromancer",
            HeroClass.WitchDoctor => "wd",
            HeroClass.Wizard => "wizard",
            _ => throw new ArgumentOutOfRangeException(nameof(heroClass)),
        };
}

public static class MyriamDetection
{
    public static bool IsEnchantScreen(string text)
    {
        bool header = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(line => line.Trim().Equals("ENCHANT", StringComparison.OrdinalIgnoreCase));
        return header
            && text.Contains("Property", StringComparison.OrdinalIgnoreCase)
            && (
                text.Contains("Replace", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Select", StringComparison.OrdinalIgnoreCase)
            );
    }
}
