using D3Vision.Core;

static class AutoCastChecks
{
    public static int Run()
    {
        int count = 0;
        void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
            count++;
        }
        foreach (double aspect in new[] { 16d / 9, 21d / 9, 4d / 3 })
        {
            var skills = SkillSettings.Defaults();
            skills[0].Calibration = new(.2, .8, .04, .07, [], aspect, false);
            var dot = SkillHitTesting.Dot(.04 * aspect, .07);
            double x = .2 + dot.X / aspect,
                y = .8 + dot.Y;
            Check(
                SkillHitTesting.HitDot(skills, x, y, aspect) == 0,
                "Rendered dot centre arms the matching skill at each aspect ratio"
            );
            Check(
                SkillHitTesting.HitDot(skills, .22, .835, aspect) is null,
                "Hovering the skill artwork away from its dot cannot arm it"
            );
            Check(
                SkillHitTesting.HitDot(skills, x, y + dot.Radius * 1.05, aspect) is null,
                "Hover target ends at the visible circle, not a rectangular corner"
            );
            Check(
                SkillHitTesting.HitDot(skills, x, y, aspect, true) is null,
                "Dot hit testing rejects a calibration from another capture mode"
            );
        }
        Check(
            SkillHitTesting.HitDot(SkillSettings.Defaults(), .5, .9, 16d / 9) is null,
            "Uncalibrated skills cannot silently arm"
        );
        var savedSettings = new Settings { WholeScreen = true, Monitor = 1 };
        for (int i = 0; i < SkillSlots.Count; i++)
            savedSettings.Skills[i].Calibration = new(
                .2 + i * .08,
                .9,
                .05,
                .05,
                new byte[64 * 64 * 4],
                16d / 9,
                true,
                1
            );
        var loadedSettings = System.Text.Json.JsonSerializer.Deserialize<Settings>(
            System.Text.Json.JsonSerializer.Serialize(savedSettings)
        )!;
        foreach (var pair in loadedSettings.Skills.Select((skill, slot) => (skill, slot)))
        {
            var calibration = pair.skill.Calibration!;
            var dot = SkillHitTesting.Dot(calibration.Width * 16d / 9, calibration.Height);
            Check(
                calibration.ReadyBgra.Length == 64 * 64 * 4
                    && SkillHitTesting.HitDot(
                        loadedSettings.Skills,
                        calibration.X + dot.X / (16d / 9),
                        calibration.Y + dot.Y,
                        16d / 9,
                        true,
                        1
                    ) == pair.slot,
                "Saved calibration pixels and capture metadata restore a matching dot target"
            );
        }
        Check(
            AutoCastAvailability.BlockReason(false, false, false, true, true, true, true, false)
                is null,
            "Settings left open behind the foreground game does not block re-arming"
        );
        Check(
            AutoCastAvailability.BlockReason(false, false, true, true, true, true, true, false)
                is not null,
            "Focused Settings still blocks auto-cast"
        );
        Check(
            AutoCastAvailability.BlockReason(false, true, false, true, true, true, true, false)
                is not null,
            "An open calibration session still blocks auto-cast"
        );
        Check(
            AutoCastAvailability.BlockReason(false, false, false, true, false, true, true, false)
                is not null,
            "Unlocked overlay still blocks auto-cast"
        );
        foreach (double dpiScale in new[] { 1d, 1.25, 1.5, 2d })
        {
            double width = 48 / dpiScale,
                height = 48 / dpiScale;
            var dot = SkillHitTesting.Dot(width, height);
            // PointFromScreen maps the visible physical centre back into these local units.
            double physicalX = 800 + dot.X * dpiScale,
                physicalY = 1000 + dot.Y * dpiScale;
            Check(
                SkillHitTesting.ContainsDot(
                    width,
                    height,
                    (physicalX - 800) / dpiScale,
                    (physicalY - 1000) / dpiScale
                ),
                "Physical cursor at rendered dot centre remains interactive at each monitor DPI"
            );
            Check(
                !SkillHitTesting.ContainsDot(width, height, dot.X - dot.Radius * 1.1, dot.Y),
                "The rendered local dot target excludes neighbouring artwork"
            );
        }
        var clock = new TestClock();
        var control = new AutoCastController(clock);
        var ready = Enumerable
            .Range(1, SkillSlots.Count)
            .Select(i => new SkillCountdown(i.ToString(), 0, 0))
            .ToArray();
        void Dwell(int slot)
        {
            control.UpdateHover(slot, true);
            for (int i = 0; i < 30; i++)
            {
                clock.Advance(.1);
                control.UpdateHover(slot, true);
            }
        }
        void Leave() => control.UpdateHover(null, true);

        Check(control.EnabledSnapshot().All(value => !value), "Auto-cast starts disarmed");
        control.UpdateHover(0, true);
        for (int i = 0; i < 29; i++)
        {
            clock.Advance(.1);
            control.UpdateHover(0, true);
        }
        Check(
            !control.IsEnabled(0) && control.HoverFraction > .9,
            "No toggle before three seconds"
        );
        clock.Advance(.1);
        Check(
            control.UpdateHover(0, true) && control.IsEnabled(0),
            "Three-second dwell arms skill"
        );
        for (int i = 0; i < 60; i++)
        {
            clock.Advance(.1);
            control.UpdateHover(0, true);
        }
        Check(control.IsEnabled(0), "Holding hover toggles once until mouse leaves");
        clock.Advance(2);
        control.UpdateHover(0, true);
        Check(control.IsEnabled(0), "A sampling gap cannot unlatch a completed hover toggle");
        Check(
            control.TakeReadySkill(ready, true) is null,
            "Casting pauses while pointer is over a skill"
        );
        Leave();
        Dwell(0);
        Check(!control.IsEnabled(0), "Leave and dwell again disarms skill");
        Leave();
        control.UpdateHover(0, true);
        for (int i = 0; i < 20; i++)
        {
            clock.Advance(.1);
            control.UpdateHover(0, true);
        }
        control.UpdateHover(1, true);
        clock.Advance(.1);
        control.UpdateHover(1, true);
        Check(
            !control.IsEnabled(0) && !control.IsEnabled(1) && control.HoverFraction < .1,
            "Switching slots resets dwell"
        );
        control.UpdateHover(1, false);
        Check(
            control.HoverSlot is null && control.HoverFraction == 0,
            "Disallowed hover resets dwell"
        );
        control.UpdateHover(0, true);
        clock.Advance(3.1);
        control.UpdateHover(0, true);
        Check(
            !control.IsEnabled(0) && control.HoverFraction == 0,
            "Sampling gaps cannot silently arm a skill"
        );
        Leave();
        Dwell(0);
        Leave();
        var unknown = ready.ToArray();
        unknown[0] = new("1", 0, 0, IsUnknown: true);
        Check(
            control.TakeReadySkill(unknown, true) is null,
            "Unknown readings cannot trigger casts"
        );
        var cooling = ready.ToArray();
        cooling[0] = new("1", 4, .5);
        Check(control.TakeReadySkill(cooling, true) is null, "Cooling skills cannot trigger casts");
        var inferred = ready.ToArray();
        inferred[0] = new("1", 0, 0, IsEstimated: true);
        Check(
            control.TakeReadySkill(inferred, true) is null,
            "An estimated zero is not confirmed ready"
        );
        var invalid = ready.ToArray();
        invalid[0] = new("1", 0, double.NaN);
        Check(
            control.TakeReadySkill(invalid, true) is null,
            "Invalid fractions cannot trigger casts"
        );
        Check(
            control.TakeReadySkill(ready, false) is null,
            "Disallowed context cannot dispatch casts"
        );
        Check(control.TakeReadySkill(ready, true) == 0, "Confirmed-ready armed skill can cast");
        Check(control.TakeReadySkill(ready, true) is null, "Repeat attempts are rate-limited");
        clock.Advance(.3);
        Check(control.TakeReadySkill(ready, true) is null, "Per-skill limiter lasts one second");
        clock.Advance(.7);
        Check(
            control.TakeReadySkill(ready, true) == 0,
            "Persistent ready state can retry after one second"
        );
        Leave();
        Dwell(1);
        Leave();
        Check(control.TakeReadySkill(ready, true) == 1, "Ready-skill selection rotates fairly");
        control.StopAll();
        Check(
            control.EnabledSnapshot().All(value => !value) && control.HoverSlot is null,
            "StopAll clears arming and dwell"
        );
        Check(control.TakeReadySkill(ready, true) is null, "Stopped slots never cast");
        Dwell(SkillSlots.Potion);
        Leave();
        Check(
            control.IsEnabled(SkillSlots.Potion)
                && control.EnabledSnapshot().Take(6).All(value => !value),
            "Potion's three-second dwell arms only the potion"
        );
        var potionUnknown = ready.ToArray();
        potionUnknown[SkillSlots.Potion] = new("D", 0, 0, IsUnknown: true);
        Check(
            control.TakeReadySkill(potionUnknown, true) is null,
            "Unknown potion readiness cannot inject D"
        );
        potionUnknown[SkillSlots.Potion] = new("D", 10, .3);
        Check(
            control.TakeReadySkill(potionUnknown, true) is null,
            "A cooling potion cannot inject D"
        );
        Check(
            control.TakeReadySkill(ready, true) == SkillSlots.Potion,
            "A confirmed-ready armed potion is dispatched"
        );
        control.StopAll();
        Check(!control.IsEnabled(SkillSlots.Potion), "StopAll also disarms the potion");
        var oldSettings = new Settings { Skills = SkillSettings.Defaults().Take(6).ToList() };
        oldSettings.Skills[0].Key = "Q";
        HudLayout.Upgrade(oldSettings);
        Check(
            oldSettings.Skills.Count == SkillSlots.Count
                && oldSettings.Skills[SkillSlots.Potion].Key == "D"
                && oldSettings.Skills[0].Key == "Q",
            "Existing six-slot settings gain potion D without replacing skill keys"
        );
        return count;
    }
}
