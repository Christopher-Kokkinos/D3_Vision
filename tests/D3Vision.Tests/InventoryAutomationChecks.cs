using D3Vision.Core;
using D3Vision.Infrastructure;

static class InventoryAutomationChecks
{
    public static async Task<int> RunAsync()
    {
        int count = 0;
        void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
            count++;
        }
        const string liveOcr =
            "SALVAGE\nCONVERT ITEMS INTO CRAFTING\nmATERlALS\nClick the Anvil button to begin salvaging.\nSALVAGE RESULTS\nINVENT+RY\nPARAGON\nDETAILS";
        Check(
            InventoryScreenRecognition.Backpack(liveOcr)
                && InventoryScreenRecognition.Salvage(liveOcr),
            "Observed live OCR detects backpack and salvage despite garbled decorative headings"
        );
        Check(
            !InventoryScreenRecognition.Salvage("FORGE WEAPONS\nSalvage\nPARAGON\nDETAILS"),
            "The Forge tab's Salvage tab label cannot activate salvage controls"
        );
        Check(
            !InventoryScreenRecognition.Backpack("PARAGON"),
            "Paragon text alone is not an open backpack"
        );
        Check(
            InventorySelection.NativeRank(9, null) == LootRank.Normal,
            "Validated sparse native rank defaults to normal when its entry is absent"
        );
        Check(
            InventorySelection.NativeRank(9, 1) == LootRank.Ancient,
            "Live Zodiac native rank one decodes as Ancient"
        );
        Check(
            InventorySelection.NativeRank(9, 2) == LootRank.Primal,
            "Native rank two decodes as Primal"
        );
        Check(
            InventorySelection.NativeRank(9, 3) is null
                && InventorySelection.NativeRank(null, null) is null,
            "Unknown rank values and missing item quality stay unknown"
        );
        Check(
            !new Settings().DropAllEnabled && !new Settings().AutoSalvageEnabled,
            "Inventory automation remains disabled in new and older settings"
        );
        var normal = new BackpackItem(1, 0, 0, 9, LootRank.Normal, "P69_Unique_Ring_01");
        foreach (LootRank rank in Enum.GetValues<LootRank>())
        {
            var item = normal with { Rank = rank };
            Check(
                InventorySelection.Eligible(item, InventoryAction.SalvageAll),
                "Salvage all includes known normal, Ancient and Primal equipment"
            );
            Check(
                InventorySelection.Eligible(item, InventoryAction.SalvageNormal)
                    == (rank == LootRank.Normal),
                "Selective salvage preserves Ancient and Primal equipment"
            );
            Check(
                InventorySelection.Eligible(item, InventoryAction.DropAll),
                "Drop includes every known equipment rank"
            );
        }
        foreach (
            var name in new[]
            {
                "x1_Emerald_06",
                "Gem_Legendary_01",
                "HealthPotionLegendary_01",
                "Crafting_Ring_Material",
                "Quest_Sword_01",
                "Unknown_01",
                "",
            }
        )
        foreach (var action in new[] { InventoryAction.SalvageAll, InventoryAction.SalvageNormal })
            Check(
                !InventorySelection.Eligible(normal with { DefinitionName = name }, action),
                "Salvage excludes gems, materials, potions, quest and unknown definitions"
            );
        foreach (
            var item in new[]
            {
                normal with
                {
                    Rank = null,
                },
                normal with
                {
                    Quality = null,
                },
                normal with
                {
                    Quality = 10,
                },
            }
        )
            Check(
                !InventorySelection.Eligible(item, InventoryAction.SalvageNormal),
                "Uncertain item metadata cannot enter selective salvage"
            );
        foreach (
            var item in new[]
            {
                normal with
                {
                    Column = -1,
                },
                normal with
                {
                    Column = 10,
                },
                normal with
                {
                    Row = 6,
                },
            }
        )
        foreach (var action in Enum.GetValues<InventoryAction>())
            Check(
                !InventorySelection.Eligible(item, action),
                "Invalid backpack coordinates never produce input"
            );
        Check(
            InventorySelection.Eligible(
                normal with
                {
                    Rank = null,
                    Quality = null,
                    DefinitionName = "x1_Ruby_07",
                },
                InventoryAction.DropAll
            ),
            "Drop all includes gems and does not need salvage metadata"
        );
        var clock = new TestClock();
        var hover = new InventoryHoverTrigger(clock);
        Check(
            hover.Update(InventoryAction.DropAll) is null,
            "Entering DROP does not immediately activate"
        );
        for (int i = 0; i < 29; i++)
        {
            clock.Advance(.1);
            Check(hover.Update(InventoryAction.DropAll) is null, "No drop before three seconds");
        }
        Check(hover.Progress > .9, "Countdown tracks the dwell fraction");
        clock.Advance(.1);
        Check(
            hover.Update(InventoryAction.DropAll) == InventoryAction.DropAll,
            "Continuous three-second hover activates"
        );
        for (int i = 0; i < 50; i++)
        {
            clock.Advance(.1);
            Check(
                hover.Update(InventoryAction.DropAll) is null,
                "Holding hover cannot repeat drop"
            );
        }
        hover.Update(null);
        hover.Update(InventoryAction.SalvageAll);
        clock.Advance(4);
        Check(
            hover.Update(InventoryAction.SalvageAll) is null && hover.Progress == 0,
            "A stale hover cannot activate salvage"
        );
        clock.Advance(.2);
        hover.Update(InventoryAction.SalvageAll);
        Check(
            hover.Update(InventoryAction.SalvageNormal) is null && hover.Progress == 0,
            "Changing options resets the dwell"
        );
        hover.Reset();
        Check(
            hover.Hovered is null && hover.Progress == 0,
            "Escape/reset clears pending activation"
        );
        // Exercise refusal paths only: these tests never inject input into the live game.
        var input = new GameInventoryInput();
        bool refused = false;
        try
        {
            await input.ClickAsync(0, 0, () => false, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            refused = true;
        }
        Check(refused, "Disallowed operation refuses to click");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        bool stopped = false;
        try
        {
            await input.DragAsync(0, 0, 1, 1, () => true, cancelled.Token);
        }
        catch (OperationCanceledException)
        {
            stopped = true;
        }
        Check(stopped, "Cancelled drop refuses to move or press the mouse");
        return count;
    }
}
