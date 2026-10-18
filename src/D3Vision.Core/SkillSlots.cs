namespace D3Vision.Core;

public static class SkillSlots
{
    public const int Count = 7;
    public const int Potion = 6;

    public static bool IsKeyboard(int slot) => slot is >= 0 and < 4 or Potion;

    public static string Label(int slot, string key) =>
        slot == Potion ? $"Potion - {key}" : $"Slot {slot + 1} - {key}";
}
