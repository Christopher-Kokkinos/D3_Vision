using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using D3Vision.Core;

namespace D3Vision.UI;

/// <summary>Inventory-style equipment reference; every slot fits without scrolling or interaction.</summary>
public sealed class MyriamGuideView : Border
{
    readonly Grid content = new();
    LeaderboardBuild? previous;
    string previousStatus = "";

    public MyriamGuideView()
    {
        Background = new SolidColorBrush(Color.FromArgb(245, 20, 18, 23));
        BorderBrush = new SolidColorBrush(Color.FromRgb(154, 125, 74));
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(5);
        Padding = new Thickness(12);
        content.RowDefinitions.Add(new() { Height = GridLength.Auto });
        content.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Child = content;
    }

    static TextBlock Text(string value, double size, Brush color) =>
        new()
        {
            Text = value,
            FontSize = size,
            Foreground = color,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4),
        };

    static string Compact(string text)
    {
        text = text.Replace("Critical Hit Chance Increased by", "Critical chance")
            .Replace("Critical Hit Damage Increased by", "Critical damage")
            .Replace("Reduces cooldown of all skills by", "Cooldown reduction")
            .Replace("Reduces all resource costs by", "Resource cost reduction")
            .Replace("Resistance to All Elements", "All resist")
            .Replace("Attack Speed Increased by", "Attack speed")
            .Replace("Increases Attack Speed by", "Attack speed")
            .Replace("Chance to Deal", "Chance:")
            .Replace("Area Damage on Hit.", "area damage.")
            .Replace("Increases damage against elites by", "Elite damage");
        return text;
    }

    public void Update(LeaderboardBuild? build, string message)
    {
        if (ReferenceEquals(previous, build) && previousStatus == message)
            return;
        previous = build;
        previousStatus = message;
        content.Children.Clear();
        var heading = new StackPanel();
        content.Children.Add(heading);
        heading.Children.Add(Text("MYRIAM - EQUIPMENT & ENCHANT REFERENCE", 15, Brushes.Wheat));
        if (build is null)
        {
            heading.Children.Add(Text(message, 13, Brushes.WhiteSmoke));
            return;
        }
        heading.Children.Add(
            Text(
                $"EU S{build.Season} | {build.Class} | RANK {build.Rank}: {build.Player}",
                14,
                Brushes.Gold
            )
        );
        heading.Children.Add(
            Text(
                $"GR {build.Tier} - {build.ClearTime} | Checked {build.FetchedUtc.ToLocalTime():g}",
                10,
                Brushes.Silver
            )
        );
        heading.Children.Add(
            Text(
                build.Notice,
                10,
                build.Rank == 1 && !build.Offline ? Brushes.LightGreen : Brushes.Khaki
            )
        );
        var board = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        for (int i = 0; i < 3; i++)
            board.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 5; i++)
            board.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        (string Slot, string Title, int Row, int Column)[] slots =
        [
            ("shoulders", "SHOULDERS", 0, 0),
            ("head", "HEAD", 0, 1),
            ("neck", "AMULET", 0, 2),
            ("hands", "GLOVES", 1, 0),
            ("torso", "CHEST", 1, 1),
            ("bracers", "BRACERS", 1, 2),
            ("leftFinger", "LEFT RING", 2, 0),
            ("waist", "BELT", 2, 1),
            ("rightFinger", "RIGHT RING", 2, 2),
            ("mainHand", "MAIN HAND", 3, 0),
            ("legs", "LEGS", 3, 1),
            ("offHand", "OFF HAND", 3, 2),
            ("feet", "BOOTS", 4, 1),
        ];
        foreach (var slot in slots)
        {
            var item = build.Gear.FirstOrDefault(g =>
                g.Slot.Equals(slot.Slot, StringComparison.OrdinalIgnoreCase)
            );
            var body = new StackPanel { Width = 215 };
            body.Children.Add(Text(slot.Title, 10, Brushes.Silver));
            body.Children.Add(Text(item?.Name ?? "No item published", 12, Brushes.Wheat));
            if (item is null || item.PrimaryAffixes.Count == 0)
                body.Children.Add(Text("Affix details unavailable", 10, Brushes.Gray));
            else
                foreach (var affix in item.PrimaryAffixes)
                    body.Children.Add(Text(Compact(affix), 10.5, Brushes.WhiteSmoke));
            var card = new Border
            {
                Margin = new Thickness(3),
                Padding = new Thickness(7),
                Background = new SolidColorBrush(Color.FromRgb(34, 29, 31)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(82, 66, 43)),
                BorderThickness = new Thickness(.5),
                Child = new Viewbox
                {
                    Stretch = Stretch.Uniform,
                    StretchDirection = StretchDirection.DownOnly,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Child = body,
                },
            };
            Grid.SetRow(card, slot.Row);
            Grid.SetColumn(card, slot.Column);
            board.Children.Add(card);
        }
        Grid.SetRow(board, 1);
        content.Children.Add(board);
        var footer = Text(
            "Primary affixes from current published gear; compare the same slot. Not an optimal-roll calculation or the loadout at the ranked clear. All slots fit without scrolling. Close Enchant to dismiss.",
            10,
            Brushes.Silver
        );
        Grid.SetRow(footer, 2);
        content.Children.Add(footer);
    }
}
