using D3Vision.Core;

namespace D3Vision.Services;

/// <summary>Bounds OCR input while retaining aspect ratio. Single-consumer, reusable storage.</summary>
public sealed class FrameScaler
{
    private byte[] buffer = [];

    public CaptureFrame Fit(CaptureFrame source, int maximumDimension)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDimension);
        if (source.Width <= maximumDimension && source.Height <= maximumDimension)
            return source;

        double ratio = (double)maximumDimension / Math.Max(source.Width, source.Height);
        int width = Math.Max(1, (int)(source.Width * ratio));
        int height = Math.Max(1, (int)(source.Height * ratio));
        int length = checked(width * height * 4);
        if (buffer.Length != length)
            buffer = new byte[length];

        for (int y = 0; y < height; y++)
        {
            int sourceRow = (int)((long)y * source.Height / height) * source.Width;
            for (int x = 0; x < width; x++)
            {
                int from = (sourceRow + (int)((long)x * source.Width / width)) * 4;
                int to = (y * width + x) * 4;
                buffer[to] = source.Bgra[from];
                buffer[to + 1] = source.Bgra[from + 1];
                buffer[to + 2] = source.Bgra[from + 2];
                buffer[to + 3] = source.Bgra[from + 3];
            }
        }

        return new(buffer, width, height);
    }
}
