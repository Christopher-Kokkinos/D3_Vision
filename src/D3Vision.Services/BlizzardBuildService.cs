using System.Net.Http;
using System.Text;
using D3Vision.Core;

namespace D3Vision.Services;

public sealed class BlizzardBuildService : ILeaderboardBuildProvider, IDisposable
{
    const string Origin = "https://eu.diablo3.blizzard.com";
    readonly HttpClient client;
    readonly bool ownsClient;
    readonly IJsonStore? store;
    readonly SemaphoreSlim gate = new(1, 1);
    readonly Dictionary<HeroClass, LeaderboardBuild> cache = [];

    public BlizzardBuildService(IJsonStore? store = null, HttpClient? client = null)
    {
        this.store = store;
        ownsClient = client is null;
        this.client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<LeaderboardBuild> GetAsync(
        HeroClass heroClass,
        bool allowFallback,
        CancellationToken token
    )
    {
        await gate.WaitAsync(token);
        try
        {
            string key = "myriam-eu-v1-" + HeroIdentity.LeaderboardSlug(heroClass);
            if (!cache.TryGetValue(heroClass, out var saved) && store is not null)
                saved = await store.LoadAsync<LeaderboardBuild?>(key, () => null);
            bool usable =
                saved is not null
                && saved.Class == heroClass
                && (allowFallback || saved.Rank == 1)
                && saved.Gear is { Count: > 0 }
                && saved.Gear.All(g => g is not null && g.PrimaryAffixes is not null);
            if (usable && DateTimeOffset.UtcNow - saved!.FetchedUtc < TimeSpan.FromMinutes(15))
                return saved with { Offline = false };
            try
            {
                int season = BlizzardBuildParser.CurrentSeason(
                    await GetHtml("/en-us/rankings/", token)
                );
                string rankingPath =
                    $"/en-us/rankings/season/{season}/rift-{HeroIdentity.LeaderboardSlug(heroClass)}";
                var rankings = BlizzardBuildParser.Rankings(await GetHtml(rankingPath, token));
                if (rankings.Count == 0 || rankings[0].Rank != 1)
                    throw new InvalidOperationException(
                        "The current EU leaderboard could not be read."
                    );
                foreach (var hero in rankings.Take(allowFallback ? 20 : 1))
                {
                    IReadOnlyList<GearLink> links;
                    try
                    {
                        links = BlizzardBuildParser.Gear(await GetHtml(hero.Path, token));
                    }
                    catch (HttpRequestException)
                    {
                        continue;
                    }
                    string heroId = hero.Path.Split('/')[^1];
                    links = links
                        .Where(link =>
                            link.TooltipPath.Split('~') is var parts
                            && parts.Length >= 2
                            && parts[1] == heroId
                        )
                        .ToArray();
                    if (links.Count == 0)
                        continue;
                    var gear = new List<BuildGear>();
                    // Keep request concurrency low on Blizzard's public site.
                    foreach (var batch in links.Chunk(3))
                    {
                        var items = await Task.WhenAll(
                            batch.Select(async link =>
                            {
                                string path = "/en-us/tooltip/" + link.TooltipPath;
                                try
                                {
                                    return BlizzardBuildParser.Item(
                                        link,
                                        await GetHtml(path, token),
                                        Origin + link.ItemPath
                                    );
                                }
                                catch (Exception ex)
                                    when (ex is HttpRequestException or InvalidOperationException)
                                {
                                    return new BuildGear(
                                        link.Slot,
                                        "Item details unavailable",
                                        [],
                                        Origin + link.ItemPath
                                    );
                                }
                            })
                        );
                        gear.AddRange(items);
                    }
                    if (!gear.Any(g => g.PrimaryAffixes.Count > 0))
                        continue;
                    string[] slots =
                    [
                        "head",
                        "torso",
                        "feet",
                        "hands",
                        "shoulders",
                        "legs",
                        "bracers",
                        "mainHand",
                        "offHand",
                        "waist",
                        "rightFinger",
                        "leftFinger",
                        "neck",
                    ];
                    foreach (string slot in slots)
                        if (!gear.Any(g => g.Slot.Equals(slot, StringComparison.OrdinalIgnoreCase)))
                            gear.Add(
                                new(slot, "No item published for this slot", [], Origin + hero.Path)
                            );
                    gear = gear.OrderBy(g =>
                            Array.FindIndex(
                                slots,
                                s => s.Equals(g.Slot, StringComparison.OrdinalIgnoreCase)
                            )
                        )
                        .ToList();
                    var result = new LeaderboardBuild(
                        heroClass,
                        season,
                        hero.Rank,
                        hero.Name,
                        hero.Tier,
                        hero.ClearTime,
                        DateTimeOffset.UtcNow,
                        Origin + rankingPath,
                        Origin + hero.Path,
                        gear,
                        hero.Rank == 1
                            ? "Rank-one current equipped gear."
                            : $"Rank {hero.Rank}: highest accessible profile; higher-ranked gear could not be retrieved."
                    );
                    cache[heroClass] = result;
                    if (store is not null)
                        await store.SaveAsync(key, result);
                    return result;
                }
                throw new InvalidOperationException(
                    allowFallback
                        ? "No accessible gear/affixes were found in the first 20 EU leaderboard profiles."
                        : "The rank-one player's gear is unavailable on Blizzard's website."
                );
            }
            catch (Exception ex)
                when (ex
                        is HttpRequestException
                            or System.IO.IOException
                            or InvalidOperationException
                    || (ex is OperationCanceledException && !token.IsCancellationRequested)
                )
            {
                if (usable && DateTimeOffset.UtcNow - saved!.FetchedUtc < TimeSpan.FromHours(24))
                    return saved with
                    {
                        Offline = true,
                        Notice =
                            "Offline cached reference; rankings may have changed. " + saved.Notice,
                    };
                throw new InvalidOperationException(
                    "Myriam build reference unavailable: " + ex.Message,
                    ex
                );
            }
        }
        finally
        {
            gate.Release();
        }
    }

    async Task<string> GetHtml(string path, CancellationToken token)
    {
        var uri = new Uri(new Uri(Origin), path);
        if (
            uri.Scheme != "https"
            || uri.Host != "eu.diablo3.blizzard.com"
            || !uri.AbsolutePath.StartsWith("/en-us/", StringComparison.Ordinal)
        )
            throw new InvalidOperationException("Unsupported build source URL.");
        using var response = await client.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            token
        );
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > 2_000_000)
            throw new InvalidOperationException("Build source response is too large.");
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new System.IO.MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(chunk, token)) > 0)
        {
            if (buffer.Length + count > 2_000_000)
                throw new InvalidOperationException("Build source response is too large.");
            buffer.Write(chunk, 0, count);
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public void Dispose()
    {
        if (ownsClient)
            client.Dispose();
        gate.Dispose();
    }
}
