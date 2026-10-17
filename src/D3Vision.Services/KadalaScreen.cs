using D3Vision.Core;

namespace D3Vision.Services;

/// <summary>Client-pixel layout and image checks for the standard Kadala vendor panel.</summary>
public static class KadalaScreen
{
    public static (int X, int Y) Tile(int offer) =>
        (43 + (offer % 2) * 225, 166 + (offer / 2) * 107);

    public static int? Offer(double x, double y, double scale)
    {
        if (scale <= 0)
            return null;
        x /= scale;
        y /= scale;
        for (int i = 0; i < 12; i++)
        {
            var p = Tile(i);
            if (x >= p.X && x < p.X + 213 && y >= p.Y && y < p.Y + 96)
                return i;
        }
        return null;
    }

    public static CaptureFrame Crop(
        CaptureFrame frame,
        int x,
        int y,
        int width,
        int height,
        double scale
    )
    {
        x = (int)Math.Round(x * scale);
        y = (int)Math.Round(y * scale);
        width = (int)Math.Round(width * scale);
        height = (int)Math.Round(height * scale);
        if (
            x < 0
            || y < 0
            || width <= 0
            || height <= 0
            || x + width > frame.Width
            || y + height > frame.Height
        )
            throw new InvalidOperationException("Unsupported Kadala capture layout.");
        var data = new byte[width * height * 4];
        for (int row = 0; row < height; row++)
            Buffer.BlockCopy(
                frame.Bgra,
                ((y + row) * frame.Width + x) * 4,
                data,
                row * width * 4,
                width * 4
            );
        return new(data, width, height);
    }

    public static bool Matches(CaptureFrame a, CaptureFrame b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
            return false;
        int different = 0,
            total = 0;
        for (int i = 0; i < a.Bgra.Length; i += 16)
        {
            total++;
            if (
                Math.Abs(a.Bgra[i] - b.Bgra[i])
                    + Math.Abs(a.Bgra[i + 1] - b.Bgra[i + 1])
                    + Math.Abs(a.Bgra[i + 2] - b.Bgra[i + 2])
                > 24
            )
                different++;
        }
        return different <= total * .02;
    }
}
