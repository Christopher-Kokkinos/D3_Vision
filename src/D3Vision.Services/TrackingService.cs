using System.Text.RegularExpressions;
using D3Vision.Core;

namespace D3Vision.Services;

public sealed partial class TrackingService(IScreenCapture capture, IOcrService ocr)
{
    public event Action<string>? TextRecognized;
    public event Action<string>? StatusChanged;
    public event Action<IReadOnlyList<CooldownObservation>>? CooldownsRead;
    public event Action<CaptureArea?>? CaptureAreaRead;
    readonly CooldownSweepReader sweepReader = new();
    string lastStatus = "";

    void Report(string status)
    {
        if (lastStatus == status)
            return;
        lastStatus = status;
        StatusChanged?.Invoke(status);
    }

    public async Task RunAsync(
        Func<(bool Whole, int Monitor, int Interval)> options,
        CancellationToken token,
        Func<IReadOnlyList<SkillCalibration?>>? cooldownRegions = null
    )
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        using var ocrStop = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task<string>? pendingOcr = null;
        var next = DateTime.MinValue;
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                if (pendingOcr is { IsCompleted: true })
                {
                    var completed = pendingOcr;
                    pendingOcr = null;
                    try
                    {
                        TextRecognized?.Invoke(await completed);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Report("OCR unavailable: " + ex.Message);
                    }
                }
                try
                {
                    var setting = options();
                    var frame = capture.Capture(setting.Whole, setting.Monitor);
                    if (frame is null)
                    {
                        // Keep the last coordinates until their freshness deadline. A single
                        // empty frame must not make the skill dots blink or disarm every slot.
                        Report("Waiting for Diablo III (windowed/borderless)");
                        continue;
                    }
                    CaptureAreaRead?.Invoke(frame.Area);
                    if (cooldownRegions is not null)
                    {
                        var regions = cooldownRegions();
                        if (regions.Count > 0)
                            CooldownsRead?.Invoke(
                                regions
                                    .Take(SkillSlots.Count)
                                    .Select((region, slot) => sweepReader.Read(frame, slot, region))
                                    .ToArray()
                            );
                    }
                    Report("Capture active - 10 FPS / OCR throttled");
                    if (pendingOcr is not null || DateTime.UtcNow < next)
                        continue;
                    next = DateTime.UtcNow.AddMilliseconds(Math.Max(500, setting.Interval));
                    // Capture reuses its pixel buffer. OCR owns a copy and has at most one
                    // request in flight, so slow text recognition cannot starve cooldown reads.
                    var copy = frame with
                    {
                        Bgra = frame.Bgra.ToArray(),
                    };
                    pendingOcr = Task.Run(
                        () => ocr.RecognizeAsync(copy, ocrStop.Token),
                        ocrStop.Token
                    );
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Report(ex.Message);
                    capture.Dispose();
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            ocrStop.Cancel();
            if (pendingOcr is not null)
                try
                {
                    await pendingOcr;
                }
                catch (OperationCanceledException) when (ocrStop.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    Report("OCR unavailable: " + ex.Message);
                }
        }
    }

    readonly HashSet<string> previous = [];

    public void Apply(string text, SessionStats stats)
    {
        var current = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var line in current.Except(previous, StringComparer.OrdinalIgnoreCase))
        {
            if (line.Contains("rift complete", StringComparison.OrdinalIgnoreCase))
            {
                stats.Rifts++;
                stats.Progress = 100;
            }
            var timer = TimerPattern().Match(line);
            if (timer.Success)
                stats.GameTimer = timer.Value;
            var xp = XpPattern().Match(line);
            if (xp.Success && long.TryParse(xp.Groups[1].Value.Replace(",", ""), out var value))
                stats.Experience += value;
            var progress = ProgressPattern().Match(line);
            if (
                progress.Success
                && double.TryParse(
                    progress.Groups[1].Value,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var p
                )
            )
                stats.Progress = Math.Clamp(p, 0, 100);
            if (
                line.Contains("legendary", StringComparison.OrdinalIgnoreCase)
                || line.Contains("set item", StringComparison.OrdinalIgnoreCase)
            )
            {
                stats.ItemDrops.Add(line);
                if (stats.ItemDrops.Count > 200)
                    stats.ItemDrops.RemoveAt(0);
            }
        }
        previous.Clear();
        previous.UnionWith(current);
    }

    [GeneratedRegex(@"\b\d{1,2}:\d{2}\b")]
    private static partial Regex TimerPattern();

    [GeneratedRegex(@"([\d,]+)\s+(?:XP|experience)\b", RegexOptions.IgnoreCase)]
    private static partial Regex XpPattern();

    [GeneratedRegex(@"(\d{1,3}(?:\.\d+)?)\s*%")]
    private static partial Regex ProgressPattern();
}
