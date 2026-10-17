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
        var clock = new TestClock();
        var control = new AutoCastController(clock);
        var ready = Enumerable
            .Range(1, 6)
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
        return count;
    }
}
