using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DesktopPet.Windows;

internal interface IMediaAudioProbe
{
    MediaAudioObservation Read();
}

internal sealed class WindowsMediaAudioProbe(Func<object>? createEnumerator = null) : IMediaAudioProbe
{
    private static readonly Guid EnumeratorClass = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid SessionManagerInterface = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
    private static readonly Guid EndpointVolumeInterface = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    private const uint ActiveDevices = 1;
    private const uint ClsctxAll = 23;

    public MediaAudioObservation Read()
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDeviceCollection? devices = null;
        try
        {
            var classType = Type.GetTypeFromCLSID(EnumeratorClass, throwOnError: true)!;
            enumerator = (IMMDeviceEnumerator)(createEnumerator?.Invoke() ?? Activator.CreateInstance(classType)!);
            // Apps can be routed away from the default, so inspect each active render endpoint.
            Check(enumerator.EnumAudioEndpoints(0, ActiveDevices, out devices));
            if (devices is null) return MediaAudioObservation.Unavailable;
            Check(devices.GetCount(out var deviceCount));

            var samples = new List<MediaAudioSample>();
            var endpoints = new List<MediaAudioEndpoint>();
            var usableDevices = deviceCount == 0 ? 1 : 0;
            for (uint deviceIndex = 0; deviceIndex < deviceCount; deviceIndex++)
            {
                IMMDevice? device = null;
                IAudioSessionManager2? manager = null;
                IAudioSessionEnumerator? sessionEnumerator = null;
                IAudioEndpointVolume? endpointVolume = null;
                try
                {
                    Check(devices.Item(deviceIndex, out var devicePointer));
                    if (devicePointer == IntPtr.Zero) continue;
                    try { device = (IMMDevice)Marshal.GetObjectForIUnknown(devicePointer); }
                    finally { Marshal.Release(devicePointer); }
                    Check(device.GetId(out var endpointIdPointer));
                    string endpointId;
                    try { endpointId = Marshal.PtrToStringUni(endpointIdPointer) ?? deviceIndex.ToString(); }
                    finally { Marshal.FreeCoTaskMem(endpointIdPointer); }
                    var sessionManagerInterface = SessionManagerInterface;
                    Check(device.Activate(ref sessionManagerInterface, ClsctxAll, IntPtr.Zero, out var managerPointer));
                    try { manager = (IAudioSessionManager2)Marshal.GetObjectForIUnknown(managerPointer); }
                    finally { Marshal.Release(managerPointer); }

                    var endpointVolumeInterface = EndpointVolumeInterface;
                    Check(device.Activate(ref endpointVolumeInterface, ClsctxAll, IntPtr.Zero, out var volumePointer));
                    try { endpointVolume = (IAudioEndpointVolume)Marshal.GetObjectForIUnknown(volumePointer); }
                    finally { Marshal.Release(volumePointer); }
                    Check(endpointVolume.GetMute(out var endpointMuted));
                    Check(endpointVolume.GetMasterVolumeLevelScalar(out var endpointScalar));
                    endpoints.Add(new MediaAudioEndpoint(endpointId, endpointMuted, endpointScalar));

                    Check(manager.GetSessionEnumerator(out sessionEnumerator));
                    if (sessionEnumerator is null) continue;
                    Check(sessionEnumerator.GetCount(out var sessionCount));
                    usableDevices++;
                    for (int sessionIndex = 0; sessionIndex < sessionCount; sessionIndex++)
                    {
                        IAudioSessionControl? sessionControl = null;
                        try
                        {
                            Check(sessionEnumerator.GetSession(sessionIndex, out sessionControl));
                            if (sessionControl is null) continue;
                            var control = (IAudioSessionControl2)sessionControl;
                            Check(control.GetProcessId(out var processId));
                            // Inspect every owning process session so browser audio subprocesses qualify without media metadata or focus checks.
                            var processName = GetProcessName(processId);
                            if (processName is null || !MediaHeadphonePolicy.IsEligible(processName)) continue;

                            Check(control.GetSessionInstanceIdentifier(out var sessionIdPointer));
                            string sessionId;
                            try { sessionId = Marshal.PtrToStringUni(sessionIdPointer) ?? processId.ToString(); }
                            finally { Marshal.FreeCoTaskMem(sessionIdPointer); }

                            Check(((ISimpleAudioVolume)control).GetMute(out var sessionMuted));
                            Check(((ISimpleAudioVolume)control).GetMasterVolume(out var sessionScalar));
                            Check(((IAudioMeterInformation)control).GetPeakValue(out var peak));
                            samples.Add(new MediaAudioSample(
                                $"{endpointId}:{sessionId}", processName, peak, sessionMuted, sessionScalar,
                                endpointMuted, endpointScalar, endpointId));
                        }
                        catch (Exception exception) when (exception is COMException or InvalidCastException or ArgumentException)
                        {
                            // A session can disappear between enumeration and sampling.
                        }
                        finally { Release(sessionControl); }
                    }
                }
                catch (Exception exception) when (exception is COMException or InvalidCastException or InvalidOperationException or ArgumentException)
                {
                    // Continue observing other active devices if one is changing or unavailable.
                }
                finally
                {
                    Release(endpointVolume);
                    Release(sessionEnumerator);
                    Release(manager);
                    Release(device);
                }
            }

            return usableDevices > 0
                ? new MediaAudioObservation(true, samples, endpoints)
                : MediaAudioObservation.Unavailable;
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException or InvalidOperationException or ArgumentException or TypeLoadException)
        {
            return MediaAudioObservation.Unavailable;
        }
        finally
        {
            Release(devices);
            Release(enumerator);
        }
    }

    private static string? GetProcessName(uint processId)
    {
        try { return Process.GetProcessById((int)processId).ProcessName; }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static void Check(int result) => Marshal.ThrowExceptionForHR(result);

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr callback);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr callback);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IntPtr device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid interfaceId, uint classContext, IntPtr activationParameters, out IntPtr instance);
        [PreserveSig] int OpenPropertyStore(int access, out IntPtr properties);
        [PreserveSig] int GetId(out IntPtr id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr sessionId, uint flags, out IntPtr control);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr sessionId, uint flags, out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnumerator);
        [PreserveSig] int RegisterSessionNotification(IntPtr notification);
        [PreserveSig] int UnregisterSessionNotification(IntPtr notification);
        [PreserveSig] int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, IntPtr notification);
        [PreserveSig] int UnregisterDuckNotification(IntPtr notification);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, out IAudioSessionControl session);
    }

    [ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName(out IntPtr name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid eventContext);
        [PreserveSig] int GetIconPath(out IntPtr path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid eventContext);
        [PreserveSig] int GetGroupingParam(out Guid groupingId);
        [PreserveSig] int SetGroupingParam(ref Guid groupingId, ref Guid eventContext);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr notification);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr notification);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName(out IntPtr name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid eventContext);
        [PreserveSig] int GetIconPath(out IntPtr path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid eventContext);
        [PreserveSig] int GetGroupingParam(out Guid groupingId);
        [PreserveSig] int SetGroupingParam(ref Guid groupingId, ref Guid eventContext);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr notification);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr notification);
        [PreserveSig] int GetSessionIdentifier(out IntPtr identifier);
        [PreserveSig] int GetSessionInstanceIdentifier(out IntPtr identifier);
        [PreserveSig] int GetProcessId(out uint processId);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        // COM dispatch uses native declaration order, including these unused setters.
        [PreserveSig] int SetMasterVolume(float volume, ref Guid eventContext);
        [PreserveSig] int GetMasterVolume(out float volume);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid eventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
    }

    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioMeterInformation
    {
        [PreserveSig] int GetPeakValue(out float peak);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr callback);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr callback);
        [PreserveSig] int GetChannelCount(out uint channelCount);
        [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid eventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, ref Guid eventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid eventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
    }
}
