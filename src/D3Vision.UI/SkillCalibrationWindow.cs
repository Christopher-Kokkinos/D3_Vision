using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using D3Vision.Core;
using D3Vision.Services;

namespace D3Vision.UI;

/// <summary>Select ready skill artwork in capture coordinates, not overlay/window coordinates.</summary>
public sealed class SkillCalibrationWindow : Window
{
    public SkillCalibrationWindow(
        CaptureFrame frame,
        IReadOnlyList<SkillSettings> skills,
        Func<IReadOnlyList<SkillCalibration?>, Task> save
    )
    {
        Title = "D3Vision • Calibrate ready skill icons";
        Width = 1150;
        Height = 800;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new DockPanel { Margin = new Thickness(12) };
        Content = root;
        var instructions = new TextBlock
        {
            Text =
                "Use an image where the skills are ready. Choose a slot, then drag a box around its square artwork only (exclude border/key labels). Each successful selection is saved immediately; slots advance automatically. No whole screenshot is saved. Repeat calibration after changing skills, UI scale or capture mode.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        };
        DockPanel.SetDock(instructions, Dock.Top);
        root.Children.Add(instructions);
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 8),
        };
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        var picker = new ComboBox
        {
            Width = 130,
            ItemsSource = skills
                .Take(SkillSlots.Count)
                .Select((s, i) => $"Slot {i + 1} • {s.Key}")
                .ToArray(),
            SelectedIndex = 0,
        };
        toolbar.Children.Add(picker);
        var status = new TextBlock
        {
            Margin = new Thickness(12, 4, 0, 0),
            Text = "Select an icon in the captured image.",
        };
        toolbar.Children.Add(status);
        var calibrations = skills.Take(SkillSlots.Count).Select(s => s.Calibration).ToArray();
        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        var clear = new Button
        {
            Content = "Clear selected slot",
            Margin = new Thickness(4),
            Padding = new Thickness(10),
        };
        var commit = new Button
        {
            Content = "Done",
            Margin = new Thickness(4),
            Padding = new Thickness(10),
        };
        footer.Children.Add(clear);
        footer.Children.Add(commit);
        bool saving = false;
        commit.Click += (_, _) =>
        {
            if (saving)
                return;
            if (!calibrations.Any(c => c is not null))
            {
                status.Text =
                    "Nothing has been calibrated. Drag a box around at least one ready icon.";
                return;
            }
            Close();
        };

        var source = BitmapSource.Create(
            frame.Width,
            frame.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            frame.Bgra,
            frame.Width * 4
        );
        source.Freeze();
        var canvas = new Canvas
        {
            Width = frame.Width,
            Height = frame.Height,
            Background = Brushes.Transparent,
        };
        canvas.Children.Add(
            new Image
            {
                Source = source,
                Width = frame.Width,
                Height = frame.Height,
                IsHitTestVisible = false,
            }
        );
        var outlines = new Rectangle[SkillSlots.Count];
        for (int i = 0; i < SkillSlots.Count; i++)
        {
            outlines[i] = new Rectangle
            {
                Stroke = Brushes.LimeGreen,
                StrokeThickness = 2,
                IsHitTestVisible = false,
            };
            canvas.Children.Add(outlines[i]);
        }
        void Redraw()
        {
            for (int i = 0; i < SkillSlots.Count; i++)
            {
                var c = calibrations[i];
                var r = outlines[i];
                r.Visibility = c is null ? Visibility.Collapsed : Visibility.Visible;
                if (c is null)
                    continue;
                r.Width = c.Width * frame.Width;
                r.Height = c.Height * frame.Height;
                Canvas.SetLeft(r, c.X * frame.Width);
                Canvas.SetTop(r, c.Y * frame.Height);
                r.Stroke = i == picker.SelectedIndex ? Brushes.Gold : Brushes.LimeGreen;
            }
        }
        clear.Click += async (_, _) =>
        {
            if (saving)
                return;
            calibrations[picker.SelectedIndex] = null;
            Redraw();
            await PersistAsync();
        };
        picker.SelectionChanged += (_, _) => Redraw();
        var selection = new Rectangle
        {
            Stroke = Brushes.Gold,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(35, 255, 220, 100)),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };
        canvas.Children.Add(selection);
        Point start = default;
        bool dragging = false;
        canvas.MouseLeftButtonDown += (_, e) =>
        {
            if (saving)
                return;
            start = e.GetPosition(canvas);
            dragging = true;
            canvas.CaptureMouse();
            selection.Visibility = Visibility.Visible;
        };
        Rect Selected(Point current) =>
            new(
                new Point(
                    Math.Clamp(Math.Min(start.X, current.X), 0, frame.Width),
                    Math.Clamp(Math.Min(start.Y, current.Y), 0, frame.Height)
                ),
                new Point(
                    Math.Clamp(Math.Max(start.X, current.X), 0, frame.Width),
                    Math.Clamp(Math.Max(start.Y, current.Y), 0, frame.Height)
                )
            );
        canvas.MouseMove += (_, e) =>
        {
            if (!dragging)
                return;
            var rect = Selected(e.GetPosition(canvas));
            selection.Width = rect.Width;
            selection.Height = rect.Height;
            Canvas.SetLeft(selection, rect.X);
            Canvas.SetTop(selection, rect.Y);
        };
        canvas.MouseLeftButtonUp += async (_, e) =>
        {
            if (!dragging)
                return;
            dragging = false;
            canvas.ReleaseMouseCapture();
            selection.Visibility = Visibility.Collapsed;
            var rect = Selected(e.GetPosition(canvas));
            try
            {
                calibrations[picker.SelectedIndex] = CooldownSweepReader.Calibrate(
                    frame,
                    rect.X / frame.Width,
                    rect.Y / frame.Height,
                    rect.Width / frame.Width,
                    rect.Height / frame.Height
                );
                int completedSlot = picker.SelectedIndex;
                Redraw();
                await PersistAsync();
                int next = Array.FindIndex(calibrations.ToArray(), c => c is null);
                if (next >= 0)
                    picker.SelectedIndex = next;
                else
                    picker.SelectedIndex = Math.Min(SkillSlots.Count - 1, completedSlot + 1);
            }
            catch (ArgumentException ex)
            {
                status.Text = ex.Message;
            }
        };
        async Task PersistAsync()
        {
            saving = true;
            canvas.IsEnabled = picker.IsEnabled = clear.IsEnabled = commit.IsEnabled = false;
            try
            {
                await save(calibrations.ToArray());
                status.Text =
                    $"Saved {calibrations.Count(c => c is not null)}/7 skill/potion icons. Red dots appear in game after closing Settings.";
            }
            catch (Exception ex)
            {
                status.Text = "Calibration could not be saved: " + ex.Message;
            }
            finally
            {
                saving = false;
                canvas.IsEnabled = picker.IsEnabled = clear.IsEnabled = commit.IsEnabled = true;
            }
        }
        root.Children.Add(new Viewbox { Child = canvas, Stretch = Stretch.Uniform });
        Redraw();
    }
}
