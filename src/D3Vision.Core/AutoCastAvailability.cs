namespace D3Vision.Core;

public static class AutoCastAvailability
{
    public static string? BlockReason(
        bool exiting,
        bool calibrating,
        bool settingsActive,
        bool overlayVisible,
        bool clickThrough,
        bool visualRead,
        bool widgetEnabled,
        bool otherAutomation
    ) =>
        exiting ? "D3Vision is closing."
        : calibrating ? "Finish or close skill calibration."
        : settingsActive ? "Return to Diablo III from Settings."
        : !overlayVisible ? "Press F10 to show the overlay."
        : !clickThrough ? "Press F11 to lock the overlay."
        : !visualRead ? "Select VisualRead in Settings."
        : !widgetEnabled ? "Enable the Cooldowns widget in Settings."
        : otherAutomation ? "An inventory or vendor action is active."
        : null;
}
