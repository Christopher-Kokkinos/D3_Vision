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

        // Dark ready artwork is valid; a shaded version must still read as cooling.
        foreach (byte brightness in new byte[] { 14, 20, 30 })
        {
            var darkReady = Scale(Frame(0), brightness / 120d);
            var darkCalibration = CooldownSweepReader.Calibrate(darkReady, 0, 0, 1, 1);
            Check(
                reader.Read(darkReady, 0, darkCalibration).Visibility == CooldownVisibility.Ready,
                "Naturally dark ready artwork can be calibrated and recognised"
            );
            var darkHalf = reader.Read(Scale(Frame(.5), brightness / 120d), 0, darkCalibration);
            Check(
                darkHalf.Visibility == CooldownVisibility.Cooling
                    && Math.Abs(darkHalf.Fraction - .5) < .04,
                "Dimming dark artwork still detects its cooldown sweep"
            );
            Check(
                reader
                    .Read(Scale(Frame(.5, true), brightness / 120d), 0, darkCalibration)
                    .Visibility == CooldownVisibility.Unknown,
                "Dark noisy changes cannot confirm readiness"
            );
            Check(
                reader.Read(new(new byte[64 * 64 * 4], 64, 64), 0, darkCalibration).Visibility
                    != CooldownVisibility.Ready,
                "A black capture cannot make a calibrated dark skill ready"
            );
        }
        foreach (byte brightness in new byte[] { 0, 4, 10 })
        {
            bool refused = false;
            try
            {
                CooldownSweepReader.Calibrate(Scale(Frame(0), brightness / 120d), 0, 0, 1, 1);
            }
            catch (ArgumentException)
            {
                refused = true;
            }
            Check(refused, "Black and near-black selections remain uncalibratable");
        }
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
        var potionTimers = new VisualCooldownTimers(clock);
        potionTimers.Apply(new(SkillSlots.Potion, CooldownVisibility.Ready, 0));
        potionTimers.Apply(new(SkillSlots.Potion, CooldownVisibility.Ready, 0));
        Check(
            !potionTimers.Snapshot(skills)[SkillSlots.Potion].IsUnknown,
            "Potion has its own confirmed-ready visual observation state"
        );
        potionTimers.Apply(new(SkillSlots.Potion, CooldownVisibility.Cooling, .5));
        Check(
            potionTimers.Snapshot(skills)[SkillSlots.Potion].Fraction == .5,
            "Potion's observed cooldown stays separate from skill cooldowns"
        );
        return count;
    }

    static CaptureFrame Scale(CaptureFrame source, double scale)
    {
        var pixels = source.Bgra.ToArray();
        for (int i = 0; i < pixels.Length; i++)
            if (i % 4 != 3)
                pixels[i] = (byte)Math.Round(pixels[i] * scale);
        return source with { Bgra = pixels };
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
