using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace DesktopPet.Windows.Tests;

[TestClass]
public sealed class DpiAwarenessTests
{
    [TestMethod]
    public void ExecutableDeclaresPerMonitorDpiBeforeWpfCreatesWindows()
    {
        // Read the shipped apphost resource without starting Hood or changing process DPI.
        var module = LoadLibraryEx(Path.Combine(AppContext.BaseDirectory, "DesktopPet.Windows.exe"), IntPtr.Zero, 0x22);
        Assert.AreNotEqual(IntPtr.Zero, module);
        try
        {
            var resource = FindResource(module, new IntPtr(1), new IntPtr(24));
            Assert.AreNotEqual(IntPtr.Zero, resource, "The executable must contain its startup manifest.");
            var data = LockResource(LoadResource(module, resource));
            Assert.AreNotEqual(IntPtr.Zero, data);
            var manifest = XDocument.Parse(Marshal.PtrToStringUTF8(data, checked((int)SizeofResource(module, resource)))!);
            XNamespace current = "http://schemas.microsoft.com/SMI/2016/WindowsSettings";
            XNamespace fallback = "http://schemas.microsoft.com/SMI/2005/WindowsSettings";
            Assert.AreEqual("PerMonitorV2", manifest.Descendants(current + "dpiAwareness").Single().Value);
            Assert.AreEqual("true/PM", manifest.Descendants(fallback + "dpiAware").Single().Value);
        }
        finally { FreeLibrary(module); }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LoadResource(IntPtr module, IntPtr resource);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LockResource(IntPtr resource);
    [DllImport("kernel32.dll")]
    private static extern uint SizeofResource(IntPtr module, IntPtr resource);
    [DllImport("kernel32.dll")]
    private static extern bool FreeLibrary(IntPtr module);
}
