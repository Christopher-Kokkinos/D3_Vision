using D3Vision.Core;
using D3Vision.Services;

static class CaptureCadenceChecks
{
    public static async Task<int> RunAsync()
    {
        using var stop = new CancellationTokenSource();
        using var capture = new FakeCapture();
        var ocr = new SlowOcr();
        var service = new TrackingService(capture, ocr);
        int areas = 0;
        var enoughFrames = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        service.CaptureAreaRead += area =>
        {
            if (area is not null && ++areas >= 7)
                enoughFrames.TrySetResult();
        };
        var task = service.RunAsync(() => (false, 0, 500), stop.Token);
        try
        {
            await enoughFrames.Task.WaitAsync(TimeSpan.FromSeconds(8));
            if (ocr.Calls != 1 || ocr.Frame is null)
                throw new Exception(
                    "Slow OCR must have at most one request in flight while capture continues."
                );
            if (ocr.Frame.Bgra[0] != 1 || capture.Frames < 7)
                throw new Exception("OCR must own its pixels while capture reuses its buffer.");
        }
        finally
        {
            stop.Cancel();
            await task;
        }
        return 3;
    }

    sealed class FakeCapture : IScreenCapture
    {
        readonly byte[] pixels = new byte[4];
        public int Frames;

        public CaptureFrame Capture(bool wholeScreen, int monitor)
        {
            pixels[0] = (byte)++Frames;
            return new(pixels, 1, 1, new(0, 0, 1, 1, false, 0));
        }

        public void Dispose() { }
    }

    sealed class SlowOcr : IOcrService
    {
        public CaptureFrame? Frame;
        public int Calls;

        public async Task<string> RecognizeAsync(CaptureFrame frame, CancellationToken token)
        {
            Frame = frame;
            Interlocked.Increment(ref Calls);
            await Task.Delay(Timeout.Infinite, token);
            return "";
        }
    }
}
