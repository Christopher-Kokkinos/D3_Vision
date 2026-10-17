using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using D3Vision.Core;
using D3Vision.Infrastructure;
using D3Vision.Rendering;
using D3Vision.Services;

internal static class RiftMapPreview
{
    public static void Save()
    {
        var snapshot = new DiabloMapReader().Capture();
        var canvas = new Canvas
        {
            Width = 420,
            Height = 340,
            Background = new SolidColorBrush(Color.FromRgb(18, 20, 25)),
        };
        var title = new TextBlock
        {
            Text = "D3Vision • live nearby map",
            Foreground = Brushes.White,
            FontSize = 16,
        };
        Canvas.SetLeft(title, 20);
        Canvas.SetTop(title, 15);
        canvas.Children.Add(title);
        var visual = new WidgetVisual
        {
            Kind = WidgetKind.RiftMap,
            MapDrawing = RiftMapGeometry.Build(snapshot),
            Width = 320,
            Height = 240,
            Accent = Color.FromRgb(211, 189, 142),
        };
        Canvas.SetLeft(visual, 50);
        Canvas.SetTop(visual, 55);
        canvas.Children.Add(visual);
        canvas.Measure(new Size(420, 340));
        canvas.Arrange(new Rect(0, 0, 420, 340));
        canvas.UpdateLayout();
        var target = new RenderTargetBitmap(420, 340, 96, 96, PixelFormats.Pbgra32);
        target.Render(canvas);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(target));
        Directory.CreateDirectory("artifacts/reverse-engineering");
        using var file = File.Create("artifacts/reverse-engineering/wpf-map-preview.png");
        png.Save(file);
    }
}
