using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using D3Vision.Core;
using D3Vision.Rendering;

internal static class PortraitBuffPreview
{
    public static void Save()
    {
        var canvas = new Canvas
        {
            Width = 840,
            Height = 330,
            Background = new SolidColorBrush(Color.FromRgb(15, 18, 22)),
        };
        var title = new TextBlock
        {
            Text = "D3Vision portrait buffs - illustrative states",
            Foreground = Brushes.Bisque,
            FontSize = 19,
        };
        Canvas.SetLeft(title, 22);
        Canvas.SetTop(title, 15);
        canvas.Children.Add(title);
        var states = new[]
        {
            (
                "Fire / Squirt active",
                new PlayerBuffState(
                    0,
                    1,
                    HeroClass.Barbarian,
                    Element.Fire,
                    true,
                    true,
                    10,
                    true,
                    false,
                    9
                )
            ),
            (
                "Cold / Squirt inactive",
                new PlayerBuffState(
                    1,
                    2,
                    HeroClass.Barbarian,
                    Element.Cold,
                    true,
                    true,
                    0,
                    true,
                    false,
                    9
                )
            ),
            (
                "Arcane / buff unreadable",
                new PlayerBuffState(
                    2,
                    3,
                    HeroClass.Wizard,
                    Element.Arcane,
                    true,
                    true,
                    null,
                    false,
                    false,
                    null
                )
            ),
            (
                "Ring mode / remote power",
                new PlayerBuffState(
                    3,
                    4,
                    HeroClass.Necromancer,
                    Element.Poison,
                    true,
                    null,
                    10,
                    false,
                    false,
                    null
                )
            ),
        };
        for (int i = 0; i < states.Length; i++)
        {
            double x = 20 + (i % 2) * 415,
                y = 65 + (i / 2) * 125;
            var label = new TextBlock
            {
                Text = states[i].Item1,
                Foreground = Brushes.Silver,
                FontSize = 12,
            };
            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, y - 15);
            canvas.Children.Add(label);
            var portrait = new Ellipse
            {
                Width = 62,
                Height = 70,
                Fill = new SolidColorBrush(Color.FromRgb(39, 43, 50)),
                Stroke = Brushes.DimGray,
            };
            Canvas.SetLeft(portrait, x + 17);
            Canvas.SetTop(portrait, y + 13);
            canvas.Children.Add(portrait);
            var hero = new TextBlock
            {
                Text = "HERO",
                Foreground = Brushes.Gray,
                FontSize = 10,
            };
            Canvas.SetLeft(hero, x + 32);
            Canvas.SetTop(hero, y + 41);
            canvas.Children.Add(hero);
            var visual = new WidgetVisual
            {
                Width = 185,
                Height = 100,
                Kind = WidgetKind.Party1,
                PlayerBuffs = states[i].Item2,
                StrickenProcs =
                    i == 0 ? 37
                    : i == 1 ? 12
                    : null,
                ConventionDisplay =
                    i == 3 ? ConventionDisplayMode.RingIcon : ConventionDisplayMode.PortraitGlow,
            };
            Canvas.SetLeft(visual, x);
            Canvas.SetTop(visual, y);
            canvas.Children.Add(visual);
        }
        canvas.Measure(new Size(840, 330));
        canvas.Arrange(new Rect(0, 0, 840, 330));
        canvas.UpdateLayout();
        var target = new RenderTargetBitmap(840, 330, 96, 96, PixelFormats.Pbgra32);
        target.Render(canvas);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(target));
        Directory.CreateDirectory("artifacts");
        using var file = File.Create("artifacts/portrait-buffs-preview.png");
        png.Save(file);
    }
}
