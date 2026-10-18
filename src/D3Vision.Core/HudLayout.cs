namespace D3Vision.Core;

public enum HorizontalAnchor
{
    Left,
    Center,
    Right,
}

public enum VerticalAnchor
{
    Top,
    Center,
    Bottom,
}

public enum HeroClass
{
    Barbarian,
    Crusader,
    DemonHunter,
    Monk,
    Necromancer,
    WitchDoctor,
    Wizard,
}

public enum Element
{
    Arcane,
    Cold,
    Fire,
    Holy,
    Lightning,
    Physical,
    Poison,
}

public sealed class PartyMemberSettings
{
    public int GamePlayerSlot { get; set; } = -1;
    public string Name { get; set; } = "Player";
    public HeroClass Class { get; set; } = HeroClass.Wizard;
}

public sealed class SkillSettings
{
    public SkillCalibration? Calibration { get; set; }
    public string Key { get; set; } = "1";
    public double DurationSeconds { get; set; } = 30;

    public static List<SkillSettings> Defaults() =>
        [
            new() { Key = "1" },
            new() { Key = "2" },
            new() { Key = "3" },
            new() { Key = "4" },
            new() { Key = "LMB", DurationSeconds = 0 },
            new() { Key = "RMB", DurationSeconds = 0 },
            new() { Key = "D", DurationSeconds = 30 },
        ];
}

public static class ElementCycles
{
    // CoE's global order, filtered to the damage types available to each class.
    public static IReadOnlyList<Element> ForClass(HeroClass hero) =>
        hero switch
        {
            HeroClass.Wizard => [Element.Arcane, Element.Cold, Element.Fire, Element.Lightning],
            HeroClass.Crusader => [Element.Fire, Element.Holy, Element.Lightning, Element.Physical],
            HeroClass.Monk =>
            [
                Element.Cold,
                Element.Fire,
                Element.Holy,
                Element.Lightning,
                Element.Physical,
            ],
            HeroClass.Necromancer => [Element.Cold, Element.Physical, Element.Poison],
            HeroClass.WitchDoctor => [Element.Cold, Element.Fire, Element.Physical, Element.Poison],
            _ => [Element.Cold, Element.Fire, Element.Lightning, Element.Physical],
        };
}

public readonly record struct HudPosition(double X, double Y);

public static class HudLayout
{
    public const int CurrentVersion = 1;

    public static bool IsParty(WidgetKind kind) =>
        kind is >= WidgetKind.Party1 and <= WidgetKind.Party4;

    public static HudPosition Resolve(WidgetState widget, double screenWidth, double screenHeight)
    {
        double x = widget.HorizontalAnchor switch
        {
            HorizontalAnchor.Center => (screenWidth - widget.Width) / 2,
            HorizontalAnchor.Right => screenWidth - widget.Width,
            _ => 0,
        };
        double y = widget.VerticalAnchor switch
        {
            VerticalAnchor.Center => (screenHeight - widget.Height) / 2,
            VerticalAnchor.Bottom => screenHeight - widget.Height,
            _ => 0,
        };
        return new(
            Math.Clamp(x + widget.X, 0, Math.Max(0, screenWidth - widget.Width)),
            Math.Clamp(y + widget.Y, 0, Math.Max(0, screenHeight - widget.Height))
        );
    }

    public static void Upgrade(Settings settings)
    {
        foreach (var kind in Enum.GetValues<WidgetKind>())
            if (!settings.Widgets.Any(w => w.Kind == kind))
            {
                var widget = new WidgetState { Kind = kind };
                if (kind == WidgetKind.RiftMap)
                    ConfigureMap(widget);
                settings.Widgets.Add(widget);
            }
        while (settings.Party.Count < 4)
            settings.Party.Add(
                new PartyMemberSettings { Name = $"Player {settings.Party.Count + 1}" }
            );
        if (settings.HudLayoutVersion < CurrentVersion)
            ApplyPreset(settings);
        if (settings.PortraitLayoutVersion < 1)
        {
            for (int i = 0; i < 4; i++)
            {
                settings.Party[i].GamePlayerSlot = -1;
                var portrait = settings.Widgets.Single(w =>
                    w.Kind == (WidgetKind)((int)WidgetKind.Party1 + i)
                );
                portrait.HorizontalAnchor = HorizontalAnchor.Left;
                portrait.VerticalAnchor = VerticalAnchor.Top;
                portrait.Width = 185;
                portrait.Height = 100;
                portrait.X = 14;
                portrait.Y = 38 + i * 118;
            }
            settings.PortraitLayoutVersion = 1;
        }
        while (settings.Skills.Count < SkillSlots.Count)
            settings.Skills.Add(SkillSettings.Defaults()[settings.Skills.Count]);
    }

    public static void ApplyPreset(Settings settings)
    {
        // Values are DIPs. Bottom/center/right anchors maintain alignment across resolutions.
        foreach (var w in settings.Widgets)
        {
            w.HorizontalAnchor = HorizontalAnchor.Center;
            w.VerticalAnchor = VerticalAnchor.Top;
            w.Height = 26;
            w.Y = 8;
            w.Opacity = .95;
            w.Color = "#D3BD8E";
            switch (w.Kind)
            {
                case WidgetKind.GreaterRiftTimer:
                    w.Width = 140;
                    w.X = -246;
                    break;
                case WidgetKind.Experience:
                    w.Width = 170;
                    w.X = -79;
                    break;
                case WidgetKind.Deaths:
                    w.Width = 96;
                    w.X = 70;
                    break;
                case WidgetKind.Elites:
                    w.Width = 106;
                    w.X = 181;
                    break;
                case WidgetKind.Goblins:
                    w.Width = 106;
                    w.X = 299;
                    break;
                case WidgetKind.RiftProgress:
                    w.HorizontalAnchor = HorizontalAnchor.Right;
                    w.VerticalAnchor = VerticalAnchor.Center;
                    w.Width = 76;
                    w.Height = 30;
                    w.X = -18;
                    w.Y = -70;
                    break;
                case WidgetKind.Cooldowns:
                    w.VerticalAnchor = VerticalAnchor.Bottom;
                    w.Width = 390;
                    w.Height = 46;
                    w.X = 36;
                    w.Y = -28;
                    break;
                case WidgetKind.BuildNotes:
                    w.HorizontalAnchor = HorizontalAnchor.Right;
                    w.Width = 230;
                    w.Height = 44;
                    w.X = -22;
                    w.Y = 42;
                    break;
                case WidgetKind.RiftMap:
                    ConfigureMap(w);
                    break;
                default:
                    if (IsParty(w.Kind))
                    {
                        w.HorizontalAnchor = HorizontalAnchor.Left;
                        w.Width = 185;
                        w.Height = 100;
                        w.X = 14;
                        w.Y = 38 + ((int)w.Kind - (int)WidgetKind.Party1) * 118;
                    }
                    break;
            }
        }
        settings.HudLayoutVersion = CurrentVersion;
    }

    private static void ConfigureMap(WidgetState widget)
    {
        widget.Enabled = false;
        widget.HorizontalAnchor = HorizontalAnchor.Right;
        widget.VerticalAnchor = VerticalAnchor.Top;
        widget.Width = 320;
        widget.Height = 240;
        widget.X = -24;
        widget.Y = 280;
        widget.Opacity = .8;
    }
}
