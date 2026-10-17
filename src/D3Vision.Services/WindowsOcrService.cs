using System.Runtime.InteropServices.WindowsRuntime;
using D3Vision.Core;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace D3Vision.Services;

public sealed class WindowsOcrService : IOcrService
{
    readonly FrameScaler scaler = new();
    readonly OcrEngine engine =
        OcrEngine.TryCreateFromUserProfileLanguages()
        ?? throw new InvalidOperationException(
            "Install a Windows OCR language pack in Windows language settings."
        );

    public async Task<string> RecognizeAsync(
        CaptureFrame frame,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        frame = scaler.Fit(frame, (int)OcrEngine.MaxImageDimension);
        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(
            frame.Bgra.AsBuffer(),
            BitmapPixelFormat.Bgra8,
            frame.Width,
            frame.Height,
            BitmapAlphaMode.Ignore
        );
        var result = await engine.RecognizeAsync(bitmap).AsTask(cancellationToken);
        return string.Join(Environment.NewLine, result.Lines.Select(line => line.Text));
    }
}
