namespace D3Vision.Core;

public static class InventoryScreenRecognition
{
    public static bool Backpack(string text) =>
        text.Contains("paragon", StringComparison.OrdinalIgnoreCase)
        && text.Contains("details", StringComparison.OrdinalIgnoreCase);

    public static bool Salvage(string text) =>
        text.Contains("salvage results", StringComparison.OrdinalIgnoreCase)
        && (
            text.Contains("anvil", StringComparison.OrdinalIgnoreCase)
            || text.Contains("click an item", StringComparison.OrdinalIgnoreCase)
            || text.Contains("select an item", StringComparison.OrdinalIgnoreCase)
        );
}
