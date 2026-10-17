using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using D3Vision.Core;
using D3Vision.Infrastructure;

namespace D3Vision.UI;

public sealed class SettingsWindow : Window
{
    public SettingsWindow(MainViewModel vm, Action apply, Action quit, Action? calibrate = null)
    {
        Topmost = true;
        Title = "D3Vision • Settings";
        Width = 680;
        Height = 780;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(25, 29, 36));
        Foreground = Brushes.White;
        var panel = new StackPanel { Margin = new Thickness(22) };
        Content = new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        void Label(string text) =>
            panel.Children.Add(
                new TextBlock
                {
                    Text = text,
                    Margin = new Thickness(0, 12, 0, 5),
                    TextWrapping = TextWrapping.Wrap,
                }
            );
        void Combo(string label, object items, object selected, Action<object> change)
        {
            Label(label);
            var combo = new ComboBox
            {
                ItemsSource = (System.Collections.IEnumerable)items,
                SelectedItem = selected,
            };
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is { } item)
                    change(item);
            };
            panel.Children.Add(combo);
        }
        Label("D3VISION    F10 visibility • F11 interaction • F12 settings");
        Label(
            "Diablo III display mode must be Windowed (Fullscreen) or Windowed. Exclusive Fullscreen hides desktop overlays. The yellow border is Windows' capture indicator, not an overlay widget."
        );
        Combo(
            "Theme",
            new[] { "Obsidian", "Slate" },
            vm.Settings.Theme,
            x => vm.Settings.Theme = (string)x
        );
        Combo(
            "Font",
            Fonts.SystemFontFamilies.Select(f => f.Source).Order().ToArray(),
            vm.Settings.Font,
            x => vm.Settings.Font = (string)x
        );
        Combo(
            "Monitor",
            Enumerable.Range(0, Native.Monitors().Count).ToArray(),
            vm.Settings.Monitor,
            x => vm.Settings.Monitor = (int)x
        );
        var whole = new CheckBox
        {
            Content = "Capture whole selected monitor",
            IsChecked = vm.Settings.WholeScreen,
            Margin = new Thickness(0, 14, 0, 4),
        };
        whole.Click += (_, _) => vm.Settings.WholeScreen = whole.IsChecked == true;
        panel.Children.Add(whole);
        Label("HUD alignment");
        Label(
            "F11 shows drag handles. Move each annotation beside its game control, then lock again. Alignment is a starting preset for borderless/fullscreen windowed play."
        );
        panel.Children.Add(
            new Button
            {
                Content = "Reset to game HUD layout",
                Padding = new Thickness(8),
                Command = new Command(() =>
                {
                    HudLayout.ApplyPreset(vm.Settings);
                    apply();
                }),
            }
        );
        Label("Skill cooldown source");
        Label(
            "Auto-cast: with VisualRead active and the overlay locked, hover a calibrated skill or its badge for 3 seconds to toggle. Move away before casting resumes. AUTO marks armed slots. Unknown/cooling skills are skipped. Leaving the game, hiding/editing the overlay, opening settings, or pressing Escape/Enter/menu keys disarms every slot. Mouse skills cast at the current cursor; toggles are not saved."
        );
        panel.Children.Add(
            new Button
            {
                Content = "Stop all auto-casting",
                Padding = new Thickness(8),
                Command = new Command(vm.AutoCast.StopAll),
            }
        );
        Combo(
            "Reading mode",
            Enum.GetValues<CooldownMode>(),
            vm.Settings.CooldownMode,
            value =>
            {
                vm.Settings.CooldownMode = (CooldownMode)value;
                vm.ResetCooldowns();
            }
        );
        Label(
            "VisualRead reads the shaded sweep from calibrated skill icons. Wait until all selected skills are ready, then calibrate their artwork. Inferred seconds are marked ~; ? means unknown or unreadable. Exact live seconds are not available from a shaded sweep alone."
        );
        panel.Children.Add(
            new Button
            {
                Content = "Calibrate ready skill icons...",
                Padding = new Thickness(8),
                IsEnabled = calibrate is not null,
                Command = new Command(() => calibrate?.Invoke()),
            }
        );
        Label("Manual fallback (InputEstimate only)");
        var track = new CheckBox
        {
            Content = "Start timers from 1–4 / mouse buttons while Diablo III is focused",
            IsChecked = vm.Settings.TrackSkillInput,
            Foreground = Brushes.White,
        };
        track.Click += (_, _) => vm.Settings.TrackSkillInput = track.IsChecked == true;
        panel.Children.Add(track);
        Label(
            "Enter each skill's effective cooldown in seconds. 0 hides its timer. Input does not confirm a cast; resets and cooldown reductions require manual adjustment."
        );
        var skillRow = new UniformGrid { Rows = 1, Columns = 6 };
        foreach (var skill in vm.Settings.Skills.Take(6))
        {
            var stack = new StackPanel { Margin = new Thickness(4) };
            stack.Children.Add(new TextBlock { Text = skill.Key, Foreground = Brushes.White });
            var duration = new TextBox { DataContext = skill };
            duration.SetBinding(
                TextBox.TextProperty,
                new Binding(nameof(skill.DurationSeconds))
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
                    ValidatesOnExceptions = true,
                }
            );
            stack.Children.Add(duration);
            skillRow.Children.Add(stack);
        }
        panel.Children.Add(skillRow);
        panel.Children.Add(
            new Button
            {
                Content = "Clear skill countdowns",
                Command = new Command(vm.ResetCooldowns),
                Margin = new Thickness(0, 5, 0, 5),
            }
        );
        Label("Party portrait bars — visual layout only");
        Label(
            "Choose names and classes to lay out each element strip. These bars show the element order; they do not display live party buffs or predicted timing."
        );
        foreach (var member in vm.Settings.Party.Take(4))
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 3, 0, 3),
            };
            var name = new TextBox
            {
                Width = 190,
                DataContext = member,
                Margin = new Thickness(0, 0, 8, 0),
            };
            name.SetBinding(
                TextBox.TextProperty,
                new Binding(nameof(member.Name))
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                }
            );
            row.Children.Add(name);
            var hero = new ComboBox
            {
                Width = 180,
                ItemsSource = Enum.GetValues<HeroClass>(),
                SelectedItem = member.Class,
            };
            hero.SelectionChanged += (_, _) =>
            {
                if (hero.SelectedItem is HeroClass selected)
                    member.Class = selected;
            };
            row.Children.Add(hero);
            panel.Children.Add(row);
        }
        Label("Widgets • enabled / opacity / accent (#RRGGBB)");
        Label(
            "Nearby map (experimental) shows available scene geometry, including unexplored parts of loaded tiles. Cached areas have dashed outlines. Minimap mode follows your character; panel mode fits nearby geometry. This does not reveal a complete unexplored floor."
        );
        Label("Myriam - EU seasonal solo build guide");
        var myriam = new CheckBox
        {
            Content = "Automatically show gear and enchant references at Myriam",
            Margin = new Thickness(0, 5, 0, 5),
        };
        myriam.SetBinding(
            CheckBox.IsCheckedProperty,
            new Binding(nameof(Settings.MyriamGuideEnabled))
            {
                Source = vm.Settings,
                Mode = BindingMode.TwoWay,
            }
        );
        panel.Children.Add(myriam);
        var fallback = new CheckBox
        {
            Content = "Use highest-ranked accessible profile when rank one is unavailable",
            Margin = new Thickness(0, 5, 0, 5),
        };
        fallback.SetBinding(
            CheckBox.IsCheckedProperty,
            new Binding(nameof(Settings.MyriamAllowFallback))
            {
                Source = vm.Settings,
                Mode = BindingMode.TwoWay,
            }
        );
        panel.Children.Add(fallback);
        Label(
            "Detects your current hero class. Shows current equipped gear and primary-affix enchant references from Blizzard's EU seasonal normal solo-GR leaderboard. Actual rank and fetch time remain visible. Cached references refresh every 15 minutes; offline copies are labelled. All gear slots appear in an inventory-style layout without scrolling. English Enchant text is required."
        );
        Label("Kadala multi-buy");
        var kadala = new CheckBox
        {
            Content = "Enable Kadala multi-buy",
            Margin = new Thickness(0, 5, 0, 5),
        };
        kadala.SetBinding(
            CheckBox.IsCheckedProperty,
            new Binding(nameof(Settings.KadalaHoverBuy))
            {
                Source = vm.Settings,
                Mode = BindingMode.TwoWay,
            }
        );
        panel.Children.Add(kadala);
        Combo(
            "Kadala activation",
            new[] { "Hover for 3 seconds", "Single left-click" },
            vm.Settings.KadalaMode == KadalaActivationMode.SingleClick
                ? "Single left-click"
                : "Hover for 3 seconds",
            selected =>
                vm.Settings.KadalaMode =
                    selected.ToString() == "Single left-click"
                        ? KadalaActivationMode.SingleClick
                        : KadalaActivationMode.Hover
        );

        Label(
            "Requires English shop text and locked/click-through overlay (F11). Choose a three-second hover or one physical left-click on the offer to start. Keep the mouse on the offer while buying. Move off, switch windows, or press F10/F11/F12 to stop. Each purchase waits for an inventory item; buying stops when full, out of shards, or rejected. Kadala mode pauses skill auto-cast."
        );
        var kadalaStatus = new TextBlock { DataContext = vm, TextWrapping = TextWrapping.Wrap };
        kadalaStatus.SetBinding(TextBlock.TextProperty, new Binding(nameof(vm.KadalaStatus)));
        panel.Children.Add(kadalaStatus);
        void MarkerToggle(string title, string property)
        {
            var check = new CheckBox { Content = title, Margin = new Thickness(0, 5, 0, 5) };
            check.SetBinding(
                CheckBox.IsCheckedProperty,
                new Binding(property) { Source = vm.Settings, Mode = BindingMode.TwoWay }
            );
            panel.Children.Add(check);
        }
        MarkerToggle("Show elite minimap markers", nameof(Settings.ShowEliteMarkers));
        MarkerToggle("Show legendary and set loot markers", nameof(Settings.ShowLootMarkers));
        Label(
            "Elite colours: blue champions; yellow rare elites; amber minions; purple uniques; orange-red bosses. Loot: orange legendary stars, green set stars; Ancients are brighter with a halo. Primals use a red circled pentagram. Nearby loot symbols spread slightly with lines to their actual drop positions."
        );
        var mapStatus = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 5),
            DataContext = vm,
        };
        mapStatus.SetBinding(TextBlock.TextProperty, new Binding("RiftMap.Message"));
        panel.Children.Add(mapStatus);
        var alignMap = new CheckBox
        {
            Content = "Align nearby map with game minimap",
            IsChecked = vm.Settings.AlignMapToMinimap,
            Margin = new Thickness(0, 8, 0, 8),
        };
        alignMap.Checked += (_, _) => vm.Settings.AlignMapToMinimap = true;
        alignMap.Unchecked += (_, _) => vm.Settings.AlignMapToMinimap = false;
        panel.Children.Add(alignMap);
        void MapCalibration(string label, string property, double minimum, double maximum)
        {
            Label(label);
            var slider = new Slider
            {
                Minimum = minimum,
                Maximum = maximum,
                Width = 300,
                HorizontalAlignment = HorizontalAlignment.Left,
                ToolTip = label,
            };
            slider.SetBinding(
                Slider.ValueProperty,
                new Binding(property) { Source = vm.Settings, Mode = BindingMode.TwoWay }
            );
            panel.Children.Add(slider);
        }
        MapCalibration("Minimap scale", nameof(Settings.MinimapScale), .25, 3);
        MapCalibration("Minimap horizontal offset", nameof(Settings.MinimapOffsetX), -100, 100);
        MapCalibration("Minimap vertical offset", nameof(Settings.MinimapOffsetY), -100, 100);
        foreach (var widget in vm.Settings.Widgets)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 5, 0, 5),
            };
            var check = new CheckBox
            {
                Content = widget.Title,
                Width = 190,
                Foreground = Brushes.White,
                DataContext = widget,
            };
            check.SetBinding(
                CheckBox.IsCheckedProperty,
                new Binding(nameof(widget.Enabled)) { Mode = BindingMode.TwoWay }
            );
            row.Children.Add(check);
            var opacity = new Slider
            {
                Minimum = .15,
                Maximum = 1,
                Width = 130,
                DataContext = widget,
            };
            opacity.SetBinding(
                Slider.ValueProperty,
                new Binding(nameof(widget.Opacity)) { Mode = BindingMode.TwoWay }
            );
            row.Children.Add(opacity);
            var color = new TextBox { Width = 100, DataContext = widget };
            color.SetBinding(
                TextBox.TextProperty,
                new Binding(nameof(widget.Color))
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                }
            );
            row.Children.Add(color);
            panel.Children.Add(row);
        }
        Label("Build notes");
        var notes = new TextBox
        {
            Text = vm.Settings.Notes,
            AcceptsReturn = true,
            Height = 75,
            TextWrapping = TextWrapping.Wrap,
        };
        notes.TextChanged += (_, _) => vm.Settings.Notes = notes.Text;
        panel.Children.Add(notes);
        Label(
            "Counters remain manual. Skill timers are estimates from your configured durations. Party bars are visual only. OCR recognizes visible English text."
        );
        var status = new TextBlock
        {
            DataContext = vm,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 8),
        };
        status.SetBinding(TextBlock.TextProperty, new Binding(nameof(vm.Status)));
        panel.Children.Add(status);
        var save = new Button
        {
            Content = "Apply and save",
            Margin = new Thickness(0, 16, 0, 4),
            Padding = new Thickness(10),
            Command = new Command(() =>
            {
                if (
                    vm.Settings.Skills.Any(s =>
                        !double.IsFinite(s.DurationSeconds)
                        || s.DurationSeconds < 0
                        || s.DurationSeconds > 3600
                    )
                )
                {
                    MessageBox.Show("Enter skill cooldowns between 0 and 3600 seconds.");
                    return;
                }
                foreach (var w in vm.Settings.Widgets)
                {
                    try
                    {
                        if (ColorConverter.ConvertFromString(w.Color) is not Color)
                            throw new FormatException("A colour is required.");
                    }
                    catch
                    {
                        MessageBox.Show("Enter a valid colour, for example #E6AD55.");
                        return;
                    }
                }
                apply();
                Close();
            }),
        };
        panel.Children.Add(save);
        panel.Children.Add(
            new Button
            {
                Content = "Exit D3Vision",
                Padding = new Thickness(8),
                Command = new Command(quit),
            }
        );
    }
}
