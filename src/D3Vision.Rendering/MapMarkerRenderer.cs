using System.Windows;
using System.Windows.Media;
using D3Vision.Core;

namespace D3Vision.Rendering;

internal static class MapMarkerRenderer
{
    static SolidColorBrush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    static readonly Brush Champion = Brush("#4DA6FF"),
        Rare = Brush("#FFD14A"),
        Minion = Brush("#D69A40"),
        Unique = Brush("#CC77FF"),
        Boss = Brush("#FF704D"),
        Legendary = Brush("#FFFFB84D"),
        Set = Brush("#FF65F578"),
        AncientLegendary = Brush("#FFFFF1C7"),
        AncientSet = Brush("#FFC5FFAB"),
        Primal = Brush("#FFFF3D3D");
    static readonly Geometry Star = MakeStar(false),
        Pentagram = MakeStar(true);

    static Geometry MakeStar(bool pentagram)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            int count = pentagram ? 5 : 10;
            Point Vertex(int index)
            {
                double angle =
                    -Math.PI / 2 + (pentagram ? index * 4 * Math.PI / 5 : index * Math.PI / 5);
                double radius = pentagram || index % 2 == 0 ? 1 : .43;
                return new(Math.Cos(angle) * radius, Math.Sin(angle) * radius);
            }
            context.BeginFigure(Vertex(0), !pentagram, true);
            context.PolyLineTo(
                Enumerable.Range(1, count - 1).Select(Vertex).ToArray(),
                true,
                false
            );
        }
        geometry.Freeze();
        return geometry;
    }

    public static void Draw(
        DrawingContext dc,
        IReadOnlyList<MapMarker> markers,
        Func<MapPoint, Point> project,
        Rect clip
    )
    {
        var occupied = new List<Rect>();
        foreach (
            var marker in markers
                .OrderBy(m => m.Kind >= MapMarkerKind.Legendary ? 1 : 0)
                .ThenBy(m => m.ActorId)
        )
        {
            var point = project(marker.Position);
            if (!clip.Contains(point))
                continue;
            bool loot = marker.Kind is MapMarkerKind.Legendary or MapMarkerKind.SetItem;
            if (loot)
            {
                var anchor = point;
                foreach (
                    var offset in new[]
                    {
                        new Vector(0, 0),
                        new Vector(18, 0),
                        new Vector(-18, 0),
                        new Vector(0, -18),
                        new Vector(0, 18),
                        new Vector(18, -18),
                        new Vector(-18, -18),
                        new Vector(18, 18),
                        new Vector(-18, 18),
                    }
                )
                {
                    var candidate = anchor + offset;
                    var bounds = new Rect(candidate.X - 8, candidate.Y - 8, 16, 16);
                    if (clip.Contains(bounds) && !occupied.Any(r => r.IntersectsWith(bounds)))
                    {
                        point = candidate;
                        break;
                    }
                }
                occupied.Add(new(point.X - 8, point.Y - 8, 16, 16));
                if (point != anchor)
                {
                    dc.DrawLine(new Pen(Brushes.Black, 2), anchor, point);
                    dc.DrawLine(
                        new Pen(
                            marker.Rank == LootRank.Primal ? Primal
                                : marker.Kind == MapMarkerKind.SetItem ? AncientSet
                                : AncientLegendary,
                            .7
                        ),
                        anchor,
                        point
                    );
                    dc.DrawEllipse(Brushes.White, null, anchor, 1, 1);
                }
            }
            DrawSymbol(dc, marker, point);
        }
    }

    static void DrawSymbol(DrawingContext dc, MapMarker marker, Point point)
    {
        bool loot = marker.Kind is MapMarkerKind.Legendary or MapMarkerKind.SetItem;
        Brush color = marker.Kind switch
        {
            MapMarkerKind.Champion => Champion,
            MapMarkerKind.RareElite => Rare,
            MapMarkerKind.RareMinion => Minion,
            MapMarkerKind.Unique => Unique,
            MapMarkerKind.Boss => Boss,
            MapMarkerKind.SetItem => marker.Rank == LootRank.Normal ? Set : AncientSet,
            _ => marker.Rank == LootRank.Normal ? Legendary : AncientLegendary,
        };
        if (loot && marker.Rank == LootRank.Primal)
            color = Primal;
        double size =
            loot
                ? (
                    marker.Rank == LootRank.Primal ? 8
                    : marker.Rank == LootRank.Ancient ? 6.5
                    : 5
                )
            : marker.Kind == MapMarkerKind.RareMinion ? 2.5
            : marker.Kind == MapMarkerKind.Boss ? 5
            : 3.5;
        if (loot)
        {
            bool enhanced = marker.Rank != LootRank.Normal;
            // Layered vector halos avoid a per-frame blur effect on the overlay.
            dc.PushOpacity(enhanced ? .20 : .10);
            dc.DrawEllipse(color, null, point, size + 6, size + 6);
            dc.Pop();
            dc.PushOpacity(enhanced ? .36 : .20);
            dc.DrawEllipse(color, null, point, size + 3, size + 3);
            dc.Pop();
            dc.PushOpacity(enhanced ? .48 : .30);
            dc.DrawEllipse(color, null, point, size + 1, size + 1);
            dc.Pop();
        }
        if (!loot)
        {
            dc.DrawEllipse(color, new Pen(Brushes.Black, 1.2), point, size, size);
            return;
        }
        dc.PushTransform(new TranslateTransform(point.X, point.Y));
        dc.PushTransform(new ScaleTransform(size, size));
        if (marker.Rank == LootRank.Primal)
        {
            dc.DrawEllipse(null, new Pen(Brushes.Black, .48), new(), 1, 1);
            dc.DrawGeometry(null, new Pen(Brushes.Black, .48), Pentagram);
            dc.DrawEllipse(null, new Pen(color, .24), new(), 1, 1);
            dc.DrawGeometry(null, new Pen(color, .24), Pentagram);
        }
        else
        {
            dc.DrawGeometry(color, new Pen(Brushes.Black, .3), Star);
            dc.DrawGeometry(null, new Pen(color, .14), Star);
            // A pale centre gives the star a luminous core while retaining its tier colour.
            dc.PushOpacity(marker.Rank == LootRank.Ancient ? .90 : .55);
            dc.PushTransform(new ScaleTransform(.46, .46));
            dc.DrawGeometry(Brushes.White, null, Star);
            dc.Pop();
            dc.Pop();
        }
        dc.Pop();
        dc.Pop();
    }
}
