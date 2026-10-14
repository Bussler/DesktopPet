using System.Runtime.InteropServices;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[TestClass]
public sealed class WindowsMediaAudioProbeEnumerationTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FailedSessionCountDoesNotMakeAnEndpointUsable(bool anotherDeviceWorks)
    {
        using var native = new NativeDevices(anotherDeviceWorks);
        var probe = new WindowsMediaAudioProbe(() => Marshal.GetObjectForIUnknown(native.Enumerator.Pointer));
        var observation = probe.Read();
        Assert.AreEqual(anotherDeviceWorks, observation.IsAvailable);
        Assert.AreEqual(anotherDeviceWorks ? 2 : 1, native.SessionCountCalls);
        if (!anotherDeviceWorks)
        {
            var policy = new MediaHeadphonePolicy();
            Assert.IsTrue(policy.Update(new(true, [new("old", "spotify", 0.1f, false, 1, false, 1)]), TimeSpan.Zero, true));
            Assert.IsFalse(policy.Update(observation, TimeSpan.FromMilliseconds(500), true),
                "An enumeration failure must clear stale listening immediately.");
        }
    }

    // Independent native COM fixtures exercise the complete production enumeration path without live audio.
    private sealed class NativeDevices : IDisposable
    {
        private readonly NativeObject[] _objects;
        public NativeObject Enumerator { get; }
        public int SessionCountCalls { get; private set; }

        public NativeDevices(bool anotherDeviceWorks)
        {
            var sessionEnumerator = new NativeObject("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8",
                new Count((IntPtr _, out int count) => { count = 0; return ++SessionCountCalls == 1 ? unchecked((int)0x80004005) : 0; }),
                new Stub(_ => throw new InvalidOperationException("There are no sessions to read.")));
            var manager = new NativeObject("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F",
                new Stub(_ => 0), new Stub(_ => 0),
                new PointerResult((IntPtr _, out IntPtr pointer) => { pointer = sessionEnumerator.Pointer; return 0; }));
            var volumeMethods = Enumerable.Repeat<Delegate>(new Stub(_ => 0), 13).ToArray();
            volumeMethods[6] = new Volume((IntPtr _, out float value) => { value = 1; return 0; });
            volumeMethods[12] = new Count((IntPtr _, out int value) => { value = 0; return 0; });
            var volume = new NativeObject("5CDF2C82-841E-4546-9722-0CF74078229A", volumeMethods);
            var device = new NativeObject("D666063F-1587-4E43-81F1-B948E807363F",
                new Activate((IntPtr _, ref Guid iid, uint context, IntPtr parameters, out IntPtr pointer) =>
                { pointer = iid == new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F") ? manager.Pointer : volume.Pointer; return 0; }),
                new Stub(_ => 0),
                new PointerResult((IntPtr _, out IntPtr pointer) => { pointer = Marshal.StringToCoTaskMemUni("fake-output"); return 0; }));
            var collection = new NativeObject("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E",
                new DeviceCount((IntPtr _, out uint count) => { count = anotherDeviceWorks ? 2u : 1u; return 0; }),
                new Item((IntPtr _, uint index, out IntPtr pointer) => { pointer = device.Pointer; return 0; }));
            Enumerator = new NativeObject("A95664D2-9614-4F35-A746-DE8DB63617E6",
                new EnumEndpoints((IntPtr _, int flow, uint states, out IntPtr pointer) => { pointer = collection.Pointer; return 0; }));
            _objects = [Enumerator, collection, device, volume, manager, sessionEnumerator];
        }

        public void Dispose() { foreach (var instance in _objects) instance.Dispose(); }
    }

    private sealed class NativeObject : IDisposable
    {
        private readonly Delegate[] _methods;
        private readonly IntPtr _vtable;
        public IntPtr Pointer { get; }

        public NativeObject(string interfaceId, params Delegate[] methods)
        {
            var iid = new Guid(interfaceId);
            _methods = [new Query((IntPtr self, ref Guid requested, out IntPtr result) =>
                { var supported = requested == iid || requested == new Guid("00000000-0000-0000-C000-000000000046"); result = supported ? self : IntPtr.Zero; return supported ? 0 : unchecked((int)0x80004002); }),
                new Reference(_ => 2), new Reference(_ => 1), .. methods];
            _vtable = Marshal.AllocHGlobal(_methods.Length * IntPtr.Size);
            for (var index = 0; index < _methods.Length; index++)
                Marshal.WriteIntPtr(_vtable, index * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(_methods[index]));
            Pointer = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(Pointer, _vtable);
        }

        public void Dispose() { Marshal.FreeHGlobal(Pointer); Marshal.FreeHGlobal(_vtable); GC.KeepAlive(_methods); }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Query(IntPtr self, ref Guid iid, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint Reference(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Stub(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Count(IntPtr self, out int count);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DeviceCount(IntPtr self, out uint count);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Volume(IntPtr self, out float value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int PointerResult(IntPtr self, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Item(IntPtr self, uint index, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int EnumEndpoints(IntPtr self, int flow, uint state, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Activate(IntPtr self, ref Guid iid, uint context, IntPtr parameters, out IntPtr result);
}
