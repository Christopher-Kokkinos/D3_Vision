using System.IO;
using D3Vision.Core;
using D3Vision.Infrastructure;
using D3Vision.Services;

var parser = new TrackingService(new NoCapture(), new NoOcr());
var stats = new SessionStats();
parser.Apply("Rift complete!\n12:34\n1,234 XP\n87.5%\nLegendary sword", stats);
Check(
    stats.Rifts == 1
        && stats.GameTimer == "12:34"
        && stats.Experience == 1234
        && stats.Progress == 87.5
        && stats.ItemDrops.Count == 1,
    "OCR parsing"
);
parser.Apply("Rift complete!\n12:34\n1,234 XP\n87.5%\nLegendary sword", stats);
Check(stats.Rifts == 1 && stats.Experience == 1234, "Persistent text must not be counted twice");
parser.Apply("", stats);
parser.Apply("2,000 experience", stats);
Check(stats.Experience == 3234, "New XP messages");
var widget = new WidgetState
{
    Width = -10,
    Height = 10000,
    Opacity = 2,
};
Check(widget.Width == 40 && widget.Height == 900 && widget.Opacity == 1, "Configuration bounds");
var store = new JsonStore();
string key = "test-" + Guid.NewGuid().ToString("N");
try
{
    await store.SaveAsync(key, stats);
    var restored = await store.LoadAsync(key, () => new SessionStats());
    Check(restored.Experience == 3234, "JSON persistence round trip");
}
finally
{
    File.Delete(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "D3Vision",
            key + ".json"
        )
    );
}
var scaled = new FrameScaler().Fit(new CaptureFrame(new byte[192 * 108 * 4], 192, 108), 64);
Check(
    scaled.Width == 64 && scaled.Height == 36 && scaled.Bgra.Length == 64 * 36 * 4,
    "OCR downscaling preserves aspect ratio"
);
var hudSettings = new Settings();
HudLayout.Upgrade(hudSettings);
Check(
    hudSettings.HudLayoutVersion == HudLayout.CurrentVersion
        && hudSettings.Widgets.Count == Enum.GetValues<WidgetKind>().Length,
    "HUD migration includes each widget"
);
var skillWidget = hudSettings.Widgets.Single(w => w.Kind == WidgetKind.Cooldowns);
var small = HudLayout.Resolve(skillWidget, 1920, 1080);
var large = HudLayout.Resolve(skillWidget, 2560, 1440);
Check(
    Math.Abs((large.X - small.X) - 320) < .01 && Math.Abs((large.Y - small.Y) - 360) < .01,
    "Skill HUD remains anchored at bottom centre"
);
skillWidget.X += 11;
HudLayout.Upgrade(hudSettings);
Check(skillWidget.X == 47, "Reload must preserve calibrated HUD positions");
Check(
    ElementCycles.ForClass(HeroClass.Monk).Count == 5
        && ElementCycles
            .ForClass(HeroClass.Necromancer)
            .SequenceEqual(new[] { Element.Cold, Element.Physical, Element.Poison }),
    "Class-specific element order"
);
var clock = new TestClock();
var timers = new SkillTimers(clock);
var configured = new SkillSettings { DurationSeconds = 10 };
timers.Start(0, configured);
clock.Advance(3);
timers.Start(0, configured);
Check(
    Math.Abs(timers.Remaining(0) - 7) < .001,
    "Repeated input must not restart an active skill cooldown"
);
clock.Advance(8);
Check(timers.Remaining(0) == 0, "Skill countdown expires");
timers.Start(0, configured);
timers.Reset();
Check(timers.Remaining(0) == 0, "Skill countdown reset");
int visualChecks = VisualCooldownChecks.Run();
int autoCastChecks = AutoCastChecks.Run();
int skillInputChecks = await SkillInputChecks.RunAsync();
int mapChecks =
    RiftMapChecks.Run()
    + MapMarkerChecks.Run()
    + KadalaChecks.Run()
    + await MyriamChecks.RunAsync();
Console.WriteLine(
    $"All {13 + visualChecks + autoCastChecks + skillInputChecks + mapChecks} checks passed."
);
if (args.Contains("--map-preview") || args.Contains("--marker-preview"))
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            if (args.Contains("--marker-preview"))
                MapMarkerPreview.Save();
            else
                RiftMapPreview.Save();
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    if (failure is not null)
        throw new InvalidOperationException("Map preview failed", failure);
    Console.WriteLine(
        args.Contains("--marker-preview")
            ? "Sample marker preview saved to artifacts/map-marker-preview.png"
            : "Live WPF map preview saved to artifacts/reverse-engineering/wpf-map-preview.png"
    );
}
if (args.Contains("--capture"))
{
    await Task.Run(async () =>
    {
        using var gpu = new GraphicsCapture();
        CaptureFrame? frame = null;
        for (int i = 0; i < 30 && frame is null; i++)
        {
            frame = gpu.Capture(true, 0);
            await Task.Delay(100);
        }
        Check(
            frame is not null && frame.Bgra.Length == frame.Width * frame.Height * 4,
            "GPU screen capture"
        );
        Console.WriteLine(
            $"GPU capture passed: {frame!.Width} x {frame.Height}. Pixels were not saved."
        );
    });
}
if (args.Contains("--hud-preview"))
{
    var previewThread = new Thread(() =>
        HudPreview.Save(Path.GetFullPath("artifacts/hud-preview.png"))
    );
    previewThread.SetApartmentState(ApartmentState.STA);
    previewThread.Start();
    previewThread.Join();
    Console.WriteLine(
        "HUD preview saved to artifacts/hud-preview.png (illustrative background, sample skill times)."
    );
}
static void Check(bool value, string message)
{
    if (!value)
        throw new Exception(message);
}

sealed class NoCapture : IScreenCapture
{
    public CaptureFrame? Capture(bool whole, int monitor) => null;

    public void Dispose() { }
}

sealed class NoOcr : IOcrService
{
    public Task<string> RecognizeAsync(CaptureFrame frame, CancellationToken token) =>
        Task.FromResult("");
}

sealed class TestClock : TimeProvider
{
    long timestamp;
    public override long TimestampFrequency => 1000;

    public override long GetTimestamp() => timestamp;

    public void Advance(double seconds) => timestamp += (long)(seconds * 1000);
}
