namespace D3Vision.Core;

public sealed class InventoryHoverTrigger(TimeProvider? timeProvider = null)
{
    readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    InventoryAction? hovered;
    long started;
    long? lastSample;
    bool latched;
    public double Progress { get; private set; }
    public InventoryAction? Hovered => hovered;

    public InventoryAction? Update(InventoryAction? action)
    {
        long now = clock.GetTimestamp();
        if (action is null)
        {
            Reset();
            return null;
        }
        if (
            hovered != action
            || lastSample is null
            || (!latched && clock.GetElapsedTime(lastSample.Value, now).TotalSeconds > .5)
        )
        {
            hovered = action;
            started = now;
            latched = false;
            Progress = 0;
        }
        lastSample = now;
        if (latched)
            return null;
        Progress = Math.Clamp(clock.GetElapsedTime(started, now).TotalSeconds / 3, 0, 1);
        if (Progress < 1)
            return null;
        latched = true;
        Progress = 0;
        return action;
    }

    public void Reset()
    {
        hovered = null;
        lastSample = null;
        latched = false;
        Progress = 0;
    }
}
