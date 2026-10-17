using System.Windows.Input;
using D3Vision.Core;

namespace D3Vision.UI;

public sealed class Command(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}

public sealed class MainViewModel(Settings settings, SessionStats stats) : Observable
{
    public Settings Settings { get; } = settings;
    public SessionStats Stats { get; } = stats;
    private string status = "Starting capture...";
    public string Status
    {
        get => status;
        set => Set(ref status, value);
    }
    private string kadalaStatus = "Kadala hover buying is disabled.";
    public string KadalaStatus
    {
        get => kadalaStatus;
        set => Set(ref kadalaStatus, value);
    }
    public bool MyriamDetected { get; set; }
    public LeaderboardBuild? MyriamBuild { get; set; }
    public string MyriamStatus { get; set; } = "Waiting for Myriam Enchant.";
    public List<double> XpSamples { get; } = [];
    public SkillTimers Cooldowns { get; } = new();
    public VisualCooldownTimers VisualCooldowns { get; } = new();
    public AutoCastController AutoCast { get; } = new();
    public CaptureArea? CaptureArea { get; set; }
    public long CaptureAreaSeen { get; set; }
    public CombatBuffSnapshot CombatBuffs { get; set; } = CombatBuffSnapshot.Waiting;
    public StrickenProcTracker Stricken { get; } = new();
    string buffStatus = "Waiting for live buffs";
    public string BuffStatus
    {
        get => buffStatus;
        set => Set(ref buffStatus, value);
    }
    private RiftMapDrawing riftMap = RiftMapDrawing.Waiting;
    public RiftMapDrawing RiftMap
    {
        get => riftMap;
        set => Set(ref riftMap, value);
    }

    public IReadOnlyList<SkillCountdown> CooldownSnapshot() =>
        Settings.CooldownMode == CooldownMode.VisualRead
            ? VisualCooldowns.Snapshot(Settings.Skills)
            : Cooldowns.Snapshot(Settings.Skills);

    public void ResetCooldowns()
    {
        Cooldowns.Reset();
        VisualCooldowns.Reset();
    }

    public string Value(WidgetKind kind) =>
        kind switch
        {
            WidgetKind.RiftProgress => $"{Stats.Progress:0.0}%",
            WidgetKind.GreaterRiftTimer => Stats.GameTimer,
            WidgetKind.Deaths => Stats.Deaths.ToString(),
            WidgetKind.Elites => Stats.Elites.ToString(),
            WidgetKind.Goblins => Stats.Goblins.ToString(),
            WidgetKind.Experience => CompactNumber(XpRate),
            _ => Settings.Notes,
        };

    static string CompactNumber(double value) =>
        value switch
        {
            >= 1_000_000_000 => $"{value / 1_000_000_000:0.0}B",
            >= 1_000_000 => $"{value / 1_000_000:0.0}M",
            >= 1_000 => $"{value / 1_000:0.0}K",
            _ => $"{value:0}",
        };

    public double XpRate =>
        Stats.Experience / Math.Max(.001, (DateTimeOffset.UtcNow - Stats.Started).TotalHours);

    public void StartSkill(int slot)
    {
        if (Settings.CooldownMode == CooldownMode.VisualRead)
            return;
        if (slot >= 0 && slot < Settings.Skills.Count)
            Cooldowns.Start(slot, Settings.Skills[slot]);
    }

    public void Increment(WidgetKind kind)
    {
        switch (kind)
        {
            case WidgetKind.Deaths:
                Stats.Deaths++;
                break;
            case WidgetKind.Elites:
                Stats.Elites++;
                break;
            case WidgetKind.Goblins:
                Stats.Goblins++;
                break;
        }
    }
}
