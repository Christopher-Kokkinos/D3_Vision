using System.Collections.Concurrent;
using D3Vision.Core;

namespace D3Vision.Infrastructure;

/// <summary>Serial, fresh game-window captures; DirectX creation/use/disposal stay on one worker.</summary>
public sealed class GameCaptureWorker : IAsyncDisposable
{
    sealed record Request(CancellationToken Token, TaskCompletionSource<CaptureFrame> Completion);

    readonly BlockingCollection<Request> requests = new(1);
    readonly CancellationTokenSource stop = new();
    readonly TaskCompletionSource finished = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    readonly Func<IScreenCapture> factory;

    public GameCaptureWorker(Func<IScreenCapture>? factory = null)
    {
        this.factory = factory ?? (() => new GraphicsCapture());
        new Thread(Run) { IsBackground = true, Name = "D3Vision inventory capture" }.Start();
    }

    public async Task<CaptureFrame> CaptureAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(stop.IsCancellationRequested, this);
        var request = new Request(token, new(TaskCreationOptions.RunContinuationsAsynchronously));
        if (!requests.TryAdd(request))
            throw new InvalidOperationException("Inventory capture is already queued.");
        return await request.Completion.Task.WaitAsync(token).ConfigureAwait(false);
    }

    void Run()
    {
        IScreenCapture? capture = null;
        Exception? failure = null;
        try
        {
            foreach (var request in requests.GetConsumingEnumerable(stop.Token))
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    stop.Token,
                    request.Token
                );
                try
                {
                    linked.Token.ThrowIfCancellationRequested();
                    capture ??= factory();
                    // The WGC pool holds two frames. Discard both so vendor/dialog
                    // validation never uses an image left queued before the previous action.
                    capture.Capture(false, 0);
                    capture.Capture(false, 0);
                    CaptureFrame? frame = null;
                    for (int attempt = 0; attempt < 150; attempt++)
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        frame = capture.Capture(false, 0);
                        if (frame is not null)
                            break;
                        if (linked.Token.WaitHandle.WaitOne(10))
                            linked.Token.ThrowIfCancellationRequested();
                    }
                    if (frame is null)
                        throw new InvalidOperationException("No fresh game-window capture.");
                    request.Completion.TrySetResult(frame with { Bgra = frame.Bgra.ToArray() });
                }
                catch (OperationCanceledException)
                {
                    request.Completion.TrySetCanceled(linked.Token);
                }
                catch (Exception ex)
                {
                    request.Completion.TrySetException(ex);
                    capture?.Dispose();
                    capture = null;
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception ex)
        {
            failure = ex;
            stop.Cancel();
        }
        finally
        {
            try
            {
                capture?.Dispose();
            }
            catch (Exception ex)
            {
                failure ??= ex;
            }
            while (requests.TryTake(out var pending))
            {
                if (failure is not null)
                    pending.Completion.TrySetException(failure);
                else
                    pending.Completion.TrySetCanceled(stop.Token);
            }
            if (failure is not null)
                finished.TrySetException(failure);
            else
                finished.TrySetResult();
        }
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        requests.CompleteAdding();
        await finished.Task.ConfigureAwait(false);
        requests.Dispose();
        stop.Dispose();
    }
}
