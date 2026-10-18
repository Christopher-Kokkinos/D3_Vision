using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using D3Vision.Core;
using D3Vision.Rendering;

static class AutoCastVisualChecks
{
    public static int Run()
    {
        Exception? failure = null;
        int count = 0;
        var thread = new Thread(() =>
        {
            try
            {
                byte[] Render(bool enabled, double hover, bool cooling)
                {
                    var visual = new WidgetVisual
                    {
                        Width = 60,
                        Height = 60,
                        Kind = WidgetKind.Cooldowns,
                        SingleSkillSlot = 0,
                        Skills = [new("1", cooling ? 5 : 0, cooling ? .5 : 0)],
                        AutoCastSkills = [enabled],
                        HoveredSkill = 0,
                        HoverProgress = hover,
                    };
                    visual.Measure(new Size(60, 60));
                    visual.Arrange(new Rect(0, 0, 60, 60));
                    var bitmap = new RenderTargetBitmap(60, 60, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(visual);
                    byte[] pixels = new byte[60 * 60 * 4];
                    bitmap.CopyPixels(pixels, 60 * 4, 0);
                    return pixels;
                }
                foreach (bool cooling in new[] { false, true })
                {
                    foreach (bool enabled in new[] { false, true })
                    {
                        var pixels = Render(enabled, 0, cooling);
                        var dot = SkillHitTesting.Dot(60, 60);
                        int index = ((int)dot.Y * 60 + (int)dot.X) * 4;
                        if (
                            enabled
                                ? pixels[index + 1] <= pixels[index + 2]
                                : pixels[index + 2] <= pixels[index + 1]
                        )
                            throw new Exception(
                                "Ready and cooling skills must visibly show the correct red/green dot."
                            );
                        count++;
                    }
                }
                var idle = Render(false, 0, false);
                var hover = Render(false, .5, false);
                if (idle.SequenceEqual(hover))
                    throw new Exception("Hover must render a visible countdown on a ready skill.");
                count++;
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
            throw new InvalidOperationException("Auto-cast visual check failed", failure);
        return count;
    }
}
