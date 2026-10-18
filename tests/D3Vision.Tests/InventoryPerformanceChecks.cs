using D3Vision.Core;
using D3Vision.Infrastructure;
using D3Vision.Services;

static class InventoryPerformanceChecks
{
    public static async Task<int> RunAsync()
    {
        int count = 0;
        void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
            count++;
        }
        var fake = new FakeCapture();
        int creations = 0;
        await using (
            var worker = new GameCaptureWorker(() =>
            {
                creations++;
                return fake;
            })
        )
        {
            var first = await worker
                .CaptureAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5));
            var second = await worker
                .CaptureAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Check(
                creations == 1,
                "A batch reuses one capture session rather than starting DirectX for every item"
            );
            Check(
                first.Bgra[0] == 3 && second.Bgra[0] == 6,
                "Two queued frames are discarded and each caller receives independently owned fresh pixels"
            );
        }
        Check(
            fake.Threads.Distinct().Count() == 1 && fake.DisposedThread == fake.Threads[0],
            "Capture creation/use/disposal remain on one dedicated worker"
        );
        var waiting = new FakeCapture { Empty = true };
        await using (var worker = new GameCaptureWorker(() => waiting))
        {
            using var stop = new CancellationTokenSource();
            var request = worker.CaptureAsync(stop.Token);
            await waiting.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            stop.Cancel();
            bool cancelled = false;
            try
            {
                await request;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            Check(
                cancelled,
                "Cancellation interrupts a pending fresh capture without waiting out the capture timeout"
            );
        }
        Check(waiting.DisposedThread != 0, "Cancelled batches release their capture resources");
        int attempts = 0;
        await using (
            var worker = new GameCaptureWorker(() =>
                ++attempts == 1 ? throw new InvalidOperationException("fixture") : new FakeCapture()
            )
        )
        {
            bool failed = false;
            try
            {
                await worker.CaptureAsync(CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                failed = true;
            }
            Check(
                failed,
                "Capture setup errors are returned to the action, not thrown unhandled on its worker"
            );
            Check(
                (await worker.CaptureAsync(CancellationToken.None)).Width == 1,
                "A later request can recover after a failed capture setup"
            );
        }
        var pixels = new byte[1920 * 1080 * 4];
        void Mark(int x, int y, byte value)
        {
            pixels[(y * 1920 + x) * 4] = value;
        }
        Mark(0, 100, 11);
        Mark(80, 820, 22);
        Mark(610, 320, 33);
        Mark(1390, 100, 44);
        var cropped = InventoryOcrFrame.Create(new(pixels, 1920, 1080));
        byte At(int x, int y) => cropped.Bgra[(y * cropped.Width + x) * 4];
        Check(
            cropped.Width * cropped.Height < 1920 * 1080 / 2,
            "Inventory OCR processes less than half the original pixels"
        );
        Check(
            At(0, 0) == 11 && At(0, 412) == 22,
            "OCR keeps vendor headings and currency/vendor footer"
        );
        Check(
            At(542, 0) == 33 && At(1254, 0) == 44,
            "OCR keeps the central confirmation area and inventory headings"
        );
        Check(
            InventoryScreenRecognition.Salvage(
                "SALVAGE RESULTS\nClick an item in your inventory to salvage it."
            ),
            "An already-active anvil remains recognised across a batch"
        );
        return count;
    }

    sealed class FakeCapture : IScreenCapture
    {
        readonly byte[] buffer = new byte[4];
        int captures;
        public bool Empty;
        public List<int> Threads = [];
        public int DisposedThread;
        public TaskCompletionSource Started = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        public CaptureFrame? Capture(bool wholeScreen, int monitor)
        {
            Threads.Add(Environment.CurrentManagedThreadId);
            Started.TrySetResult();
            buffer[0] = (byte)++captures;
            return Empty ? null : new(buffer, 1, 1);
        }

        public void Dispose()
        {
            DisposedThread = Environment.CurrentManagedThreadId;
        }
    }
}
