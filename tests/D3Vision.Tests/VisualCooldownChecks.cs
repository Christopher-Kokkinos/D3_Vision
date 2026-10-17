using System.Text.Json;
using D3Vision.Core;
using D3Vision.Services;

static class VisualCooldownChecks
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
            count++;
        }
        var ready = Frame(0);
        var calibration = CooldownSweepReader.Calibrate(ready, 0, 0, 1, 1);
        var reader = new CooldownSweepReader();
        Check(
            reader.Read(ready, 0, calibration).Visibility == CooldownVisibility.Ready,
            "Ready template recognition"
        );
        var half = reader.Read(Frame(.5), 0, calibration);
        Check(
            half.Visibility == CooldownVisibility.Cooling && Math.Abs(half.Fraction - .5) < .04,
            "Half sweep recognition"
        );
        Check(
            reader.Read(ready, 0, null).Visibility == CooldownVisibility.Unknown,
            "Uncalibrated icons stay unknown"
        );
        bool rejected = false;
        try
        {
            CooldownSweepReader.Calibrate(ready, .99, 0, .5, 1);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }
        Check(rejected, "Reject out-of-bounds calibration");
        Check(
            reader
                .Read(new CaptureFrame(new byte[128 * 64 * 4], 128, 64), 0, calibration)
                .Visibility == CooldownVisibility.Unknown,
            "Aspect ratio changes invalidate calibration"
        );
        Check(
            reader.Read(Frame(.5, true), 0, calibration).Visibility == CooldownVisibility.Unknown,
            "Noisy non-radial changes are not cooldowns"
        );
        var restored = JsonSerializer.Deserialize<SkillCalibration>(
            JsonSerializer.Serialize(calibration)
        );
        Check(
            reader.Read(ready, 0, restored).Visibility == CooldownVisibility.Ready,
            "Calibration JSON round trip"
        );

        var clock = new TestClock();
        var timers = new VisualCooldownTimers(clock);
        var skills = SkillSettings.Defaults();
        Check(timers.Snapshot(skills)[0].IsUnknown, "Missing data must not be presented as ready");
        for (int i = 0; i < 6; i++)
        {
            timers.Apply(new(0, CooldownVisibility.Cooling, .9 - .02 * i));
            if (i < 5)
                clock.Advance(.2);
        }
        var learned = timers.Snapshot(skills)[0];
        Check(
            learned.IsEstimated
                && !learned.IsUnknown
                && Math.Abs(learned.RemainingSeconds - 8) < .02,
            "Learn countdown from observed sweep speed"
        );
        clock.Advance(.1);
        timers.Apply(new(0, CooldownVisibility.Cooling, .3));
        Check(
            Math.Abs(timers.Snapshot(skills)[0].RemainingSeconds - 3) < .02,
            "Observed reductions update remaining time"
        );
        timers.Apply(new(0, CooldownVisibility.Ready, 0));
        Check(
            timers.Snapshot(skills)[0].RemainingSeconds > 0,
            "One bright frame cannot reset cooldown"
        );
        timers.Apply(new(0, CooldownVisibility.Ready, 0));
        Check(
            !timers.Snapshot(skills)[0].IsUnknown
                && timers.Snapshot(skills)[0].RemainingSeconds == 0,
            "Confirmed visual reset clears cooldown"
        );
        clock.Advance(1.1);
        Check(timers.Snapshot(skills)[0].IsUnknown, "Stale capture becomes unknown");
        timers.Apply(new(0, CooldownVisibility.Cooling, double.NaN));
        Check(
            timers.Snapshot(skills)[0].IsUnknown,
            "Invalid observations never reach the renderer as NaN"
        );
        return count;
    }

    static CaptureFrame Frame(double fraction, bool noisy = false)
    {
        const int size = 64;
        var pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            double angle = Math.Atan2(x + .5 - size / 2, -(y + .5 - size / 2));
            if (angle < 0)
                angle += Math.Tau;
            bool dark = noisy
                ? ((int)(angle / Math.Tau * 72)) % 2 == 0
                : angle < Math.Tau * fraction;
            int offset = (y * size + x) * 4;
            byte value = (byte)(dark ? 60 : 120);
            pixels[offset] = pixels[offset + 1] = pixels[offset + 2] = value;
            pixels[offset + 3] = 255;
        }
        return new(pixels, size, size);
    }
}
