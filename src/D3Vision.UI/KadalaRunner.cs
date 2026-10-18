using D3Vision.Core;
using D3Vision.Infrastructure;
using D3Vision.Services;
using static D3Vision.Services.KadalaScreen;

namespace D3Vision.UI;

/// <summary>Foreground-only Kadala workflow. Does not move the cursor or click during detection.</summary>
internal sealed class KadalaRunner
{
    sealed record Click(int Offer, long Timestamp);

    Click? pendingClick;
    long lastInteraction = Environment.TickCount64 - 1500;
    public bool IsEngaged => Environment.TickCount64 - Interlocked.Read(ref lastInteraction) < 1500;

    public void RecordLeftClick(int x, int y)
    {
        if (!GameInput.TryGetGameClientBounds(out var bounds))
            return;
        var offer = Offer(x - bounds.Left, y - bounds.Top, (bounds.Bottom - bounds.Top) / 1080d);
        if (offer is { } id)
            Interlocked.Exchange(ref pendingClick, new Click(id, Environment.TickCount64));
    }

    readonly KadalaHoverBuyer buyer = new();
    readonly DiabloMapReader reader = new();

    public async Task RunAsync(
        Func<bool> allowed,
        Func<KadalaActivationMode> activationMode,
        Action<string> status,
        CancellationToken token
    )
    {
        GraphicsCapture? capture = null;
        IOcrService? ocr = null;
        RiftMapSnapshot? snapshot = null;
        CaptureFrame? signature = null;
        long recognizedUntil = 0;
        int? recognizedOffer = null;
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(100, token);
                if (
                    !allowed()
                    || !GameInput.TryGetGameClientBounds(out var bounds)
                    || !GameInput.TryGetCursor(out var cursor)
                )
                {
                    if (buyer.IsBuying || buyer.HoverFraction > 0)
                        status(
                            "Kadala paused: hover again after focusing the game with settings closed and overlay locked."
                        );
                    buyer.Reset();
                    Interlocked.Exchange(ref pendingClick, null);
                    signature = null;
                    recognizedUntil = 0;
                    capture?.Dispose();
                    capture = null;
                    continue;
                }
                try
                {
                    capture ??= new GraphicsCapture();
                    ocr ??= new WindowsOcrService();
                    var frame = capture.Capture(false, 0);
                    if (frame is null)
                    {
                        buyer.Reset();
                        continue;
                    }
                    int width = bounds.Right - bounds.Left,
                        height = bounds.Bottom - bounds.Top;
                    if (frame.Width != width || frame.Height != height)
                    {
                        buyer.Reset();
                        continue;
                    }
                    double scale = height / 1080d;
                    int? offer = Offer(cursor.X - bounds.Left, cursor.Y - bounds.Top, scale);
                    if (offer is null)
                    {
                        Interlocked.Exchange(ref pendingClick, null);
                        buyer.Reset();
                        status(buyer.Status);
                        continue;
                    }
                    var footer = Crop(frame, 80, 820, 240, 48, scale);
                    if (
                        Environment.TickCount64 >= recognizedUntil
                        || offer != recognizedOffer
                        || signature is null
                        || !Matches(footer, signature)
                    )
                    {
                        recognizedUntil = 0;
                        string shop = await ocr.RecognizeAsync(footer, token);
                        var tile = Tile(offer.Value);
                        string item = await ocr.RecognizeAsync(
                            Crop(frame, tile.X + 55, tile.Y + 2, 155, 46, scale),
                            token
                        );
                        if (
                            !shop.Contains("blood", StringComparison.OrdinalIgnoreCase)
                            || !shop.Contains("shard", StringComparison.OrdinalIgnoreCase)
                            || !item.Contains("mystery", StringComparison.OrdinalIgnoreCase)
                        )
                        {
                            buyer.Reset();
                            status(
                                "Kadala: waiting for a visible Mystery offer and Blood Shards shop."
                            );
                            continue;
                        }
                        signature = footer;
                        recognizedOffer = offer;
                        recognizedUntil = Environment.TickCount64 + 1000;
                    }
                    // Refresh the image and cursor after asynchronous OCR. A closed/switched
                    // vendor must never inherit the last recognised Kadala state.
                    var fresh = capture.Capture(false, 0);
                    if (
                        fresh is null
                        || !Matches(Crop(fresh, 80, 820, 240, 48, scale), signature!)
                        || !allowed()
                        || !GameInput.TryGetGameClientBounds(out var current)
                        || current.Left != bounds.Left
                        || current.Top != bounds.Top
                        || current.Right != bounds.Right
                        || current.Bottom != bounds.Bottom
                        || !GameInput.TryGetCursor(out cursor)
                        || Offer(cursor.X - bounds.Left, cursor.Y - bounds.Top, scale) != offer
                    )
                    {
                        buyer.Reset();
                        recognizedUntil = 0;
                        continue;
                    }
                    Interlocked.Exchange(ref lastInteraction, Environment.TickCount64);
                    // Rehash only when establishing/re-establishing a supported process.
                    snapshot ??= reader.Capture();
                    int count = reader.ReadBackpack(snapshot).Count;
                    var click = Interlocked.Exchange(ref pendingClick, null);
                    bool clicked =
                        click is not null
                        && click.Offer == offer
                        && Environment.TickCount64 - click.Timestamp <= 1000;
                    if (buyer.Update(offer, true, count, activationMode(), clicked))
                    {
                        if (
                            !allowed()
                            || !GameInput.TryGetCursor(out cursor)
                            || Offer(cursor.X - bounds.Left, cursor.Y - bounds.Top, scale) != offer
                        )
                        {
                            buyer.Reset();
                            continue;
                        }
                        var rapid = new GameWindowInventoryInput(bounds);
                        bool Guard() =>
                            allowed()
                            && GameInput.TryGetGameClientBounds(out var now)
                            && now.Left == bounds.Left
                            && now.Top == bounds.Top
                            && now.Right == bounds.Right
                            && now.Bottom == bounds.Bottom
                            && GameInput.TryGetCursor(out var point)
                            && Offer(point.X - bounds.Left, point.Y - bounds.Top, scale) == offer;
                        // A bounded burst is acknowledged by the next inventory read. Full
                        // inventories / exhausted shards stop through the existing no-progress latch.
                        int burst = Math.Clamp((60 - count) / 2, 1, 4);
                        for (int i = 0; i < burst; i++)
                            rapid.Click(
                                cursor.X - bounds.Left,
                                cursor.Y - bounds.Top,
                                true,
                                Guard,
                                token
                            );
                    }
                    status(buyer.Status);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                    when (ex
                            is System.ComponentModel.Win32Exception
                                or InvalidOperationException
                                or ArgumentException
                                or System.IO.IOException
                                or UnauthorizedAccessException
                                or NotSupportedException
                    )
                {
                    buyer.Stop("Kadala stopped: " + ex.Message);
                    status(buyer.Status);
                    snapshot = null;
                    signature = null;
                    recognizedUntil = 0;
                    // A failed reader/input requires leaving the offer or reopening settings.
                    while (
                        allowed()
                        && GameInput.TryGetGameClientBounds(out var rect)
                        && GameInput.TryGetCursor(out var point)
                        && Offer(
                            point.X - rect.Left,
                            point.Y - rect.Top,
                            (rect.Bottom - rect.Top) / 1080d
                        )
                            is not null
                    )
                        await Task.Delay(100, token);
                    buyer.Reset();
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            capture?.Dispose();
        }
    }
}
