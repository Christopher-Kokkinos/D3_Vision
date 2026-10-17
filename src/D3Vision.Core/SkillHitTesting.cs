namespace D3Vision.Core;

public static class SkillHitTesting
{
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
        for (int i = 0; i < Math.Min(6, skills.Count); i++)
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
