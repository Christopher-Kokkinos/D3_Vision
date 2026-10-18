using D3Vision.Core;

namespace D3Vision.Services;

/// <summary>Keep inventory/vendor headings, footer and the complete central dialog area for OCR.</summary>
public static class InventoryOcrFrame
{
    public static CaptureFrame Create(CaptureFrame source)
    {
        double scale = source.Height / 1080d;
        int Pixel(double value) => (int)Math.Round(value * scale);
        if (
            source.Width <= 0
            || source.Height <= 0
            || source.Bgra.Length != (long)source.Width * source.Height * 4
            || source.Width / (double)source.Height is < 1.7 or > 3.6
        )
            throw new InvalidOperationException("Unsupported inventory OCR layout.");
        int side = Pixel(530),
            middle = Pixel(700),
            gap = Pixel(12);
        int width = side * 2 + middle + gap * 2,
            height = Pixel(500);
        var output = new byte[checked(width * height * 4)];
        for (int i = 3; i < output.Length; i += 4)
            output[i] = 255;
        void Copy(int x, int y, int w, int h, int targetX, int targetY)
        {
            if (
                x < 0
                || y < 0
                || x + w > source.Width
                || y + h > source.Height
                || targetX + w > width
                || targetY + h > height
            )
                throw new InvalidOperationException("Inventory OCR crop is out of bounds.");
            for (int row = 0; row < h; row++)
                Buffer.BlockCopy(
                    source.Bgra,
                    ((y + row) * source.Width + x) * 4,
                    output,
                    ((targetY + row) * width + targetX) * 4,
                    w * 4
                );
        }
        Copy(0, Pixel(100), side, Pixel(400), 0, 0);
        Copy(Pixel(80), Pixel(820), Pixel(300), Pixel(80), 0, Pixel(412));
        Copy(
            source.Width / 2 - Pixel(350),
            source.Height / 2 - Pixel(220),
            middle,
            Pixel(440),
            side + gap,
            0
        );
        Copy(source.Width - side, Pixel(100), side, Pixel(450), side + middle + gap * 2, 0);
        return new(output, width, height);
    }
}
