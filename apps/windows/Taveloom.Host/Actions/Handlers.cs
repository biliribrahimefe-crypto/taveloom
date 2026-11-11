using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using Taveloom.Host.Native;
using Taveloom.Host.Workspaces;
using Taveloom.Protocol;

namespace Taveloom.Host.Actions;

public sealed class HotkeyHandler(IInputSender input) : IActionHandler
{
    public ActionType Type => ActionType.Hotkey;
    public string? Validate(ActionSpec s) => KeyParser.TryParse(s.Arg("keys"), out _, out var err) ? null : err;
    public Availability Check(ActionSpec s) => Availability.Yes;

    public Task<ActionOutcome> ExecuteAsync(ActionSpec s, double? value, CancellationToken ct)
    {
        KeyParser.TryParse(s.Arg("keys"), out var combo, out _);
        try { input.Send(combo); return Task.FromResult(ActionOutcome.Success($"Sent {s.Arg("keys")}.")); }
        catch (InputBlockedException ex) { return Task.FromResult(ActionOutcome.Fail(ErrorCodes.ActionFailed, ex.Message)); }
    }
}

/// <summary>Launches locally configured apps, folders and http(s) URLs. Targets come only from the Windows-side configuration.</summary>
public sealed class LaunchHandler(ActionType type) : IActionHandler
{
    public ActionType Type => type;

    // A missing target is "not configured yet" (valid but unavailable, so templates can ship unconfigured); a malformed one is an error.
    public string? Validate(ActionSpec s) => type switch
    {
        ActionType.LaunchApp => s.Arg("path") is not { } p ? null
            : !Path.IsPathFullyQualified(Environment.ExpandEnvironmentVariables(p)) ? "The application path must be absolute."
            : Path.GetExtension(p).ToLowerInvariant() is not (".exe" or ".lnk") ? "Only .exe and .lnk targets are allowed." : null,
        ActionType.LaunchFolder => s.Arg("path") is not { } p ? null
            : Path.IsPathFullyQualified(Environment.ExpandEnvironmentVariables(p)) ? null : "The folder path must be absolute.",
        _ => s.Arg("url") is not { } u ? null
            : Uri.TryCreate(u, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? null : "Enter a valid http:// or https:// URL.",
    };

    public Availability Check(ActionSpec s)
    {
        var key = type == ActionType.LaunchUrl ? "url" : "path";
        if (s.Arg(key) is not { } v) return Availability.No("Not configured. Set it in Workspace Studio.");
        return type switch
        {
            ActionType.LaunchApp => File.Exists(Environment.ExpandEnvironmentVariables(v)) ? Availability.Yes : Availability.No("Application not found at the configured path."),
            ActionType.LaunchFolder => Directory.Exists(Environment.ExpandEnvironmentVariables(v)) ? Availability.Yes : Availability.No("Folder not found."),
            _ => Availability.Yes,
        };
    }

    public Task<ActionOutcome> ExecuteAsync(ActionSpec s, double? value, CancellationToken ct)
    {
        var target = type == ActionType.LaunchUrl ? s.Arg("url")! : Environment.ExpandEnvironmentVariables(s.Arg("path")!);
        var psi = new ProcessStartInfo(target) { UseShellExecute = true };
        if (type == ActionType.LaunchApp && s.Arg("args") is { } a) psi.Arguments = a;
        using var p = Process.Start(psi);
        return Task.FromResult(ActionOutcome.Success("Launched."));
    }
}

public sealed class MediaHandler(IInputSender input, IAudioController audio) : IActionHandler
{
    public const double VolumeStep = 5;
    private static readonly Dictionary<string, ushort> Keys = new()
    {
        ["playpause"] = 0xB3,
        ["next"] = 0xB0,
        ["previous"] = 0xB1,
        ["stop"] = 0xB2,
    };
    private static readonly string[] AudioCommands = ["volumeup", "volumedown", "mute", "volume"];

    public ActionType Type => ActionType.Media;
    public string? Validate(ActionSpec s) =>
        s.Arg("command") is { } c && (Keys.ContainsKey(c) || AudioCommands.Contains(c)) ? null
        : "Media command must be one of: playpause, next, previous, stop, volumeup, volumedown, mute, volume.";

    public Availability Check(ActionSpec s) =>
        Keys.ContainsKey(s.Arg("command")!) || audio.GetPlayback() is not null ? Availability.Yes : Availability.No("No audio output device is available.");

    public Task<ActionOutcome> ExecuteAsync(ActionSpec s, double? value, CancellationToken ct)
    {
        var cmd = s.Arg("command")!;
        if (Keys.TryGetValue(cmd, out var vk))
        {
            try { input.Send(new KeyCombo([], vk)); }
            catch (InputBlockedException ex) { return Task.FromResult(ActionOutcome.Fail(ErrorCodes.ActionFailed, ex.Message)); }
            // Windows gives no feedback on whether a player consumed the media key, so say exactly what happened.
            return Task.FromResult(ActionOutcome.Success($"Media key '{cmd}' sent to Windows."));
        }

        var state = audio.GetPlayback();
        if (state is null) return Task.FromResult(ActionOutcome.Fail(ErrorCodes.ActionUnavailable, "No audio output device is available."));
        bool ok; string msg;
        switch (cmd)
        {
            case "mute": ok = audio.SetPlaybackMute(!state.Value.Muted); msg = state.Value.Muted ? "Unmuted." : "Muted."; break;
            case "volumeup": ok = audio.SetPlaybackVolume(state.Value.VolumePercent + VolumeStep); msg = "Volume increased."; break;
            case "volumedown": ok = audio.SetPlaybackVolume(state.Value.VolumePercent - VolumeStep); msg = "Volume decreased."; break;
            default:
                if (value is not { } v || double.IsNaN(v) || v is < 0 or > 100) return Task.FromResult(ActionOutcome.Fail(ErrorCodes.InvalidMessage, "Volume needs a value between 0 and 100."));
                ok = audio.SetPlaybackVolume(v); msg = $"Volume set to {Math.Round(v)}%."; break;
        }
        return Task.FromResult(ok ? ActionOutcome.Success(msg) : ActionOutcome.Fail(ErrorCodes.ActionFailed, "Windows rejected the audio change."));
    }
}

public sealed class SystemHandler(string screenshotDir) : IActionHandler
{
    private static readonly string[] Commands = ["screenshot", "lock"];
    public ActionType Type => ActionType.System;
    public string? Validate(ActionSpec s) => s.Arg("command") is { } c && Commands.Contains(c) ? null : "System command must be 'screenshot' or 'lock'.";
    public Availability Check(ActionSpec s) => Availability.Yes;

    public Task<ActionOutcome> ExecuteAsync(ActionSpec s, double? value, CancellationToken ct)
    {
        if (s.Arg("command") == "lock")
            return Task.FromResult(Win32.LockWorkStation() ? ActionOutcome.Success("Workstation locked.") : ActionOutcome.Fail(ErrorCodes.ActionFailed, "Windows refused to lock the workstation."));

        Directory.CreateDirectory(screenshotDir);
        int x = Win32.GetSystemMetrics(76), y = Win32.GetSystemMetrics(77), w = Win32.GetSystemMetrics(78), h = Win32.GetSystemMetrics(79);
        if (w <= 0 || h <= 0) return Task.FromResult(ActionOutcome.Fail(ErrorCodes.ActionFailed, "No display available to capture."));
        var path = Path.Combine(screenshotDir, $"Taveloom-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png");
        using (var bmp = new Bitmap(w, h))
        {
            using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(x, y, 0, 0, bmp.Size);
            bmp.Save(path, ImageFormat.Png);
        }
        return Task.FromResult(ActionOutcome.Success($"Screenshot saved to {path}"));
    }
}
