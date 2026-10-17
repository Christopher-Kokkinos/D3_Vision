using D3Vision.Core;

internal static class CombatBuffChecks
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
            checks++;
        }
        Check(
            BuffDecoding.IconKey(430674, 3) == ((430674u << 12) | 0x302),
            "Native one-based descriptor IDs map to zero-based packed keys"
        );
        uint?[] icons = [1, 0, 0, 1, 0, 0, 0, 0];
        Check(
            BuffDecoding.ConventionElement(HeroClass.Barbarian, icons) == Element.Fire,
            "Only the live CoE icon determines colour"
        );
        icons[2] = 1;
        Check(
            BuffDecoding.ConventionElement(HeroClass.Barbarian, icons) is null,
            "Ambiguous CoE transition clears glow"
        );
        icons = [1, 1, 0, 0, 0, 0, 0, 0];
        Check(
            BuffDecoding.ConventionElement(HeroClass.Barbarian, icons) is null,
            "Invalid class element cannot illuminate portrait"
        );
        Check(
            BuffDecoding.ConventionElement(HeroClass.Wizard, icons) == Element.Arcane,
            "Wizard Arcane index"
        );
        icons[0] = 0;
        Check(
            BuffDecoding.ConventionElement(HeroClass.Wizard, icons) is null,
            "Unequipped CoE has no inferred cycle"
        );
        Check(
            unchecked((uint)BuffDecoding.ItemId("Unique_Gloves_Set_01_p2")) == 0x7AAE6512,
            "Item name hash matches inspected native game balance row"
        );
        var player = new PlayerBuffState(
            3,
            100,
            HeroClass.Barbarian,
            Element.Fire,
            true,
            true,
            10,
            true,
            false,
            500
        );
        var snapshot = new CombatBuffSnapshot(
            7,
            20,
            1000,
            [player, player with { PlayerSlot = 1, ActorId = 200 }],
            "test",
            3
        );
        Check(
            snapshot.Portrait(0)?.PlayerSlot == 3 && snapshot.Portrait(1)?.PlayerSlot == 1,
            "Local hero is first even in game slot four"
        );
        Check(
            snapshot.Portrait(0, 1)?.ActorId == 200 && snapshot.Portrait(2) is null,
            "Explicit slot mapping and absent party member"
        );
        Check(
            snapshot.IsFresh(1750) && !snapshot.IsFresh(1751) && !snapshot.IsFresh(999),
            "Stale or future readings cannot colour portraits"
        );
        var tracker = new StrickenProcTracker();
        CombatBuffSnapshot Sample(long time, bool proc, int boss = 500) =>
            new(7, 20, time, [player with { StrickenProc = proc, BossActorId = boss }], "");
        tracker.Apply(Sample(1000, true));
        Check(
            tracker.Count(3) == 0,
            "Already active Stricken proc is baseline, not a fabricated stack"
        );
        tracker.Apply(Sample(1100, false));
        tracker.Apply(Sample(1200, true));
        tracker.Apply(Sample(1300, true));
        Check(tracker.Count(3) == 1, "Only rising Stricken proc edges increment estimate");
        tracker.Apply(Sample(1400, false));
        tracker.Apply(Sample(1500, true, 501));
        Check(tracker.Count(3) == 0, "New boss resets estimate");
        tracker.Apply(Sample(2000, true, 501));
        Check(tracker.Count(3) == 0, "Sampling gaps cannot silently retain encounter estimate");
        tracker.Apply(CombatBuffSnapshot.Waiting);
        Check(tracker.Count(3) is null, "Lost sample removes estimate");
        var skills = SkillSettings.Defaults();
        for (int i = 0; i < 6; i++)
            skills[i].Calibration = new(.2 + i * .08, .9, .05, .05, [], 16.0 / 9, false);
        for (int i = 0; i < 6; i++)
            Check(
                SkillHitTesting.Hit(skills, .225 + i * .08, .925, 16.0 / 9) == i,
                "Each calibrated skill targets its own artwork"
            );
        Check(
            SkillHitTesting.Hit(skills, .27, .925, 16.0 / 9) is null,
            "Gap between skills never arms a neighbour"
        );
        Check(
            SkillHitTesting.Hit(skills, .8, .925, 16.0 / 9) is null,
            "Old timer badge area cannot arm skills"
        );
        Check(
            SkillHitTesting.Hit(skills, .225, .925, 4.0 / 3) is null,
            "Changed aspect ratio requires recalibration"
        );
        Check(
            SkillHitTesting.Hit(skills, .225, .925, 16.0 / 9, true) is null,
            "Changed capture mode requires recalibration"
        );
        skills[0].Calibration = skills[0].Calibration! with { WholeScreen = true, Monitor = 1 };
        Check(
            SkillHitTesting.Hit(skills, .225, .925, 16.0 / 9, true, 0) is null,
            "Different monitor cannot reuse a hover target"
        );
        Check(
            SkillHitTesting.Hit(skills, .225, .925, 16.0 / 9, true, 1) == 0,
            "Whole-screen monitor calibration is respected"
        );
        skills[0].Calibration = skills[0].Calibration! with { WholeScreen = null };
        Check(
            SkillHitTesting.Hit(skills, .225, .925, 16.0 / 9) is null,
            "Legacy calibration requires saving capture mode before arming"
        );
        skills[0].Calibration = skills[1].Calibration;
        Check(
            SkillHitTesting.Hit(skills, .305, .925, 16.0 / 9) is null,
            "Overlapping calibration boxes cannot toggle either skill"
        );
        Check(
            SkillHitTesting.Hit(skills, double.NaN, .925, 16.0 / 9) is null,
            "Invalid cursor coordinates cannot toggle skills"
        );
        var settings = new Settings();
        HudLayout.Upgrade(settings);
        var portrait = settings.Widgets.Single(w => w.Kind == WidgetKind.Party1);
        Check(
            portrait.X == 14 && portrait.Width == 185 && settings.Party[0].GamePlayerSlot == -1,
            "Portrait migration installs glow layout and automatic local mapping"
        );
        portrait.X = 77;
        HudLayout.Upgrade(settings);
        Check(portrait.X == 77, "Subsequent launches preserve calibrated portrait alignment");
        return checks;
    }
}
