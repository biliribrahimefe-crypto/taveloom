using System.Runtime.InteropServices;

namespace Taveloom.Host.Native;

public readonly record struct AudioState(double VolumePercent, bool Muted);

/// <summary>Master volume / mute of the default Windows audio endpoints. Methods return false/null when no device is available.</summary>
public interface IAudioController
{
    AudioState? GetPlayback();
    bool SetPlaybackVolume(double percent);
    bool SetPlaybackMute(bool muted);
    bool? GetMicrophoneMuted();
}

/// <summary>Core Audio (MMDevice / IAudioEndpointVolume) implementation. A fresh enumerator is used per call so default-device changes are honoured.</summary>
public sealed class CoreAudioController : IAudioController
{
    private static readonly Guid AudioEndpointVolumeIid = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    private const int ERender = 0, ECapture = 1, EConsole = 0, ClsCtxAll = 23;

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] private class MMDeviceEnumeratorCom { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, int mask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr p);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr p);
        [PreserveSig] int GetChannelCount(out uint n);
        [PreserveSig] int SetMasterVolumeLevel(float db, ref Guid ctx);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
        [PreserveSig] int GetMasterVolumeLevel(out float db);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint ch, float db, ref Guid ctx);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint ch, float level, ref Guid ctx);
        [PreserveSig] int GetChannelVolumeLevel(uint ch, out float db);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint ch, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    private static T? With<T>(int flow, Func<IAudioEndpointVolume, T> body)
    {
        object? enumObj = null, devObj = null, volObj = null;
        try
        {
            enumObj = new MMDeviceEnumeratorCom();
            var en = (IMMDeviceEnumerator)enumObj;
            if (en.GetDefaultAudioEndpoint(flow, EConsole, out var dev) != 0) return default;
            devObj = dev;
            var iid = AudioEndpointVolumeIid;
            if (dev.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out volObj) != 0) return default;
            return body((IAudioEndpointVolume)volObj);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException) { return default; }
        finally
        {
            foreach (var o in new[] { volObj, devObj, enumObj }) if (o is not null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o);
        }
    }

    public AudioState? GetPlayback() => With(ERender, v =>
    {
        if (v.GetMasterVolumeLevelScalar(out var level) != 0 || v.GetMute(out var muted) != 0) return (AudioState?)null;
        return new AudioState(Math.Round(level * 100), muted);
    });

    public bool SetPlaybackVolume(double percent) => With(ERender, v =>
    {
        var ctx = Guid.Empty;
        return v.SetMasterVolumeLevelScalar((float)Math.Clamp(percent, 0, 100) / 100f, ref ctx) == 0;
    });

    public bool SetPlaybackMute(bool muted) => With(ERender, v => { var ctx = Guid.Empty; return v.SetMute(muted, ref ctx) == 0; });

    public bool? GetMicrophoneMuted() => With(ECapture, v => v.GetMute(out var m) == 0 ? (bool?)m : null);
}
