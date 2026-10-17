namespace D3Vision.Core;

/// <summary>Continuous three-second dwell, one purchase in flight, and no-progress stop.</summary>
public enum KadalaActivationMode
{
    Hover,
    SingleClick,
}

public sealed class KadalaHoverBuyer(TimeProvider? timeProvider = null)
{
    readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    int? target;
    KadalaActivationMode activationMode;
    long started,
        lastSample,
        sent;
    int inventoryCount;
    bool waiting,
        latched;
    public bool IsBuying { get; private set; }
    public double HoverFraction { get; private set; }
    public string Status { get; private set; } = "Hover over a Kadala offer for 3 seconds.";

    public bool Update(
        int? offer,
        bool allowed,
        int items,
        KadalaActivationMode mode = KadalaActivationMode.Hover,
        bool clicked = false
    )
    {
        long now = clock.GetTimestamp();
        if (mode != activationMode)
        {
            Reset();
            activationMode = mode;
        }
        if (!Enum.IsDefined(mode))
        {
            Reset();
            return false;
        }
        if (!allowed || offer is null || items < 0)
        {
            Reset();
            return false;
        }
        if (
            target != offer
            || (!latched && clock.GetElapsedTime(lastSample, now).TotalSeconds > .6)
        )
        {
            Reset();
            target = offer;
            started = now;
        }
        lastSample = now;
        if (latched)
        {
            if (mode != KadalaActivationMode.SingleClick || !clicked)
                return false;
            Reset();
            target = offer;
            started = lastSample = now;
        }
        if (!IsBuying)
        {
            if (mode == KadalaActivationMode.SingleClick)
            {
                HoverFraction = 0;
                Status = "Kadala: left-click this offer to multi-buy.";
                if (!clicked)
                    return false;
            }
            else
            {
                HoverFraction = Math.Clamp(
                    clock.GetElapsedTime(started, now).TotalSeconds / 3,
                    0,
                    1
                );
                Status = $"Kadala: hold hover {(3 * (1 - HoverFraction)):0.0}s";
                if (HoverFraction < 1)
                    return false;
            }
            IsBuying = true;
        }
        if (waiting)
        {
            if (items > inventoryCount)
            {
                waiting = false;
                Status = "Kadala: buying hovered offer.";
            }
            else if (clock.GetElapsedTime(sent, now).TotalSeconds >= 2)
            {
                Stop(
                    "Kadala stopped: no item added (inventory full, insufficient shards, or unavailable offer). "
                        + (
                            mode == KadalaActivationMode.SingleClick
                                ? "Left-click again to rearm."
                                : "Move off the offer to rearm."
                        )
                );
                return false;
            }
            else
                return false;
        }
        if (sent != 0 && clock.GetElapsedTime(sent, now).TotalSeconds < .35)
            return false;
        inventoryCount = items;
        sent = now;
        waiting = true;
        HoverFraction = 0;
        Status = "Kadala: waiting for purchased item.";
        return true;
    }

    public void Stop(string message)
    {
        IsBuying = false;
        waiting = false;
        latched = true;
        HoverFraction = 0;
        Status = message;
    }

    public void Reset()
    {
        target = null;
        IsBuying = false;
        waiting = false;
        latched = false;
        HoverFraction = 0;
        sent = 0;
        Status = "Hover over a Kadala offer for 3 seconds.";
    }
}
