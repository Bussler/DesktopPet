using System;
using System.Runtime.InteropServices;

namespace DesktopPet.Windows;

// Windows exposes no unrestricted public DND setter. Keep this undocumented COM
// boundary isolated; failures must be reported rather than presented as protection.
internal static class WindowsFocusNotifications
{
    internal static string Read() => WithSettings(settings =>
    {
        IntPtr profile = IntPtr.Zero;
        try
        {
            Marshal.ThrowExceptionForHR(settings.GetUserSelectedProfile(out profile));
            return Marshal.PtrToStringUni(profile) ?? throw new InvalidOperationException("Windows returned no notification profile.");
        }
        finally { Marshal.FreeCoTaskMem(profile); }
    });

    internal static void Write(string profile) => WithSettings(settings =>
    {
        Marshal.ThrowExceptionForHR(settings.SetUserSelectedProfile(profile));
        return true;
    });

    private static T WithSettings<T>(Func<IQuietHoursSettings, T> action)
    {
        var clsid = new Guid("f53321fa-34f8-4b7f-b9a3-361877cb94cf");
        var iid = typeof(IQuietHoursSettings).GUID;
        Marshal.ThrowExceptionForHR(CoCreateInstance(ref clsid, IntPtr.Zero, 4 /* CLSCTX_LOCAL_SERVER */, ref iid, out var settings));
        try { return action(settings); }
        finally { Marshal.ReleaseComObject(settings); }
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint context, ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IQuietHoursSettings settings);

    [ComImport, Guid("6bff4732-81ec-4ffb-ae67-b6c1bc29631f"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IQuietHoursSettings
    {
        [PreserveSig] int GetUserSelectedProfile(out IntPtr profile);
        [PreserveSig] int SetUserSelectedProfile([MarshalAs(UnmanagedType.LPWStr)] string profile);
    }
}
