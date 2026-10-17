using System.Net;
using System.Text.RegularExpressions;
using D3Vision.Core;

namespace D3Vision.Services;

public sealed record RankedHero(int Rank, string Name, string Path, string Tier, string ClearTime);

public sealed record GearLink(string Slot, string TooltipPath, string ItemPath);

/// <summary>Reads bounded, known Blizzard markup as plain data; no scripts or HTML are rendered.</summary>
public static class BlizzardBuildParser
{
    static MatchCollection Find(string text, string pattern) =>
        Regex.Matches(
            text,
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.Singleline,
            TimeSpan.FromSeconds(1)
        );

    public static string Plain(string html) =>
        WebUtility
            .HtmlDecode(
                Regex.Replace(
                    Regex.Replace(
                        html,
                        "<[^>]+>",
                        " ",
                        RegexOptions.Singleline,
                        TimeSpan.FromSeconds(1)
                    ),
                    @"\s+",
                    " ",
                    RegexOptions.None,
                    TimeSpan.FromSeconds(1)
                )
            )
            .Trim();

    public static int CurrentSeason(string html)
    {
        var matches = Find(html, @"/rankings/season/(?<season>\d+)/rift-barbarian");
        if (matches.Count == 0)
            throw new InvalidOperationException("Blizzard's current season could not be read.");
        int season = int.Parse(
            matches[0].Groups["season"].Value,
            System.Globalization.CultureInfo.InvariantCulture
        );
        if (season is < 1 or > 999)
            throw new InvalidOperationException("Invalid leaderboard season.");
        return season;
    }

    public static IReadOnlyList<RankedHero> Rankings(string html)
    {
        var heroes = new List<RankedHero>();
        foreach (Match row in Find(html, @"<tr\b[^>]*>(?<row>.*?)</tr>"))
        {
            string body = row.Groups["row"].Value;
            var rank = Find(body, "class=[\"']cell-Rank[\"'][^>]*data-raw=[\"'](?<rank>\\d+)[\"']")
                .Cast<Match>()
                .FirstOrDefault();
            var link = Find(
                    body,
                    "<a\\b[^>]*href=[\"'](?<url>/en-us/profile/[^\"'<>]+/hero/\\d+)[\"'][^>]*>(?<name>.*?)</a>"
                )
                .Cast<Match>()
                .FirstOrDefault();
            if (rank is null || link is null)
                continue;
            string Cell(string name) =>
                Find(body, "<td[^>]*class=[\"']cell-" + name + "[\"'][^>]*>(?<text>.*?)</td>")
                    .Cast<Match>()
                    .Select(m => Plain(m.Groups["text"].Value))
                    .FirstOrDefault()
                ?? "";
            heroes.Add(
                new(
                    int.Parse(
                        rank.Groups["rank"].Value,
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                    Plain(link.Groups["name"].Value),
                    WebUtility.HtmlDecode(link.Groups["url"].Value),
                    Cell("RiftLevel"),
                    Cell("RiftTime")
                )
            );
        }
        return heroes.OrderBy(h => h.Rank).ToArray();
    }

    public static IReadOnlyList<GearLink> Gear(string html)
    {
        var gear = new List<GearLink>();
        var list = Find(html, "<ul[^>]*class=[\"']gear-slots[\"'][^>]*>(?<body>.*?)</ul>")
            .Cast<Match>()
            .FirstOrDefault();
        if (list is null)
            return gear;
        foreach (
            Match slot in Find(
                list.Groups["body"].Value,
                "<li\\b[^>]*class=[\"']slot-(?<slot>[a-z-]+)[\"'][^>]*>(?<body>.*?)</li>"
            )
        )
        {
            var link = Find(slot.Groups["body"].Value, "<a[^>]*class=[\"']slot-link[\"'][^>]*>")
                .Cast<Match>()
                .FirstOrDefault();
            if (link is null)
                continue;
            string Attribute(string name) =>
                Find(link.Value, name + "=[\"'](?<value>[^\"']+)[\"']")
                    .Cast<Match>()
                    .Select(m => WebUtility.HtmlDecode(m.Groups["value"].Value))
                    .FirstOrDefault()
                ?? "";
            string tooltip = Attribute("data-d3tooltip"),
                path = Attribute("href");
            if (
                !tooltip.StartsWith("item-profile/", StringComparison.Ordinal)
                || !path.StartsWith("/en-us/", StringComparison.Ordinal)
            )
                continue;
            gear.Add(new(slot.Groups["slot"].Value, tooltip, path));
        }
        return gear;
    }

    public static BuildGear Item(GearLink link, string html, string sourceUrl)
    {
        var name = Find(html, @"<h3\b[^>]*>(?<text>.*?)</h3>")
            .Cast<Match>()
            .Select(m => Plain(m.Groups["text"].Value))
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Item tooltip is unavailable.");
        var effects =
            Find(html, "<ul[^>]*class=[\"']item-effects[\"'][^>]*>(?<text>.*?)</ul>")
                .Cast<Match>()
                .Select(m => m.Groups["text"].Value)
                .FirstOrDefault()
            ?? "";
        var secondary = Find(
                effects,
                "<p[^>]*class=[\"']item-property-category[\"'][^>]*>\\s*Secondary"
            )
            .Cast<Match>()
            .FirstOrDefault();
        if (secondary is not null)
            effects = effects[..secondary.Index];
        var affixes = Regex
            .Split(effects, @"<br\s*/?>", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1))
            .Select(Plain)
            .Where(t =>
                t.Length > 0
                && !t.Contains("Caldesann", StringComparison.OrdinalIgnoreCase)
                && !t.Equals("Primary", StringComparison.OrdinalIgnoreCase)
            )
            .Take(8)
            .ToArray();
        int sockets = Find(
            html,
            "<li[^>]*class=[\"'][^\"']*(?:full|empty)-socket[^\"']*[\"']"
        ).Count;
        if (sockets > 0 && sockets <= 3)
            affixes = [.. affixes, $"Sockets ({sockets})"];
        return new(link.Slot, name, affixes, sourceUrl);
    }
}
