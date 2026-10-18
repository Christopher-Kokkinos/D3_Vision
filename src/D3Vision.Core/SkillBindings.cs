namespace D3Vision.Core;

public static class SkillBindings
{
    public static bool TryKey(string? text, out ushort key)
    {
        string value = text?.Trim().ToUpperInvariant() ?? "";
        key = value.Length == 1 ? value[0] : (ushort)0;
        return key is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A;
    }
}
