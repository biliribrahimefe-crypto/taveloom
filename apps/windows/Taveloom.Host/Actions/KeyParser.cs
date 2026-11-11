namespace Taveloom.Host.Actions;

public sealed record KeyCombo(IReadOnlyList<ushort> Modifiers, ushort Key);

/// <summary>Parses and validates textual hotkeys such as "ctrl+shift+s" into virtual-key codes.</summary>
public static class KeyParser
{
    private static readonly Dictionary<string, ushort> Modifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = 0x11,
        ["control"] = 0x11,
        ["shift"] = 0x10,
        ["alt"] = 0x12,
        ["win"] = 0x5B,
    };

    private static readonly Dictionary<string, ushort> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["enter"] = 0x0D,
        ["esc"] = 0x1B,
        ["escape"] = 0x1B,
        ["tab"] = 0x09,
        ["space"] = 0x20,
        ["backspace"] = 0x08,
        ["delete"] = 0x2E,
        ["insert"] = 0x2D,
        ["home"] = 0x24,
        ["end"] = 0x23,
        ["pageup"] = 0x21,
        ["pagedown"] = 0x22,
        ["left"] = 0x25,
        ["up"] = 0x26,
        ["right"] = 0x27,
        ["down"] = 0x28,
        ["printscreen"] = 0x2C,
        ["plus"] = 0xBB,
        ["minus"] = 0xBD,
        ["comma"] = 0xBC,
        ["period"] = 0xBE,
        ["slash"] = 0xBF,
        ["semicolon"] = 0xBA,
        ["quote"] = 0xDE,
        ["lbracket"] = 0xDB,
        ["rbracket"] = 0xDD,
        ["backslash"] = 0xDC,
        ["backtick"] = 0xC0,
    };

    public static bool IsExtended(ushort vk) => vk is (>= 0x21 and <= 0x28) or 0x2D or 0x2E or 0x5B or 0x2C;

    public static bool TryParse(string? text, out KeyCombo combo, out string error)
    {
        combo = null!;
        if (string.IsNullOrWhiteSpace(text)) { error = "No keys specified."; return false; }
        var mods = new List<ushort>();
        ushort? key = null;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries))
        {
            if (raw.Length == 0) { error = "Empty key segment. Use 'plus' for the + key."; return false; }
            if (Modifiers.TryGetValue(raw, out var m))
            {
                if (mods.Contains(m)) { error = $"Modifier '{raw}' repeated."; return false; }
                mods.Add(m);
            }
            else if (TryKey(raw, out var k))
            {
                if (key is not null) { error = "Only one non-modifier key is allowed."; return false; }
                key = k;
            }
            else { error = $"Unknown key '{raw}'."; return false; }
        }
        if (key is null) { error = "A hotkey needs a non-modifier key."; return false; }
        combo = new KeyCombo(mods, key.Value); error = ""; return true;
    }

    private static bool TryKey(string s, out ushort vk)
    {
        vk = 0;
        if (Named.TryGetValue(s, out vk)) return true;
        if (s.Length == 1 && char.IsAsciiLetter(s[0])) { vk = (ushort)char.ToUpperInvariant(s[0]); return true; }
        if (s.Length == 1 && char.IsAsciiDigit(s[0])) { vk = (ushort)s[0]; return true; }
        if ((s[0] is 'f' or 'F') && int.TryParse(s.AsSpan(1), out var n) && n is >= 1 and <= 24) { vk = (ushort)(0x6F + n); return true; }
        return false;
    }
}
