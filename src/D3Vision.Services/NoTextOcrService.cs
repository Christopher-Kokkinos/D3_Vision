using D3Vision.Core;

namespace D3Vision.Services;

/// <summary>Visual skill tracking can operate even when Windows text OCR is unavailable.</summary>
public sealed class NoTextOcrService : IOcrService
{
    public Task<string> RecognizeAsync(CaptureFrame frame, CancellationToken cancellationToken) =>
        Task.FromResult("");
}
