using System.ComponentModel;
using System.Runtime.InteropServices;
using Taveloom.Host.Native;

namespace Taveloom.Host.Actions;

public interface IInputSender
{
    /// <summary>Presses modifiers, taps the key, releases. Throws <see cref="InputBlockedException"/> if Windows rejects the input.</summary>
    void Send(KeyCombo combo);
}

public sealed class InputBlockedException(string message) : Exception(message);

public sealed class SendInputSender : IInputSender
{
    public void Send(KeyCombo combo)
    {
        var inputs = new List<Win32.INPUT>();
        foreach (var m in combo.Modifiers) inputs.Add(Key(m, false));
        inputs.Add(Key(combo.Key, false));
        inputs.Add(Key(combo.Key, true));
        foreach (var m in combo.Modifiers.Reverse()) inputs.Add(Key(m, true));

        var sent = Win32.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Win32.INPUT>());
        if (sent != inputs.Count)
            throw new InputBlockedException(
                $"Windows blocked the input ({new Win32Exception(Marshal.GetLastWin32Error()).Message}). " +
                "This usually means an elevated (administrator) window is focused.");
    }

    private static Win32.INPUT Key(ushort vk, bool up) => new()
    {
        type = Win32.INPUT_KEYBOARD,
        u = new Win32.InputUnion
        {
            ki = new Win32.KEYBDINPUT
            {
                wVk = vk,
                dwFlags = (KeyParser.IsExtended(vk) || vk is >= 0xAD and <= 0xB3 ? Win32.KEYEVENTF_EXTENDEDKEY : 0) | (up ? Win32.KEYEVENTF_KEYUP : 0),
            },
        },
    };
}
