using System.Globalization;
using System.Windows;
using System.Windows.Media;
using D3Vision.Core;

namespace D3Vision.Rendering;

/// <summary>Small transparent HUD annotations; window chrome appears only while editing.</summary>
public sealed partial class WidgetVisual : FrameworkElement
{
    public WidgetVisual() => ClipToBounds = true;

    public bool SlateTheme { get; set; }
    public WidgetKind Kind { get; set; }
    public string Caption { get; set; } = "";
    public string Value { get; set; } = "";
    public string FontName { get; set; } = "Segoe UI";
    public Color Accent { get; set; } = Color.FromRgb(211, 189, 142);
    public bool EditMode { get; set; }
    public IReadOnlyList<SkillCountdown> Skills { get; set; } = [];
    public IReadOnlyList<bool> AutoCastSkills { get; set; } = [];
    public int? HoveredSkill { get; set; }
    public double HoverProgress { get; set; }
    public int? SingleSkillSlot { get; set; }
    public string PartyName { get; set; } = "Player";
    public HeroClass PartyClass { get; set; } = HeroClass.Wizard;
    public RiftMapDrawing MapDrawing { get; set; } = RiftMapDrawing.Waiting;

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
            return;
        if (Kind == WidgetKind.RiftMap && AlignMap)
        {
            DrawAlignedMap(dc);
            return;
        }
        var accent = new SolidColorBrush(Accent);
        if (SlateTheme && !EditMode && Kind != WidgetKind.Cooldowns && !HudLayout.IsParty(Kind))
            dc.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(55, 35, 43, 55)),
                null,
                new Rect(RenderSize)
            );
        if (EditMode)
        {
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(150, 211, 189, 142)), 1)
            {
                DashStyle = DashStyles.Dash,
            };
            dc.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(90, 12, 10, 9)),
                pen,
                new Rect(0, 0, ActualWidth, ActualHeight)
            );
        }
        if (Kind == WidgetKind.Cooldowns)
        {
            DrawSkills(dc, accent);
            return;
        }
        if (Kind == WidgetKind.RiftMap)
        {
            DrawMap(dc, accent);
            return;
        }
        if (HudLayout.IsParty(Kind))
        {
            DrawElements(dc);
            return;
        }
        if (Kind == WidgetKind.RiftProgress)
        {
            Text(dc, Value, 20, accent, 2, 1, ActualWidth - 4, bold: true);
            return;
        }
        if (Kind == WidgetKind.BuildNotes)
        {
            Text(dc, Value, 11, accent, 4, 3, ActualWidth - 8);
            return;
        }

        // A thin bronze rule and quiet label blend into the game's dark fantasy HUD.
        dc.DrawLine(
            new Pen(new SolidColorBrush(Color.FromArgb(85, 211, 189, 142)), 1),
            new(0, 4),
            new(0, ActualHeight - 4)
        );
        double labelWidth = Text(
            dc,
            Caption,
            9,
            new SolidColorBrush(Color.FromRgb(170, 157, 132)),
            7,
            7,
            ActualWidth - 12
        );
        Text(
            dc,
            Value,
            13,
            accent,
            12 + labelWidth,
            4,
            Math.Max(1, ActualWidth - labelWidth - 18),
            bold: true
        );
    }

    public bool AlignMap { get; set; }
    public double MapScale { get; set; } = 1;
    public double MapOffsetX { get; set; }
    public double MapOffsetY { get; set; }

    void DrawAlignedMap(DrawingContext dc)
    {
        if (MapDrawing.Player is not { } player)
            return;
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
        var fill = new SolidColorBrush(Color.FromArgb(45, Accent.R, Accent.G, Accent.B));
        var pen = new Pen(
            new SolidColorBrush(Color.FromArgb(140, Accent.R, Accent.G, Accent.B)),
            .6
        );
        foreach (
            var polygon in MapDrawing.Polygons.Where(p =>
                !p.CachedFootprint && p.Points.Length >= 3
            )
        )
        {
            Point Project(MapPoint p)
            {
                var projected = MinimapProjection.Position(p, player, ActualWidth, MapScale);
                return new(
                    projected.X + MapOffsetX * ActualWidth / 350,
                    projected.Y + MapOffsetY * ActualWidth / 350
                );
            }
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(Project(polygon.Points[0]), true, true);
                context.PolyLineTo(polygon.Points.Skip(1).Select(Project).ToArray(), true, false);
            }
            geometry.Freeze();
            dc.DrawGeometry(fill, pen, geometry);
        }
        MapMarkerRenderer.Draw(
            dc,
            MapDrawing.Markers,
            position =>
            {
                var panelPoint = new MapPoint(
                    (position.Y - position.X) * .7071067811865476,
                    (position.X + position.Y) * .3535533905932738
                );
                var projected = MinimapProjection.Position(
                    panelPoint,
                    player,
                    ActualWidth,
                    MapScale
                );
                return new(
                    projected.X + MapOffsetX * ActualWidth / 350,
                    projected.Y + MapOffsetY * ActualWidth / 350
                );
            },
            new Rect(0, 0, ActualWidth, ActualHeight)
        );
        dc.Pop();
    }

    void DrawMap(DrawingContext dc, Brush accent)
    {
        if (AlignMap)
        {
            DrawAlignedMap(dc);
            return;
        }
        Text(dc, "NEARBY MAP", 10, accent, 6, 3, Math.Max(1, ActualWidth - 12), bold: true);
        if (MapDrawing.Polygons.Count == 0 || ActualHeight < 65 || ActualWidth < 65)
        {
            Text(dc, MapDrawing.Message, 10, accent, 6, 22, Math.Max(1, ActualWidth - 12));
            return;
        }
        var extents = MapDrawing.Polygons.Where(p => !p.CachedFootprint).ToArray();
        if (extents.Length == 0)
            extents = MapDrawing.Polygons.ToArray();
        double minX = extents.SelectMany(p => p.Points).Min(p => p.X);
        double minY = extents.SelectMany(p => p.Points).Min(p => p.Y);
        double maxX = extents.SelectMany(p => p.Points).Max(p => p.X);
        double maxY = extents.SelectMany(p => p.Points).Max(p => p.Y);
        double width = Math.Max(1, maxX - minX),
            height = Math.Max(1, maxY - minY);
        double scale = Math.Min((ActualWidth - 12) / width, (ActualHeight - 52) / height);
        double left = (ActualWidth - width * scale) / 2;
        double top = 22 + (ActualHeight - 52 - height * scale) / 2;
        var fill = new SolidColorBrush(Color.FromArgb(95, Accent.R, Accent.G, Accent.B));
        var edge = new Pen(
            new SolidColorBrush(Color.FromArgb(160, Accent.R, Accent.G, Accent.B)),
            .5
        );
        var cached = new Pen(
            new SolidColorBrush(Color.FromArgb(55, Accent.R, Accent.G, Accent.B)),
            .6
        )
        {
            DashStyle = DashStyles.Dash,
        };
        dc.PushClip(new RectangleGeometry(new Rect(4, 20, ActualWidth - 8, ActualHeight - 45)));
        foreach (var polygon in MapDrawing.Polygons)
        {
            if (polygon.Points.Length < 3)
                continue;
            Point Project(MapPoint point) =>
                new(left + (point.X - minX) * scale, top + (point.Y - minY) * scale);
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(Project(polygon.Points[0]), !polygon.CachedFootprint, true);
                context.PolyLineTo(polygon.Points.Skip(1).Select(Project).ToArray(), true, false);
            }
            geometry.Freeze();
            dc.DrawGeometry(
                polygon.CachedFootprint ? null : fill,
                polygon.CachedFootprint ? cached : edge,
                geometry
            );
        }
        MapMarkerRenderer.Draw(
            dc,
            MapDrawing.Markers,
            position =>
            {
                double x = (position.Y - position.X) * .7071067811865476,
                    y = (position.X + position.Y) * .3535533905932738;
                return new(left + (x - minX) * scale, top + (y - minY) * scale);
            },
            new Rect(4, 20, ActualWidth - 8, ActualHeight - 45)
        );
        dc.Pop();
        Text(
            dc,
            MapDrawing.Message,
            8,
            accent,
            6,
            ActualHeight - 20,
            Math.Max(1, ActualWidth - 12)
        );
    }

    void DrawSkills(DrawingContext dc, Brush accent)
    {
        double cell = SingleSkillSlot is null ? ActualWidth / 6 : ActualWidth;
        for (int i = SingleSkillSlot ?? 0; i < Math.Min(6, Skills.Count); i++)
        {
            if (SingleSkillSlot is { } selected && i != selected)
                break;
            var skill = Skills[i];
            double x = SingleSkillSlot is null ? cell * i : 0;
            bool automatic = i < AutoCastSkills.Count && AutoCastSkills[i];
            if (automatic)
            {
                dc.DrawRoundedRectangle(
                    new SolidColorBrush(Color.FromArgb(28, 60, 140, 70)),
                    new Pen(new SolidColorBrush(Color.FromArgb(160, 114, 191, 118)), 1),
                    new Rect(x + 2, 1, Math.Max(1, cell - 4), Math.Max(1, ActualHeight - 2)),
                    3,
                    3
                );
                Text(
                    dc,
                    "AUTO",
                    8,
                    Brushes.LightGreen,
                    x + 2,
                    0,
                    cell - 4,
                    center: true,
                    bold: true
                );
            }
            if (HoveredSkill == i && HoverProgress > 0)
            {
                dc.DrawRectangle(
                    new SolidColorBrush(Color.FromArgb(110, 30, 25, 15)),
                    null,
                    new Rect(x + 3, ActualHeight - 2, Math.Max(0, cell - 6), 2)
                );
                dc.DrawRectangle(
                    accent,
                    null,
                    new Rect(
                        x + 3,
                        ActualHeight - 2,
                        Math.Max(0, cell - 6) * Math.Clamp(HoverProgress, 0, 1),
                        2
                    )
                );
            }
            if (EditMode)
                Text(dc, skill.Key, 9, accent, x + 3, 0, cell - 6);
            if (skill.RemainingSeconds <= 0 && !skill.IsUnknown)
                continue;
            double circle = Math.Min(34, Math.Min(cell - 4, ActualHeight - 9));
            var centre = new Point(x + cell / 2, ActualHeight / 2 + 3);
            dc.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(145, 8, 6, 5)),
                null,
                centre,
                circle / 2,
                circle / 2
            );
            string value =
                skill.RemainingSeconds < 10
                    ? skill.RemainingSeconds.ToString("0.0", CultureInfo.InvariantCulture)
                    : Math.Ceiling(skill.RemainingSeconds).ToString(CultureInfo.InvariantCulture);
            value =
                skill.IsUnknown ? "?"
                : skill.IsEstimated ? "~" + value
                : value;
            var brush = skill.RemainingSeconds <= 3 ? Brushes.White : accent;
            Text(dc, value, 20, brush, x + 2, centre.Y - 14, cell - 4, center: true, bold: true);
            dc.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(95, 0, 0, 0)),
                null,
                new Rect(x + 6, ActualHeight - 4, Math.Max(0, cell - 12), 2)
            );
            dc.DrawRectangle(
                accent,
                null,
                new Rect(
                    x + 6,
                    ActualHeight - 4,
                    Math.Max(0, cell - 12) * Math.Clamp(skill.Fraction, 0, 1),
                    2
                )
            );
        }
    }

    void DrawElements(DrawingContext dc)
    {
        DrawPlayerBuffs(dc);
    }

    public static Color ElementColor(Element element) =>
        element switch
        {
            Element.Arcane => Color.FromRgb(170, 101, 220),
            Element.Cold => Color.FromRgb(103, 188, 224),
            Element.Fire => Color.FromRgb(220, 107, 54),
            Element.Holy => Color.FromRgb(225, 204, 120),
            Element.Lightning => Color.FromRgb(131, 149, 236),
            Element.Physical => Color.FromRgb(174, 161, 143),
            _ => Color.FromRgb(123, 177, 81),
        };

    double Text(
        DrawingContext dc,
        string value,
        double size,
        Brush brush,
        double x,
        double y,
        double width,
        bool center = false,
        bool bold = false
    )
    {
        if (width <= 0 || y >= ActualHeight)
            return 0;
        var typeface = new Typeface(
            new FontFamily(FontName),
            FontStyles.Normal,
            bold ? FontWeights.SemiBold : FontWeights.Normal,
            FontStretches.Normal
        );
        FormattedText Make(Brush ink) =>
            new(
                value,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                size,
                ink,
                VisualTreeHelper.GetDpi(this).PixelsPerDip
            )
            {
                MaxTextWidth = Math.Max(1, width),
                MaxTextHeight = Math.Max(1, ActualHeight - y),
                Trimming = TextTrimming.CharacterEllipsis,
                TextAlignment = center ? TextAlignment.Center : TextAlignment.Left,
            };
        dc.DrawText(Make(Brushes.Black), new(x + 1, y + 1));
        var text = Make(brush);
        dc.DrawText(text, new(x, y));
        return Math.Min(width, text.WidthIncludingTrailingWhitespace);
    }
}
