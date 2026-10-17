using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace D3Vision.Core;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new(name));
        return true;
    }

    public void Refresh() => PropertyChanged?.Invoke(this, new(null));
}

public enum WidgetKind
{
    RiftProgress,
    GreaterRiftTimer,
    Cooldowns,
    Deaths,
    Experience,
    BuildNotes,
    Elites,
    Goblins,
    Party1,
    Party2,
    Party3,
    Party4,
    RiftMap,
}

public sealed class WidgetState : Observable
{
    double x = 24,
        y = 24,
        width = 260,
        height = 130,
        opacity = .85;
    bool enabled = true;
    string color = "#E6AD55";
    public WidgetKind Kind { get; set; }
    public HorizontalAnchor HorizontalAnchor { get; set; }
    public VerticalAnchor VerticalAnchor { get; set; }
    public double X
    {
        get => x;
        set => Set(ref x, value);
    }
    public double Y
    {
        get => y;
        set => Set(ref y, value);
    }
    public double Width
    {
        get => width;
        set => Set(ref width, Math.Clamp(value, 40, 1200));
    }
    public double Height
    {
        get => height;
        set => Set(ref height, Math.Clamp(value, 18, 900));
    }
    public double Opacity
    {
        get => opacity;
        set => Set(ref opacity, Math.Clamp(value, .15, 1));
    }
    public bool Enabled
    {
        get => enabled;
        set => Set(ref enabled, value);
    }
    public string Color
    {
        get => color;
        set => Set(ref color, value);
    }
    public string Title =>
        Kind switch
        {
            WidgetKind.RiftProgress => "RIFT PROGRESS",
            WidgetKind.GreaterRiftTimer => "GREATER RIFT",
            WidgetKind.Cooldowns => "COOLDOWNS",
            WidgetKind.Deaths => "DEATHS",
            WidgetKind.Experience => "XP / HOUR",
            WidgetKind.BuildNotes => "BUILD NOTES",
            WidgetKind.Elites => "ELITE PACKS",
            WidgetKind.Goblins => "GOBLINS",
            WidgetKind.RiftMap => "NEARBY MAP (EXPERIMENTAL)",
            _ => $"PARTY {(int)Kind - (int)WidgetKind.Party1 + 1} ELEMENTS",
        };
}

public sealed class Settings
{
    public CooldownMode CooldownMode { get; set; } = CooldownMode.VisualRead;
    public int HudLayoutVersion { get; set; }
    public bool TrackSkillInput { get; set; } = true;
    public List<SkillSettings> Skills { get; set; } = SkillSettings.Defaults();
    public List<PartyMemberSettings> Party { get; set; } =
        Enumerable
            .Range(1, 4)
            .Select(i => new PartyMemberSettings { Name = $"Player {i}" })
            .ToList();
    public string Theme { get; set; } = "Obsidian";
    public string Font { get; set; } = "Segoe UI";
    public int Monitor { get; set; }
    public bool WholeScreen { get; set; }
    public bool ClickThrough { get; set; } = true;
    public bool MyriamGuideEnabled { get; set; } = true;
    public bool MyriamAllowFallback { get; set; } = true;
    public KadalaActivationMode KadalaMode { get; set; } = KadalaActivationMode.Hover;
    public bool KadalaHoverBuy { get; set; }
    public bool ShowEliteMarkers { get; set; } = true;
    public bool ShowLootMarkers { get; set; } = true;
    public bool AlignMapToMinimap { get; set; } = true;
    public double MinimapScale { get; set; } = 1;
    public double MinimapOffsetX { get; set; }
    public double MinimapOffsetY { get; set; }
    public int OcrIntervalMs { get; set; } = 1000;
    public string Notes { get; set; } = "F11 to edit • F12 settings";
    public List<WidgetState> Widgets { get; set; } =
        Enum.GetValues<WidgetKind>()
            .Select(
                (k, i) =>
                    new WidgetState
                    {
                        Kind = k,
                        Enabled = k != WidgetKind.RiftMap,
                        X = 24 + (i % 3) * 280,
                        Y = 24 + (i / 3) * 150,
                    }
            )
            .ToList();
}

public sealed class SessionStats
{
    public DateTimeOffset Started { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? Ended { get; set; }
    public int Deaths { get; set; }
    public int Elites { get; set; }
    public int Goblins { get; set; }
    public long Experience { get; set; }
    public double Progress { get; set; }
    public string GameTimer { get; set; } = "--:--";
    public int Rifts { get; set; }
    public List<string> ItemDrops { get; set; } = [];
}

public sealed record CaptureFrame(byte[] Bgra, int Width, int Height);

public interface IScreenCapture : IDisposable
{
    // The returned buffer is owned by the capture service and valid until its next call.
    CaptureFrame? Capture(bool wholeScreen, int monitor);
}

public interface IOcrService
{
    Task<string> RecognizeAsync(CaptureFrame frame, CancellationToken cancellationToken);
}

public interface IJsonStore
{
    Task<T> LoadAsync<T>(string name, Func<T> fallback);
    Task SaveAsync<T>(string name, T value);
}
