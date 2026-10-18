using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using D3Vision.Core;
using D3Vision.Infrastructure;
using D3Vision.Rendering;

namespace D3Vision.UI;

public sealed class OverlayWindow : Window
{
    readonly MainViewModel vm;
    readonly Canvas canvas = new();
    readonly TextBlock skillSetupLabel = InventoryLabel();
    readonly TextBlock inventoryStatusLabel = InventoryLabel();
    readonly TextBlock dropLabel = InventoryLabel();
    readonly TextBlock salvageAllLabel = InventoryLabel();
    readonly TextBlock salvageNormalLabel = InventoryLabel();
    readonly InventoryHoverTrigger inventoryHover = new();
    public event Action<InventoryAction>? InventoryActionRequested;

    static TextBlock InventoryLabel() =>
        new()
        {
            FontSize = 15,
            FontWeight = FontWeights.Black,
            Foreground = Brushes.Black,
            Background = new SolidColorBrush(Color.FromArgb(210, 224, 197, 145)),
            Padding = new Thickness(6, 3, 6, 3),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };

    public void ResetInventoryHover() => inventoryHover.Reset();

    readonly MyriamGuideView myriamGuide = new()
    {
        Visibility = Visibility.Collapsed,
        IsHitTestVisible = false,
        Focusable = false,
    };
    readonly Dictionary<WidgetState, WidgetVisual> visuals = [];
    readonly List<WidgetVisual> skillAnchors = [];
    HwndSource? source;
    KeyboardHotkeys? hotkeys;
    long nextTopmostCheck;
    public event Action<int>? Hotkey;
    public event Action<int>? SkillInput;
    public event Action<int, int>? PhysicalLeftClick;

    public OverlayWindow(MainViewModel model)
    {
        Title = "D3Vision Overlay";
        vm = model;
        DataContext = vm;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        Content = canvas;
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            source = HwndSource.FromHwnd(hwnd);
            source.AddHook(Hook);
            Native.SetWindowDisplayAffinity(hwnd, 0x11);
            hotkeys = new KeyboardHotkeys(
                id => Dispatcher.BeginInvoke(() => Hotkey?.Invoke(id)),
                key => Dispatcher.BeginInvoke(() => SkillInput?.Invoke(key)),
                (x, y) => Dispatcher.BeginInvoke(() => PhysicalLeftClick?.Invoke(x, y))
            );
            ApplyMonitor();
            SetClickThrough();
        };
        Closed += (_, _) =>
        {
            hotkeys?.Dispose();
            source?.RemoveHook(Hook);
        };
    }

    nint Hook(nint h, int msg, nint w, nint l, ref bool handled)
    {
        if (msg == 0x7e)
            Dispatcher.BeginInvoke(ApplyMonitor);
        return 0;
    }

    public void ApplyMonitor()
    {
        var monitors = Native.Monitors();
        if (monitors.Count == 0)
            return;
        var r = monitors[Math.Clamp(vm.Settings.Monitor, 0, monitors.Count - 1)];
        var hwnd = new WindowInteropHelper(this).Handle;
        Native.SetWindowPos(
            hwnd,
            new nint(-1),
            r.Left,
            r.Top,
            r.Right - r.Left,
            r.Bottom - r.Top,
            0x10
        );
        double scale = Math.Max(96, Native.GetDpiForWindow(hwnd)) / 96d;
        Width = (r.Right - r.Left) / scale;
        Height = (r.Bottom - r.Top) / scale;
        Native.SetWindowPos(
            hwnd,
            new nint(-1),
            r.Left,
            r.Top,
            r.Right - r.Left,
            r.Bottom - r.Top,
            0x10
        );
        Rebuild();
    }

    public void SetClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        long style = (long)Native.GetWindowLongPtr(hwnd, -20);
        style |= 0x80000 | 0x80 | 0x8000000;
        style = vm.Settings.ClickThrough ? style | 0x20 : style & ~0x20;
        Native.SetWindowLongPtr(hwnd, -20, (nint)style);
        Rebuild();
    }

    /// <summary>Recover Z order after the game restores or reasserts its own topmost window.</summary>
    public void MaintainTopmost()
    {
        if (!IsVisible || Environment.TickCount64 < nextTopmostCheck)
            return;
        nextTopmostCheck = Environment.TickCount64 + 1000;
        if (!GameInput.IsDiabloForeground())
            return;
        // HWND_TOPMOST, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE.
        // Never activate the overlay or take keyboard focus from the game.
        Native.SetWindowPos(new WindowInteropHelper(this).Handle, new nint(-1), 0, 0, 0, 0, 0x13);
    }

    public void Rebuild()
    {
        canvas.Children.Clear();
        visuals.Clear();
        skillAnchors.Clear();
        bool editing = !vm.Settings.ClickThrough;
        foreach (var state in vm.Settings.Widgets.Where(w => w.Enabled))
        {
            var grid = new Grid { DataContext = state };
            grid.SetBinding(WidthProperty, new Binding(nameof(state.Width)));
            grid.SetBinding(HeightProperty, new Binding(nameof(state.Height)));
            grid.SetBinding(OpacityProperty, new Binding(nameof(state.Opacity)));
            void Place()
            {
                var point = HudLayout.Resolve(state, Width, Height);
                Canvas.SetLeft(grid, point.X);
                Canvas.SetTop(grid, point.Y);
            }
            Place();
            var visual = new WidgetVisual { Kind = state.Kind, EditMode = editing };
            visuals[state] = visual;
            grid.Children.Add(visual);
            if (editing && !(state.Kind == WidgetKind.RiftMap && vm.Settings.AlignMapToMinimap))
            {
                var drag = new Thumb
                {
                    Height = Math.Min(10, state.Height / 3),
                    VerticalAlignment = VerticalAlignment.Top,
                    Cursor = System.Windows.Input.Cursors.SizeAll,
                    Opacity = 0,
                    ToolTip = $"Drag {state.Title}",
                };
                drag.DragDelta += (_, e) =>
                {
                    var p = HudLayout.Resolve(state, Width, Height);
                    state.X +=
                        Snap(
                            Math.Clamp(
                                p.X + e.HorizontalChange,
                                0,
                                Math.Max(0, Width - state.Width)
                            ),
                            Width - state.Width
                        ) - p.X;
                    state.Y +=
                        Snap(
                            Math.Clamp(
                                p.Y + e.VerticalChange,
                                0,
                                Math.Max(0, Height - state.Height)
                            ),
                            Height - state.Height
                        ) - p.Y;
                    Place();
                };
                grid.Children.Add(drag);
                var resize = new Thumb
                {
                    Width = 8,
                    Height = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Cursor = System.Windows.Input.Cursors.SizeNWSE,
                    Opacity = .5,
                };
                resize.DragDelta += (_, e) =>
                {
                    state.Width = Math.Clamp(
                        state.Width + e.HorizontalChange,
                        40,
                        Math.Max(40, Math.Min(1200, Width))
                    );
                    state.Height = Math.Clamp(
                        state.Height + e.VerticalChange,
                        18,
                        Math.Max(18, Math.Min(900, Height))
                    );
                    Place();
                };
                grid.Children.Add(resize);
                if (state.Kind is WidgetKind.Deaths or WidgetKind.Elites or WidgetKind.Goblins)
                {
                    var add = new Button
                    {
                        Content = "+",
                        Width = 16,
                        Height = 16,
                        FontSize = 10,
                        Padding = new Thickness(0),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Command = new Command(() =>
                        {
                            vm.Increment(state.Kind);
                            Refresh();
                        }),
                    };
                    grid.Children.Add(add);
                }
                if (state.Kind == WidgetKind.Cooldowns)
                {
                    // Transparent hit targets let an unlocked skill badge start its configured timer.
                    var slots = new UniformGrid
                    {
                        Rows = 1,
                        Columns = SkillSlots.Count,
                        Margin = new Thickness(0, 10, 0, 8),
                    };
                    for (int i = 0; i < SkillSlots.Count; i++)
                    {
                        int slot = i;
                        var hit = new Border
                        {
                            Background = Brushes.Transparent,
                            ToolTip = $"Start {vm.Settings.Skills[i].Key} cooldown estimate",
                        };
                        hit.MouseLeftButtonDown += (_, e) =>
                        {
                            vm.StartSkill(slot);
                            Refresh();
                            e.Handled = true;
                        };
                        slots.Children.Add(hit);
                    }
                    grid.Children.Add(slots);
                }
            }
            canvas.Children.Add(grid);
        }
        for (int i = 0; i < SkillSlots.Count; i++)
        {
            var anchor = new WidgetVisual
            {
                Kind = WidgetKind.Cooldowns,
                SingleSkillSlot = i,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
            };
            skillAnchors.Add(anchor);
            canvas.Children.Add(anchor);
        }
        canvas.Children.Add(skillSetupLabel);
        canvas.Children.Add(myriamGuide);
        canvas.Children.Add(inventoryStatusLabel);
        canvas.Children.Add(dropLabel);
        canvas.Children.Add(salvageAllLabel);
        canvas.Children.Add(salvageNormalLabel);
        Refresh();
    }

    static double Snap(double value, double edge) =>
        value < 12 ? 0
        : Math.Abs(value - edge) < 12 ? edge
        : value;

    public int? HoveredSkill(Native.Point cursor)
    {
        if (!vm.Settings.Widgets.Any(w => w.Kind == WidgetKind.Cooldowns && w.Enabled))
            return null;
        // Hit-test the actual rendered anchors. Their local geometry already includes
        // monitor DPI and capture transforms, so the hover target cannot drift off its dot.
        int? hit = null;
        for (int slot = 0; slot < skillAnchors.Count; slot++)
        {
            var visual = skillAnchors[slot];
            if (
                visual.Visibility != Visibility.Visible
                || visual.ActualWidth <= 0
                || visual.ActualHeight <= 0
            )
                continue;
            var point = visual.PointFromScreen(new Point(cursor.X, cursor.Y));
            if (
                !SkillHitTesting.ContainsDot(
                    visual.ActualWidth,
                    visual.ActualHeight,
                    point.X,
                    point.Y
                )
            )
                continue;
            if (hit is not null)
                return null;
            hit = slot;
        }
        return hit;
    }

    public void Refresh()
    {
        UpdateInventoryControls();
        if (
            vm.Settings.MyriamGuideEnabled
            && vm.Settings.ClickThrough
            && !vm.MyriamGuideHidden
            && vm.MyriamDetected
            && GameInput.TryGetGameClientBounds(out var guideBounds)
        )
        {
            myriamGuide.Visibility = Visibility.Visible;
            var a = PointFromScreen(new Point(guideBounds.Left, guideBounds.Top));
            var b = PointFromScreen(new Point(guideBounds.Right, guideBounds.Bottom));
            myriamGuide.Width = Math.Clamp((b.X - a.X) * .30, 240, 600);
            myriamGuide.Height = Math.Max(160, (b.Y - a.Y) * .65);
            Canvas.SetLeft(myriamGuide, (a.X + b.X - myriamGuide.Width) / 2);
            Canvas.SetTop(myriamGuide, a.Y + (b.Y - a.Y) * .05);
            myriamGuide.Update(vm.MyriamBuild, vm.MyriamStatus);
        }
        else
            myriamGuide.Visibility = Visibility.Collapsed;

        var skills = vm.CooldownSnapshot();
        var autoCast = vm.AutoCast.EnabledSnapshot();
        UpdateSkillAnchors(skills, autoCast);
        foreach (var (state, visual) in visuals)
        {
            if (state.Kind == WidgetKind.Cooldowns)
                ((Grid)visual.Parent).Visibility =
                    vm.Settings.ClickThrough && vm.Settings.CooldownMode == CooldownMode.VisualRead
                        ? Visibility.Collapsed
                        : Visibility.Visible;
            string previousCaption = visual.Caption;
            string previousValue = visual.Value;
            bool mapChanged =
                state.Kind == WidgetKind.RiftMap && !ReferenceEquals(visual.MapDrawing, vm.RiftMap);
            visual.MapDrawing = vm.RiftMap;
            if (state.Kind == WidgetKind.RiftMap && vm.Settings.AlignMapToMinimap)
            {
                visual.AlignMap = true;
                visual.MapScale = vm.Settings.MinimapScale;
                visual.MapOffsetX = vm.Settings.MinimapOffsetX;
                visual.MapOffsetY = vm.Settings.MinimapOffsetY;
                var grid = (Grid)visual.Parent;
                bool visible =
                    vm.RiftMap.Player is not null && GameInput.TryGetGameClientBounds(out _);
                grid.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                if (visible && GameInput.TryGetGameClientBounds(out var bounds))
                {
                    var viewport = MinimapProjection.Viewport(
                        bounds.Right - bounds.Left,
                        bounds.Bottom - bounds.Top
                    );
                    var a = PointFromScreen(
                        new Point(bounds.Left + viewport.Left, bounds.Top + viewport.Top)
                    );
                    var b = PointFromScreen(
                        new Point(
                            bounds.Left + viewport.Left + viewport.Size,
                            bounds.Top + viewport.Top + viewport.Size
                        )
                    );
                    grid.Width = b.X - a.X;
                    grid.Height = b.Y - a.Y;
                    Canvas.SetLeft(grid, a.X);
                    Canvas.SetTop(grid, a.Y);
                }
            }
            string previousFont = visual.FontName;
            Color previousAccent = visual.Accent;
            bool previousTheme = visual.SlateTheme;
            visual.SlateTheme = vm.Settings.Theme == "Slate";
            string previousName = visual.PartyName;
            HeroClass previousClass = visual.PartyClass;
            bool skillChanged =
                state.Kind == WidgetKind.Cooldowns
                && (
                    !visual.Skills.SequenceEqual(skills)
                    || !visual.AutoCastSkills.SequenceEqual(autoCast)
                    || visual.HoveredSkill != vm.AutoCast.HoverSlot
                    || visual.HoverProgress != vm.AutoCast.HoverFraction
                );
            visual.AutoCastSkills = autoCast;
            visual.HoveredSkill = vm.AutoCast.HoverSlot;
            visual.HoverProgress = vm.AutoCast.HoverFraction;
            visual.Caption = state.Kind switch
            {
                WidgetKind.Experience => "XP/H",
                WidgetKind.GreaterRiftTimer => "GR",
                _ => state.Title,
            };
            visual.Value = vm.Value(state.Kind);
            visual.FontName = vm.Settings.Font;
            try
            {
                visual.Accent = ColorConverter.ConvertFromString(state.Color) is Color color
                    ? color
                    : Colors.Goldenrod;
            }
            catch (Exception ex)
                when (ex is FormatException or NotSupportedException or ArgumentException)
            {
                visual.Accent = Colors.Goldenrod;
            }
            visual.Skills = skills;
            if (HudLayout.IsParty(state.Kind))
            {
                var member = vm.Settings.Party[(int)state.Kind - (int)WidgetKind.Party1];
                visual.PartyName = member.Name;
                visual.PartyClass = member.Class;
                var buffs =
                    vm.Settings.PortraitBuffsEnabled
                    && vm.CombatBuffs.IsFresh(Environment.TickCount64)
                    && GameInput.IsDiabloForeground()
                        ? vm.CombatBuffs.Portrait(
                            (int)state.Kind - (int)WidgetKind.Party1,
                            member.GamePlayerSlot
                        )
                        : null;
                visual.PlayerBuffs = buffs;
                visual.StrickenProcs = buffs is null ? null : vm.Stricken.Count(buffs.PlayerSlot);
                visual.ConventionDisplay = vm.Settings.ConventionDisplay;
                visual.InvalidateVisual();
            }
            if (
                previousCaption != visual.Caption
                || previousValue != visual.Value
                || previousFont != visual.FontName
                || previousAccent != visual.Accent
                || previousTheme != visual.SlateTheme
                || previousName != visual.PartyName
                || previousClass != visual.PartyClass
                || skillChanged
                || mapChanged
            )
                visual.InvalidateVisual();
        }
    }

    void UpdateInventoryControls()
    {
        inventoryStatusLabel.Visibility = Visibility.Collapsed;
        dropLabel.Visibility =
            salvageAllLabel.Visibility =
            salvageNormalLabel.Visibility =
                Visibility.Collapsed;
        if (
            !IsVisible
            || !vm.Settings.ClickThrough
            || !GameInput.TryGetGameClientBounds(out var bounds)
            || !GameInput.TryGetCursor(out var cursor)
        )
        {
            inventoryHover.Reset();
            return;
        }
        double scale = (bounds.Bottom - bounds.Top) / 1080d;
        if (
            (vm.Settings.AutoSalvageEnabled || vm.Settings.DropAllEnabled)
            && (vm.InventoryBusy || Environment.TickCount64 - vm.InventoryStatusSeen < 12000)
        )
        {
            var statusPosition = PointFromScreen(
                new Point(
                    bounds.Left + (bounds.Right - bounds.Left) / 2d - 320 * scale,
                    bounds.Bottom - 230 * scale
                )
            );
            Canvas.SetLeft(inventoryStatusLabel, statusPosition.X);
            Canvas.SetTop(inventoryStatusLabel, statusPosition.Y);
            inventoryStatusLabel.Width = 360;
            inventoryStatusLabel.Height = double.NaN;
            inventoryStatusLabel.FontSize = 12;
            inventoryStatusLabel.TextWrapping = TextWrapping.Wrap;
            inventoryStatusLabel.Text = vm.InventoryStatus;
            inventoryStatusLabel.Visibility = Visibility.Visible;
        }
        bool salvage =
            vm.Settings.AutoSalvageEnabled && Environment.TickCount64 - vm.BlacksmithSeen < 2500;
        InventoryAction? hovered = null;
        void Place(
            TextBlock label,
            string caption,
            double x,
            double y,
            double width,
            InventoryAction action
        )
        {
            var a = PointFromScreen(new Point(x, y));
            var b = PointFromScreen(new Point(x + width * scale, y + 30 * scale));
            label.Width = b.X - a.X;
            label.Height = b.Y - a.Y;
            label.FontSize =
                15
                * scale
                / (
                    PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11
                    ?? 1
                );
            Canvas.SetLeft(label, a.X);
            Canvas.SetTop(label, a.Y);
            label.Visibility = Visibility.Visible;
            if (
                cursor.X >= x
                && cursor.X < x + width * scale
                && cursor.Y >= y
                && cursor.Y < y + 30 * scale
            )
                hovered = action;
            label.Text = vm.InventoryBusy ? "STOP: ESC" : caption;
        }
        if (vm.Settings.DropAllEnabled)
            Place(
                dropLabel,
                "DROP",
                bounds.Left + (bounds.Right - bounds.Left) / 2d - 480 * scale,
                bounds.Bottom - 142 * scale,
                120,
                InventoryAction.DropAll
            );
        if (salvage)
        {
            Place(
                salvageAllLabel,
                "SALVAGE ALL",
                bounds.Left + 50 * scale,
                bounds.Top + 570 * scale,
                360,
                InventoryAction.SalvageAll
            );
            Place(
                salvageNormalLabel,
                "SALVAGE: KEEP ANCIENT / PRIMAL",
                bounds.Left + 50 * scale,
                bounds.Top + 610 * scale,
                360,
                InventoryAction.SalvageNormal
            );
        }
        // Keep a completed dwell latched while the operation runs. An empty plan
        // must not repeat continuously if the pointer stays over its label.
        if (vm.InventoryBusy)
            return;
        var request = inventoryHover.Update(hovered);
        if (inventoryHover.Progress > 0 && hovered is { } action)
        {
            var label =
                action == InventoryAction.DropAll ? dropLabel
                : action == InventoryAction.SalvageAll ? salvageAllLabel
                : salvageNormalLabel;
            label.Text += $" {3 * (1 - inventoryHover.Progress):0.0}s";
        }
        if (request is { } selected)
            InventoryActionRequested?.Invoke(selected);
    }

    void UpdateSkillAnchors(IReadOnlyList<SkillCountdown> skills, IReadOnlyList<bool> automatic)
    {
        var widget = vm.Settings.Widgets.FirstOrDefault(w =>
            w.Kind == WidgetKind.Cooldowns && w.Enabled
        );
        var area = vm.CaptureArea;
        bool show =
            widget is not null
            && vm.Settings.ClickThrough
            && vm.Settings.CooldownMode == CooldownMode.VisualRead
            && area is not null
            && Environment.TickCount64 - vm.CaptureAreaSeen <= 500
            && area.WholeScreen == vm.Settings.WholeScreen
            && (!area.WholeScreen || area.Monitor == vm.Settings.Monitor)
            && GameInput.IsDiabloForeground();
        skillSetupLabel.Visibility = Visibility.Collapsed;
        int calibrated = vm
            .Settings.Skills.Take(SkillSlots.Count)
            .Count(skill => skill.Calibration is not null);
        int visibleDots = 0;
        for (int i = 0; i < skillAnchors.Count; i++)
        {
            var visual = skillAnchors[i];
            visual.Visibility = Visibility.Collapsed;
            if (
                !show
                || area is null
                || i >= vm.Settings.Skills.Count
                || vm.Settings.Skills[i].Calibration is not { } c
                || SkillHitTesting.Hit(
                    vm.Settings.Skills,
                    c.X + c.Width / 2,
                    c.Y + c.Height / 2,
                    (double)area.Width / area.Height,
                    area.WholeScreen,
                    area.Monitor
                ) != i
            )
                continue;
            var a = PointFromScreen(
                new Point(area.Left + c.X * area.Width, area.Top + c.Y * area.Height)
            );
            var b = PointFromScreen(
                new Point(
                    area.Left + (c.X + c.Width) * area.Width,
                    area.Top + (c.Y + c.Height) * area.Height
                )
            );
            visual.Width = Math.Max(1, b.X - a.X);
            visual.Height = Math.Max(1, b.Y - a.Y);
            Canvas.SetLeft(visual, a.X);
            Canvas.SetTop(visual, a.Y);
            visual.Skills = skills;
            visual.AutoCastSkills = automatic;
            visual.HoveredSkill = vm.AutoCast.HoverSlot;
            visual.HoverProgress = vm.AutoCast.HoverFraction;
            visual.FontName = vm.Settings.Font;
            visual.Opacity = widget!.Opacity;
            visual.Visibility = Visibility.Visible;
            visibleDots++;
            visual.InvalidateVisual();
        }
        vm.AutoCastStatus =
            vm.AutoCastBlockReason is { } reason ? "Auto-cast paused: " + reason
            : calibrated == 0
                ? "Auto-cast setup: 0/7 icons saved. F12 > Calibrate ready skill icons. Each selection now saves automatically."
            : !vm.Settings.ClickThrough ? "Auto-cast paused: press F11 to lock the overlay."
            : vm.Settings.CooldownMode != CooldownMode.VisualRead
                ? "Auto-cast requires VisualRead mode in Settings."
            : widget is null ? "Auto-cast requires the Cooldowns widget to be enabled."
            : area is null || Environment.TickCount64 - vm.CaptureAreaSeen > 500
                ? "Auto-cast: waiting for a fresh capture with screen coordinates."
            : !GameInput.IsDiabloForeground()
                ? $"Auto-cast: {calibrated}/7 icons saved. Return to Diablo III to see the dots."
            : visibleDots == 0
                ? "Auto-cast: saved icons do not match this capture mode, monitor or aspect ratio. Recalibrate for this capture."
            : $"Auto-cast: {visibleDots}/7 dots ready. Hover a red dot for 3 seconds, then move away. Check your skill key bindings in Settings.";
        if (vm.AutoCastBlockReason is null && vm.AutoCast.HoverSlot is { } hovered)
        {
            string key = vm.Settings.Skills[hovered].Key;
            vm.AutoCastStatus =
                vm.AutoCast.HoverFraction > 0
                    ? $"Auto-cast {key}: hover {3 * (1 - vm.AutoCast.HoverFraction):0.0}s more to toggle."
                    : $"Auto-cast {key}: {(vm.AutoCast.IsEnabled(hovered) ? "enabled" : "inactive")}. Move away before hovering again.";
        }
        var armed = vm
            .AutoCast.EnabledSnapshot()
            .Select((enabled, slot) => (enabled, slot))
            .Where(pair => pair.enabled)
            .Select(pair => pair.slot)
            .ToArray();
        if (vm.AutoCastBlockReason is null && vm.AutoCast.HoverSlot is null && armed.Length > 0)
        {
            vm.AutoCastStatus =
                "Auto-cast: "
                + string.Join(
                    "; ",
                    armed.Select(slot =>
                        $"{vm.Settings.Skills[slot].Key} {(skills[slot].IsUnknown ? "waiting for readable icon" : skills[slot].Fraction > 0 ? "cooling" : "ready")}"
                    )
                );
        }
        System.Windows.Automation.AutomationProperties.SetHelpText(
            this,
            vm.AutoCastStatus + " " + vm.AutoCastLastInput
        );
        if (
            (visibleDots == 0 || vm.AutoCastBlockReason is not null || armed.Length > 0)
            && GameInput.TryGetGameClientBounds(out var bounds)
        )
        {
            var position = PointFromScreen(
                new Point(
                    bounds.Left + (bounds.Right - bounds.Left) * .5,
                    bounds.Bottom - (bounds.Bottom - bounds.Top) * .19
                )
            );
            skillSetupLabel.Width = 580;
            skillSetupLabel.TextWrapping = TextWrapping.Wrap;
            skillSetupLabel.FontSize = 12;
            skillSetupLabel.Text =
                vm.AutoCastStatus + (armed.Length > 0 ? " " + vm.AutoCastLastInput : "");
            Canvas.SetLeft(skillSetupLabel, position.X - 290);
            Canvas.SetTop(skillSetupLabel, position.Y);
            skillSetupLabel.Visibility = Visibility.Visible;
        }
    }
}
