using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using D3Vision.Core;
using D3Vision.Rendering;

internal static class MapMarkerPreview
{
    public static void Save()
    {
        var canvas = new Canvas
        {
            Width = 480,
            Height = 450,
            Background = new SolidColorBrush(Color.FromRgb(18, 20, 25)),
        };
        void Text(string text, double x, double y, int size = 11)
        {
            var label = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontSize = size,
            };
            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, y);
            canvas.Children.Add(label);
        }
        var samples = new[]
        {
            ("Champion", MapMarkerKind.Champion, LootRank.Normal),
            ("Rare elite", MapMarkerKind.RareElite, LootRank.Normal),
            ("Rare minion", MapMarkerKind.RareMinion, LootRank.Normal),
            ("Unique", MapMarkerKind.Unique, LootRank.Normal),
            ("Boss", MapMarkerKind.Boss, LootRank.Normal),
            ("Legendary", MapMarkerKind.Legendary, LootRank.Normal),
            ("Ancient legendary", MapMarkerKind.Legendary, LootRank.Ancient),
            ("Set item", MapMarkerKind.SetItem, LootRank.Normal),
            ("Ancient set", MapMarkerKind.SetItem, LootRank.Ancient),
            ("Primal legendary", MapMarkerKind.Legendary, LootRank.Primal),
            ("Primal set", MapMarkerKind.SetItem, LootRank.Primal),
        };
        var markers = new List<MapMarker>();
        for (int i = 0; i < samples.Length; i++)
        {
            double x = 45 + (i % 3) * 125,
                y = 35 + (i / 3) * 85,
                dx = x - 175,
                dy = y - 175;
            markers.Add(
                new(
                    i,
                    new((dy - dx) / Math.Sqrt(2), (dy + dx) / Math.Sqrt(2)),
                    samples[i].Item2,
                    samples[i].Item3
                )
            );
        }
        var visual = new WidgetVisual
        {
            Kind = WidgetKind.RiftMap,
            AlignMap = true,
            Width = 350,
            Height = 350,
            MapDrawing = new([], "Sample markers")
            {
                Player = new MapPoint(0, 0),
                Markers = markers,
            },
        };
        Canvas.SetLeft(visual, 25);
        Canvas.SetTop(visual, 55);
        canvas.Children.Add(visual);
        Text("D3Vision - marker style preview", 20, 14, 18);
        Text("Illustrative samples; symbols shown at native HUD size", 20, 40);
        for (int i = 0; i < samples.Length; i++)
            Text(samples[i].Item1, 25 + 45 + (i % 3) * 125 - 30, 55 + 35 + (i / 3) * 85 + 14);
        canvas.Measure(new Size(480, 450));
        canvas.Arrange(new Rect(0, 0, 480, 450));
        canvas.UpdateLayout();
        var bitmap = new RenderTargetBitmap(480, 450, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(canvas);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory("artifacts");
        using var file = File.Create("artifacts/map-marker-preview.png");
        encoder.Save(file);
    }
}
