using System.Windows;
using System.Windows.Media;
using D3Vision.Core;

namespace D3Vision.Rendering;

public sealed partial class WidgetVisual
{
    public PlayerBuffState? PlayerBuffs { get; set; }
    public int? StrickenProcs { get; set; }
    public ConventionDisplayMode ConventionDisplay { get; set; }

    void DrawPlayerBuffs(DrawingContext dc)
    {
        if (PlayerBuffs is null && !EditMode)
            return;
        dc.PushTransform(new ScaleTransform(ActualWidth / 185, ActualHeight / 100));
        var buff = PlayerBuffs;
        Color colour = buff?.ActiveElement is { } element ? ElementColor(element) : Colors.Gray;
        var ink = new SolidColorBrush(colour);
        bool active = buff?.ActiveElement is not null;
        if (ConventionDisplay == ConventionDisplayMode.PortraitGlow && (active || EditMode))
        {
            // Layered translucent strokes leave the portrait artwork completely visible.
            for (int layer = 7; layer >= 1; layer--)
                dc.DrawEllipse(
                    null,
                    new Pen(
                        new SolidColorBrush(
                            Color.FromArgb(
                                (byte)(active ? 12 + (8 - layer) * 8 : 10),
                                colour.R,
                                colour.G,
                                colour.B
                            )
                        ),
                        layer * 2
                    ),
                    new Point(48, 48),
                    34,
                    38
                );
            dc.DrawEllipse(null, new Pen(ink, active ? 1.8 : 1), new Point(48, 48), 34, 38);
        }
        if (buff?.ConventionEquipped == true || EditMode)
        {
            var ring = new Point(105, 24);
            dc.DrawEllipse(null, new Pen(Brushes.Black, 6), ring, 10, 11);
            dc.DrawEllipse(null, new Pen(active ? ink : Brushes.DimGray, 3), ring, 10, 11);
            dc.DrawEllipse(
                active ? ink : Brushes.Gray,
                new Pen(Brushes.Goldenrod, 1),
                new(105, 12),
                5,
                4
            );
        }
        if (buff?.SquirtsEquipped == true || buff?.SquirtsStacks is not null || EditMode)
        {
            bool? up = buff?.SquirtsStacks is { } stacks ? stacks > 0 : null;
            var necklace = up == true ? Brushes.Gold : Brushes.Gray;
            dc.DrawEllipse(null, new Pen(Brushes.Black, 5), new(105, 55), 11, 9);
            dc.DrawEllipse(null, new Pen(necklace, 2), new(105, 55), 11, 9);
            var gem = Geometry.Parse("M 105,59 L 112,66 L 105,75 L 98,66 Z");
            dc.DrawGeometry(necklace, new Pen(Brushes.Black, 1), gem);
            if (up == false)
            {
                dc.DrawLine(new Pen(Brushes.Black, 5), new(94, 48), new(117, 75));
                dc.DrawLine(new Pen(Brushes.Black, 5), new(117, 48), new(94, 75));
                dc.DrawLine(new Pen(Brushes.Red, 3), new(94, 48), new(117, 75));
                dc.DrawLine(new Pen(Brushes.Red, 3), new(117, 48), new(94, 75));
            }
        }
        dc.Pop();
        // Text uses actual widget size, keeping trimming and DPI handling consistent.
        double sx = ActualWidth / 185,
            sy = ActualHeight / 100;
        if (buff?.ConventionEquipped == true || EditMode)
            Text(
                dc,
                buff?.ActiveElement?.ToString() ?? "CoE ?",
                10,
                active ? ink : Brushes.Gray,
                121 * sx,
                17 * sy,
                62 * sx
            );
        if (buff?.SquirtsEquipped == true || buff?.SquirtsStacks is not null || EditMode)
            Text(
                dc,
                buff?.SquirtsStacks is { } count
                    ? $"{count}/10{(buff.SquirtsEquipped != true ? "*" : "")}"
                    : "Squirt ?",
                10,
                Brushes.Gold,
                122 * sx,
                55 * sy,
                61 * sx
            );
        if (buff?.StrickenEquipped == true || EditMode)
            Text(
                dc,
                StrickenReadout.Text(buff, StrickenProcs) ?? "Stricken ready",
                10,
                Brushes.LightPink,
                89 * sx,
                82 * sy,
                95 * sx
            );
        if (EditMode)
            Text(dc, PartyName, 9, Brushes.Gray, 3, 0, 88 * sx);
    }
}
