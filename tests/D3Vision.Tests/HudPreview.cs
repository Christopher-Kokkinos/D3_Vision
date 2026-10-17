using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using D3Vision.Core;
using D3Vision.Rendering;

static class HudPreview
{
    public static void Save(string path)
    {
        const int width = 1920,
            height = 1080;
        var canvas = new Canvas
        {
            Width = width,
            Height = height,
            Background = new RadialGradientBrush(
                Color.FromRgb(31, 29, 25),
                Color.FromRgb(9, 10, 11)
            ),
        };
        void Add(FrameworkElement element, double x, double y)
        {
            Canvas.SetLeft(element, x);
            Canvas.SetTop(element, y);
            canvas.Children.Add(element);
        }
        Add(
            new TextBlock
            {
                Text = "D3VISION / HUD LAYOUT PREVIEW",
                Foreground = new SolidColorBrush(Color.FromRgb(91, 85, 73)),
                FontSize = 16,
                FontFamily = new FontFamily("Segoe UI"),
            },
            690,
            480
        );
        Add(
            new TextBlock
            {
                Text = "Illustrative backdrop • sample skill times • party bars are visual only",
                Foreground = new SolidColorBrush(Color.FromRgb(91, 85, 73)),
                FontSize = 12,
            },
            690,
            510
        );
        var settings = new Settings();
        HudLayout.Upgrade(settings);
        var names = new[]
        {
            "Player 1 · Wizard",
            "Player 2 · Monk",
            "Player 3 · Necromancer",
            "Player 4 · Barbarian",
        };
        var heroes = new[]
        {
            HeroClass.Wizard,
            HeroClass.Monk,
            HeroClass.Necromancer,
            HeroClass.Barbarian,
        };
        var skillWidget = settings.Widgets.Single(w => w.Kind == WidgetKind.Cooldowns);
        var skillPoint = HudLayout.Resolve(skillWidget, width, height);
        for (int i = 0; i < 6; i++)
            Add(
                new Border
                {
                    Width = 53,
                    Height = 53,
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(67, 57, 41)),
                    Background = new SolidColorBrush(Color.FromRgb(25, 23, 20)),
                    Child = new TextBlock
                    {
                        Text = settings.Skills[i].Key,
                        Foreground = Brushes.DimGray,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        FontSize = 9,
                    },
                },
                skillPoint.X + i * 65 + 6,
                skillPoint.Y - 4
            );
        Add(
            new Border
            {
                Width = 8,
                Height = 210,
                BorderBrush = new SolidColorBrush(Color.FromRgb(86, 72, 51)),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromRgb(28, 22, 29)),
                Child = new Border
                {
                    Height = 183,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Background = new SolidColorBrush(Color.FromRgb(96, 54, 124)),
                },
            },
            width - 110,
            height / 2 - 140
        );
        foreach (var w in settings.Widgets.Where(w => w.Enabled))
        {
            var value = w.Kind switch
            {
                WidgetKind.RiftProgress => "87.5%",
                WidgetKind.GreaterRiftTimer => "08:42",
                WidgetKind.Experience => "12.8B",
                WidgetKind.Deaths => "2",
                WidgetKind.Elites => "18",
                WidgetKind.Goblins => "3",
                WidgetKind.BuildNotes => "",
                _ => "",
            };
            var visual = new WidgetVisual
            {
                Kind = w.Kind,
                Width = w.Width,
                Height = w.Height,
                Value = value,
                Caption = w.Kind switch
                {
                    WidgetKind.GreaterRiftTimer => "GR",
                    WidgetKind.Experience => "XP/H",
                    _ => w.Title,
                },
                Skills =
                [
                    new("1", 12, .4),
                    new("2", 3.2, .2),
                    new("3", 0, 0),
                    new("4", 18, .6),
                    new("LMB", 0, 0),
                    new("RMB", 0, 0),
                ],
            };
            if (HudLayout.IsParty(w.Kind))
            {
                int i = (int)w.Kind - (int)WidgetKind.Party1;
                visual.PartyName = names[i];
                visual.PartyClass = heroes[i];
                Add(
                    new Border
                    {
                        Width = 48,
                        Height = 48,
                        CornerRadius = new CornerRadius(24),
                        BorderThickness = new Thickness(1),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(90, 74, 48)),
                        Background = new SolidColorBrush(Color.FromRgb(26, 24, 21)),
                        Child = new TextBlock
                        {
                            Text = (i + 1).ToString(),
                            Foreground = Brushes.DimGray,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                    },
                    35,
                    w.Y - 4
                );
            }
            var point = HudLayout.Resolve(w, width, height);
            Add(visual, point.X, point.Y);
        }
        canvas.Measure(new Size(width, height));
        canvas.Arrange(new Rect(0, 0, width, height));
        canvas.UpdateLayout();
        var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        image.Render(canvas);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
