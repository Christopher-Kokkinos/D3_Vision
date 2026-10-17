using System.Net;
using System.Net.Http;
using System.Text.Json;
using D3Vision.Core;
using D3Vision.Services;

internal static class MyriamChecks
{
    const string Index = "<a href='/en-us/rankings/season/40/rift-barbarian'>Seasonal</a>";
    const string Board =
        "<table><tr><td class='cell-Rank' data-raw='1'>1.</td><td><a href='/en-us/profile/One-1/hero/101'>One</a></td><td class='cell-RiftLevel'>150</td><td class='cell-RiftTime'>5m</td></tr><tr><td class='cell-Rank' data-raw='2'>2.</td><td><a href='/en-us/profile/Two-2/hero/202'>Two</a></td><td class='cell-RiftLevel'>149</td></tr></table>";
    const string Profile =
        "<ul class='gear-slots'><li class='slot-head'><a class='slot-link' href='/en-us/item/test' data-d3tooltip='item-profile/22~202~test~id'>head</a></li><li class='slot-mainHand'><span>Empty</span></li></ul>";
    const string Tooltip =
        "<h3>Guardian&#39;s Test</h3><ul class='item-effects'><span>+950 Strength</span><br/><span>Critical Hit Chance Increased by 6.0%</span><br/><span>+625 Strength (Caldesann's Despair Rank 125)</span><br/><p class='item-property-category'>Secondary</p><span>+200 Poison Resistance</span><br/></ul><li class='d3-color-white full-socket'>Gem</li>";

    public static async Task<int> RunAsync()
    {
        int count = 0;
        void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
            count++;
        }
        Check(
            MyriamDetection.IsEnchantScreen("ENCHANT\nSelect a Property to Replace\n+487 Strength"),
            "Selected-item Enchant screen triggers the guide"
        );
        Check(
            MyriamDetection.IsEnchantScreen("Enchant\nReplace Property"),
            "Reroll selection keeps the guide visible"
        );
        Check(
            !MyriamDetection.IsEnchantScreen("TRANS MOG\nAvailable Gold")
                && !MyriamDetection.IsEnchantScreen("Chat: ENCHANT\nSelect Property"),
            "Other vendor panels and chat mentions do not trigger the guide"
        );
        foreach (
            var pair in new[]
            {
                (3301, HeroClass.Barbarian),
                (238286, HeroClass.Crusader),
                (74706, HeroClass.DemonHunter),
                (4717, HeroClass.Monk),
                (454402, HeroClass.Necromancer),
                (6485, HeroClass.WitchDoctor),
                (6544, HeroClass.Wizard),
            }
        )
            Check(
                HeroIdentity.FromActorSno(pair.Item1) == pair.Item2,
                "Both hero model and class mapping are explicit"
            );
        Check(
            HeroIdentity.FromActorSno(0) is null,
            "Unknown models cannot silently become Barbarian"
        );
        Check(
            HeroIdentity.LeaderboardSlug(HeroClass.DemonHunter) == "dh"
                && HeroIdentity.LeaderboardSlug(HeroClass.WitchDoctor) == "wd",
            "Class slugs match the official solo-GR boards"
        );
        Check(
            BlizzardBuildParser.CurrentSeason(Index) == 40,
            "Season is obtained from Blizzard navigation"
        );
        var rankings = BlizzardBuildParser.Rankings(Board);
        Check(
            rankings.Count == 2 && rankings[0].Rank == 1 && rankings[1].Name == "Two",
            "Rank and profile remain associated"
        );
        var links = BlizzardBuildParser.Gear(Profile);
        Check(
            links.Count == 1 && links[0].Slot == "head",
            "Only published gear links become items"
        );
        var item = BlizzardBuildParser.Item(
            links[0],
            Tooltip,
            "https://eu.diablo3.blizzard.com/en-us/item/test"
        );
        Check(
            item.Name == "Guardian's Test" && item.PrimaryAffixes.Count == 3,
            "Tooltip names, primary affixes and socket count are read"
        );
        Check(
            !item.PrimaryAffixes.Any(a => a.Contains("Caldesann") || a.Contains("Poison")),
            "Augments and secondary affixes do not become primary enchant targets"
        );
        var handler = new Handler();
        using var client = new HttpClient(handler);
        using var provider = new BlizzardBuildService(client: client);
        var build = await provider.GetAsync(HeroClass.Barbarian, true, default);
        Check(
            build.Rank == 2 && build.Player == "Two" && build.ProfileUrl.EndsWith("/202"),
            "Fallback explicitly retains its actual leaderboard rank and profile"
        );
        Check(
            build.Gear.Count == 13
                && build.Gear.Any(g => g.Slot == "mainHand" && g.PrimaryAffixes.Count == 0),
            "Unpublished slots are explicitly labelled instead of fabricated"
        );
        int requests = handler.Requests;
        var again = await provider.GetAsync(HeroClass.Barbarian, true, default);
        Check(
            again == build && handler.Requests == requests,
            "A fresh reference uses the 15-minute cache"
        );
        bool unavailable = false;
        try
        {
            await provider.GetAsync(HeroClass.Barbarian, false, default);
        }
        catch (InvalidOperationException)
        {
            unavailable = true;
        }
        Check(unavailable, "Disabling fallback never substitutes rank-two gear for rank one");
        using var wrongClient = new HttpClient(new Handler { WrongIdentity = true });
        using var wrongProvider = new BlizzardBuildService(client: wrongClient);
        bool rejected = false;
        try
        {
            await wrongProvider.GetAsync(HeroClass.Barbarian, true, default);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Check(rejected, "Gear belonging to a different hero ID is rejected");
        var store = new Store(build with { FetchedUtc = DateTimeOffset.UtcNow.AddHours(-1) });
        using var offlineClient = new HttpClient(new Handler { Offline = true });
        using var offlineProvider = new BlizzardBuildService(store, offlineClient);
        var offline = await offlineProvider.GetAsync(HeroClass.Barbarian, true, default);
        Check(
            offline.Offline && offline.Notice.Contains("cached") && offline.Rank == 2,
            "Offline references retain original rank and are labelled stale"
        );
        using var expiredProvider = new BlizzardBuildService(
            new Store(build with { FetchedUtc = DateTimeOffset.UtcNow.AddDays(-2) }),
            offlineClient
        );
        rejected = false;
        try
        {
            await expiredProvider.GetAsync(HeroClass.Barbarian, true, default);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Check(rejected, "Expired offline data is not presented as a current leaderboard build");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        bool stopped = false;
        try
        {
            await provider.GetAsync(HeroClass.Wizard, true, cancelled.Token);
        }
        catch (OperationCanceledException)
        {
            stopped = true;
        }
        Check(stopped, "Closing the guide cancels outstanding retrieval");
        return count;
    }

    sealed class Handler : HttpMessageHandler
    {
        public bool Offline,
            WrongIdentity;
        public int Requests;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken token
        )
        {
            Requests++;
            token.ThrowIfCancellationRequested();
            if (Offline)
                throw new HttpRequestException("Offline fixture");
            string path = request.RequestUri!.AbsolutePath;
            string html =
                path == "/en-us/rankings/" ? Index
                : path.Contains("/rankings/season/") ? Board
                : path.EndsWith("/101") ? "Profile Unavailable"
                : path.EndsWith("/202")
                    ? (WrongIdentity ? Profile.Replace("~202~", "~999~") : Profile)
                : Tooltip;
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) }
            );
        }
    }

    sealed class Store(LeaderboardBuild saved) : IJsonStore
    {
        public Task<T> LoadAsync<T>(string name, Func<T> fallback) =>
            Task.FromResult(
                JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(saved)) ?? fallback()
            );

        public Task SaveAsync<T>(string name, T value) => Task.CompletedTask;
    }
}
