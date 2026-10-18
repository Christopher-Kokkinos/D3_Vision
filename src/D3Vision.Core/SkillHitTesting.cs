namespace D3Vision.Core;

public static class SkillHitTesting
{
    // Geometry is expressed in the same units as the icon; rendering and physical-pixel
    // hit testing therefore agree at every DPI and capture resolution.
    public static (double X, double Y, double Radius) Dot(double width, double height)
    {
        double radius = Math.Min(width, height) * .14;
        return (width - radius * 1.25, radius * 1.25, radius);
    }

    public static bool ContainsDot(double width, double height, double x, double y)
    {
        if (
            !double.IsFinite(width)
            || !double.IsFinite(height)
            || width <= 0
            || height <= 0
            || !double.IsFinite(x)
            || !double.IsFinite(y)
        )
            return false;
        var dot = Dot(width, height);
        double dx = x - dot.X,
            dy = y - dot.Y;
        return dx * dx + dy * dy <= dot.Radius * dot.Radius;
    }

    public static int? HitDot(
        IReadOnlyList<SkillSettings> skills,
        double x,
        double y,
        double aspect,
        bool wholeScreen = false,
        int monitor = 0
    )
    {
        var slot = Hit(skills, x, y, aspect, wholeScreen, monitor);
        if (slot is not { } i || skills[i].Calibration is not { } c)
            return null;
        // Use height units on both axes so the hit area remains a circle.
        var dot = Dot(c.Width * aspect, c.Height);
        double dx = (x - c.X) * aspect - dot.X;
        double dy = y - c.Y - dot.Y;
        return dx * dx + dy * dy <= dot.Radius * dot.Radius ? i : null;
    }

    // Only calibrated game artwork is interactive. Movable display badges never arm skills.
    public static int? Hit(
        IReadOnlyList<SkillSettings> skills,
        double x,
        double y,
        double aspect,
        bool wholeScreen = false,
        int monitor = 0
    )
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(aspect) || aspect <= 0)
            return null;
        int? found = null;
        for (int i = 0; i < Math.Min(SkillSlots.Count, skills.Count); i++)
        {
            if (
                skills[i].Calibration is not { } c
                || !double.IsFinite(c.CaptureAspectRatio)
                || c.CaptureAspectRatio <= 0
                || c.WholeScreen is not { } whole
                || whole != wholeScreen
                || (wholeScreen && c.Monitor is { } selected && selected != monitor)
                || Math.Abs(c.CaptureAspectRatio / aspect - 1) > .015
                || c.X < 0
                || c.Y < 0
                || c.Width <= 0
                || c.Height <= 0
                || c.X + c.Width > 1
                || c.Y + c.Height > 1
            )
                continue;
            if (x >= c.X && x < c.X + c.Width && y >= c.Y && y < c.Y + c.Height)
            {
                if (found is not null)
                    return null; // Ambiguous overlapping calibrations cannot toggle either skill.
                found = i;
            }
        }
        return found;
    }
}
