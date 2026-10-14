using System.Reflection;
using System.Runtime.InteropServices;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[TestClass]
public sealed class WindowsMediaAudioProbeTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SessionVolumeQueriesPreserveNativeVolumeAndMute(bool initiallyMuted)
    {
        using var native = new NativeSessionVolume(0.37f, initiallyMuted);
        var volumeInterface = typeof(WindowsMediaAudioProbe)
            .GetNestedType("ISimpleAudioVolume", BindingFlags.NonPublic)!;
        var session = Marshal.GetObjectForIUnknown(native.Pointer);
        try
        {
            object[] muteArguments = [false];
            object[] volumeArguments = [0f];
            Assert.AreEqual(0, volumeInterface.GetMethod("GetMute")!.Invoke(session, muteArguments));
            Assert.AreEqual(0, volumeInterface.GetMethod("GetMasterVolume")!.Invoke(session, volumeArguments));

            Assert.AreEqual(0, native.SetterCalls,
                $"Audio observation invoked a native setter; volume is now {native.Volume} instead of 0.37.");
            Assert.AreEqual(0.37f, native.Volume);
            Assert.AreEqual(initiallyMuted, native.Muted);
            Assert.AreEqual(initiallyMuted, (bool)muteArguments[0]);
            Assert.AreEqual(0.37f, (float)volumeArguments[0]);
        }
        finally { Marshal.ReleaseComObject(session); }
    }

    // Independent native layout from Windows SDK AudioClient.h. Never touches real audio sessions.
    private sealed class NativeSessionVolume : IDisposable
    {
        private static readonly Guid VolumeInterface = new("87CE5498-68D6-44E5-9215-6DA47EF883D8");
        private static readonly Guid UnknownInterface = new("00000000-0000-0000-C000-000000000046");
        private readonly Delegate[] _methods;
        private readonly IntPtr _vtable;
        public IntPtr Pointer { get; }
        public float Volume { get; private set; }
        public bool Muted { get; private set; }
        public int SetterCalls { get; private set; }

        public NativeSessionVolume(float volume, bool muted)
        {
            Volume = volume;
            Muted = muted;
            _methods =
            [
                new QueryInterface((IntPtr self, ref Guid iid, out IntPtr result) =>
                {
                    var supported = iid == VolumeInterface || iid == UnknownInterface;
                    result = supported ? self : IntPtr.Zero;
                    return supported ? 0 : unchecked((int)0x80004002);
                }),
                new ReferenceCount(_ => 2),
                new ReferenceCount(_ => 1),
                new SetVolume((_, value, _) => { SetterCalls++; Volume = value; return 0; }),
                new GetVolume((IntPtr _, out float value) => { value = Volume; return 0; }),
                new SetMute((_, value, _) => { SetterCalls++; Muted = value != 0; return 0; }),
                new GetMute((IntPtr _, out int value) => { value = Muted ? 1 : 0; return 0; })
            ];
            _vtable = Marshal.AllocHGlobal(_methods.Length * IntPtr.Size);
            for (var index = 0; index < _methods.Length; index++)
                Marshal.WriteIntPtr(_vtable, index * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(_methods[index]));
            Pointer = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(Pointer, _vtable);
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(Pointer);
            Marshal.FreeHGlobal(_vtable);
            GC.KeepAlive(_methods);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int QueryInterface(IntPtr self, ref Guid iid, out IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint ReferenceCount(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int SetVolume(IntPtr self, float value, IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetVolume(IntPtr self, out float value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int SetMute(IntPtr self, int value, IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetMute(IntPtr self, out int value);
    }
}
