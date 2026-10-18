using D3Vision.Core;

namespace D3Vision.Services;

/// <summary>Conservative comparison against a calibrated, ready icon. No OCR or key events.</summary>
public sealed class CooldownSweepReader
{
    public const int PatchSize = 64;
    const int Sectors = 72;

    // Ready Diablo artwork often contains large near-black areas. Eight radial
    // samples cover its visible strokes without requiring a uniformly bright icon.
    const double MinimumReferenceLuminance = 12;
    const int MinimumUsableSectors = 60;
    static readonly (int X, int Y)[][] Samples = Enumerable
        .Range(0, Sectors)
        .Select(i =>
            new[] { .2, .3, .4, .5, .6, .7, .8, .9 }
                .Select(radius =>
                {
                    double angle = (i + .5) * Math.Tau / Sectors;
                    return (
                        (int)(32 + Math.Sin(angle) * radius * 30),
                        (int)(32 - Math.Cos(angle) * radius * 30)
                    );
                })
                .ToArray()
        )
        .ToArray();

    public static SkillCalibration Calibrate(
        CaptureFrame frame,
        double x,
        double y,
        double width,
        double height
    )
    {
        if (
            frame.Width <= 0
            || frame.Height <= 0
            || frame.Bgra.Length != (long)frame.Width * frame.Height * 4
            || !ValidRegion(x, y, width, height)
            || width * frame.Width < 16
            || height * frame.Height < 16
            || width * frame.Width / (height * frame.Height) is < .65 or > 1.5
        )
            throw new ArgumentException(
                "Select the square skill artwork, at least 16 pixels wide, excluding its key label."
            );
        var patch = new byte[PatchSize * PatchSize * 4];
        for (int py = 0; py < PatchSize; py++)
        for (int px = 0; px < PatchSize; px++)
        {
            int source = Offset(
                frame,
                x + (px + .5) / PatchSize * width,
                y + (py + .5) / PatchSize * height
            );
            frame.Bgra.AsSpan(source, 4).CopyTo(patch.AsSpan((py * PatchSize + px) * 4, 4));
        }
        var calibration = new SkillCalibration(
            x,
            y,
            width,
            height,
            patch,
            (double)frame.Width / frame.Height
        );
        if (!IsUsable(calibration))
            throw new ArgumentException(
                "Not enough visible skill artwork was found. Select the whole ready icon, excluding its border and key label. Close menus covering it; try game-window capture if the image is black."
            );
        return calibration;
    }

    public CooldownObservation Read(CaptureFrame frame, int slot, SkillCalibration? calibration)
    {
        if (
            calibration is null
            || !ValidRegion(calibration.X, calibration.Y, calibration.Width, calibration.Height)
            || !IsUsable(calibration)
            || frame.Width <= 0
            || frame.Height <= 0
            || frame.Bgra.Length != (long)frame.Width * frame.Height * 4
            || (
                calibration.CaptureAspectRatio > 0
                && Math.Abs((double)frame.Width / frame.Height / calibration.CaptureAspectRatio - 1)
                    > .04
            )
        )
            return new(slot, CooldownVisibility.Unknown, 0);
        Span<bool> shaded = stackalloc bool[Sectors];
        Span<bool> usableSectors = stackalloc bool[Sectors];
        int valid = 0,
            dark = 0;
        for (int i = 0; i < Sectors; i++)
        {
            int usable = 0,
                darker = 0;
            foreach (var (x, y) in Samples[i])
            {
                int referenceOffset = (y * PatchSize + x) * 4;
                double reference = Luminance(calibration.ReadyBgra, referenceOffset);
                if (reference < MinimumReferenceLuminance)
                    continue;
                usable++;
                int currentOffset = Offset(
                    frame,
                    calibration.X + (x + .5) / PatchSize * calibration.Width,
                    calibration.Y + (y + .5) / PatchSize * calibration.Height
                );
                if (Luminance(frame.Bgra, currentOffset) < reference * .72)
                    darker++;
            }
            if (usable < 2)
                continue;
            valid++;
            usableSectors[i] = true;
            shaded[i] = darker >= Math.Max(2, (int)Math.Ceiling(usable * .6));
            if (shaded[i])
                dark++;
        }
        if (valid < MinimumUsableSectors)
            return new(slot, CooldownVisibility.Unknown, 0);
        int transitions = 0;
        int firstUsable = -1,
            previousUsable = -1;
        for (int i = 0; i < Sectors; i++)
        {
            if (!usableSectors[i])
                continue;
            if (firstUsable < 0)
                firstUsable = i;
            if (previousUsable >= 0 && shaded[i] != shaded[previousUsable])
                transitions++;
            previousUsable = i;
        }
        if (shaded[previousUsable] != shaded[firstUsable])
            transitions++;
        // A cooldown sweep is one contiguous sector. Noisy texture changes are unknown.
        if (transitions > 4)
            return new(slot, CooldownVisibility.Unknown, 0);
        double fraction = (double)dark / valid;
        return fraction <= .02
            ? new(slot, CooldownVisibility.Ready, 0)
            : new(slot, CooldownVisibility.Cooling, fraction);
    }

    static bool IsUsable(SkillCalibration calibration)
    {
        if (calibration.ReadyBgra is not { Length: PatchSize * PatchSize * 4 })
            return false;
        int usable = 0;
        foreach (var sector in Samples)
            if (
                sector.Count(p =>
                    Luminance(calibration.ReadyBgra, (p.Y * PatchSize + p.X) * 4)
                    >= MinimumReferenceLuminance
                ) >= 2
            )
                usable++;
        return usable >= MinimumUsableSectors;
    }

    static bool ValidRegion(double x, double y, double width, double height) =>
        double.IsFinite(x)
        && double.IsFinite(y)
        && double.IsFinite(width)
        && double.IsFinite(height)
        && x >= 0
        && y >= 0
        && width > 0
        && height > 0
        && x + width <= 1.000001
        && y + height <= 1.000001;

    static int Offset(CaptureFrame frame, double x, double y) =>
        (
            Math.Clamp((int)(y * frame.Height), 0, frame.Height - 1) * frame.Width
            + Math.Clamp((int)(x * frame.Width), 0, frame.Width - 1)
        ) * 4;

    static double Luminance(byte[] pixels, int offset) =>
        .0722 * pixels[offset] + .7152 * pixels[offset + 1] + .2126 * pixels[offset + 2];
}
