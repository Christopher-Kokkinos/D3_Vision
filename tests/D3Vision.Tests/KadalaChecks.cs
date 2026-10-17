using D3Vision.Core;
using D3Vision.Infrastructure;
using D3Vision.Services;

internal static class KadalaChecks
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool result, string message)
        {
            if (!result)
                throw new Exception(message);
            checks++;
        }
        var clock = new Clock();
        var buyer = new KadalaHoverBuyer(clock);
        bool Dwell(int offer, int count = 5)
        {
            bool clicked = false;
            for (int i = 0; i < 31; i++)
            {
                clicked |= buyer.Update(offer, true, count);
                clock.Advance(.1);
            }
            return clicked;
        }
        Check(!buyer.Update(0, true, 5), "Kadala cannot buy immediately on hover");
        Check(Dwell(0), "A continuous three-second hover starts buying");
        Check(!buyer.Update(0, true, 5), "No repeated purchase before inventory confirmation");
        clock.Advance(.1);
        Check(!buyer.Update(0, true, 6), "Purchase confirmation still obeys rate limit");
        clock.Advance(.2);
        Check(buyer.Update(0, true, 6), "Confirmed item permits the next purchase");
        for (int i = 0; i < 21; i++)
        {
            clock.Advance(.1);
            buyer.Update(0, true, 6);
        }
        Check(
            !buyer.IsBuying && buyer.Status.Contains("no item added"),
            "Inventory-full or out-of-shards rejection stops purchases"
        );
        clock.Advance(1);
        Check(!buyer.Update(0, true, 6), "Stopped purchase remains latched across sampling pauses");
        Check(!Dwell(0, 6), "Holding hover after completion cannot rearm");
        buyer.Update(null, true, 6);
        Check(Dwell(0, 6), "Leaving and re-entering the offer permits another deliberate dwell");
        Check(
            !buyer.Update(null, true, 6) && !buyer.IsBuying,
            "Leaving the hovered offer stops immediately"
        );
        Check(!buyer.Update(0, false, 6), "Focus/settings/disabled gate prevents input");
        buyer.Update(0, true, 6);
        clock.Advance(2.9);
        Check(!buyer.Update(0, true, 6), "Long sampling gap cannot complete an old hover");
        buyer.Reset();
        buyer.Update(0, true, 6);
        clock.Advance(.1);
        Check(!buyer.Update(1, true, 6), "Changing offers restarts dwell");
        buyer.Stop("Input failed");
        Check(!buyer.Update(1, true, 6), "Failed dispatch latches stopped state");
        Check(
            KadalaScreen.Offer(70, 200, 1) == 0 && KadalaScreen.Offer(300, 200, 1) == 1,
            "Shop columns resolve correctly"
        );
        Check(
            KadalaScreen.Offer(70 * 1.5, 200 * 1.5, 1.5) == 0,
            "Shop hit testing follows client height scaling"
        );
        Check(
            KadalaScreen.Offer(520, 200, 1) is null && KadalaScreen.Offer(100, 840, 1) is null,
            "Tabs and shard footer cannot trigger purchases"
        );
        var frame = new CaptureFrame(new byte[20 * 20 * 4], 20, 20);
        var crop = KadalaScreen.Crop(frame, 2, 3, 4, 5, 1);
        Check(
            crop.Width == 4 && crop.Height == 5,
            "Cropped detection buffers have independent ownership"
        );
        Check(KadalaScreen.Matches(crop, crop), "Stable shop signature matches");
        var changed = new CaptureFrame(Enumerable.Repeat((byte)255, 4 * 5 * 4).ToArray(), 4, 5);
        Check(
            !KadalaScreen.Matches(crop, changed),
            "Closing/changing the vendor invalidates the shop signature"
        );
        buyer.Reset();
        Check(
            !buyer.Update(0, true, 5, KadalaActivationMode.SingleClick),
            "Click mode never buys merely on hover"
        );
        bool unintended = false;
        for (int i = 0; i < 40; i++)
        {
            clock.Advance(.1);
            unintended |= buyer.Update(0, true, 5, KadalaActivationMode.SingleClick);
        }
        Check(!unintended, "Click mode cannot fall back to dwell");
        Check(
            buyer.Update(0, true, 5, KadalaActivationMode.SingleClick, true),
            "One physical click starts click-mode multi-buy"
        );
        clock.Advance(.4);
        Check(
            buyer.Update(0, true, 6, KadalaActivationMode.SingleClick),
            "Click-mode purchases continue after inventory confirmation without more clicks"
        );
        for (int i = 0; i < 21; i++)
        {
            clock.Advance(.1);
            buyer.Update(0, true, 6, KadalaActivationMode.SingleClick);
        }
        Check(
            !buyer.Update(0, true, 6, KadalaActivationMode.SingleClick) && !buyer.IsBuying,
            "Click mode stops on a rejected/full-inventory purchase"
        );
        Check(
            buyer.Update(0, true, 6, KadalaActivationMode.SingleClick, true),
            "A fresh deliberate click can rearm a completed batch"
        );
        Check(
            !buyer.Update(1, true, 6, KadalaActivationMode.SingleClick),
            "Changing offers requires a new click in click mode"
        );
        Check(
            !buyer.Update(1, false, 6, KadalaActivationMode.SingleClick, true),
            "A click in a disallowed context cannot start a purchase"
        );
        Check(
            !buyer.Update(1, true, 6, KadalaActivationMode.Hover, true),
            "Switching to hover mode still requires the full dwell"
        );
        Check(
            KeyboardHotkeys.IsPhysicalLeftRelease(0x202, 0),
            "A physical left-button release is a click trigger"
        );
        Check(
            !KeyboardHotkeys.IsPhysicalLeftRelease(0x202, 1)
                && !KeyboardHotkeys.IsPhysicalLeftRelease(0x202, 2),
            "Injected clicks cannot trigger another Kadala batch"
        );
        Check(
            !KeyboardHotkeys.IsPhysicalLeftRelease(0x201, 0)
                && !KeyboardHotkeys.IsPhysicalLeftRelease(0x205, 0),
            "Mouse-down and right-button events are not click-mode triggers"
        );
        var restored = System.Text.Json.JsonSerializer.Deserialize<Settings>(
            System.Text.Json.JsonSerializer.Serialize(
                new Settings
                {
                    KadalaHoverBuy = true,
                    KadalaMode = KadalaActivationMode.SingleClick,
                }
            )
        )!;
        Check(
            restored.KadalaHoverBuy && restored.KadalaMode == KadalaActivationMode.SingleClick,
            "Settings preserve the selected multi-buy activation mode"
        );
        Check(
            System.Text.Json.JsonSerializer.Deserialize<Settings>("{}")!.KadalaMode
                == KadalaActivationMode.Hover,
            "Existing settings keep the original hover behavior"
        );
        return checks;
    }

    sealed class Clock : TimeProvider
    {
        long ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => ticks;

        public void Advance(double seconds) => ticks += (long)(seconds * TimeSpan.TicksPerSecond);
    }
}
