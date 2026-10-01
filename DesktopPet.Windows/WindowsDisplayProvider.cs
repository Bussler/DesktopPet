using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;
using DesktopPet.Core;

namespace DesktopPet.Windows;

internal static class WindowsDisplayProvider
{
    private const int EffectiveDpi = 0;
    private const uint MonitorDefaultToNearest = 2;

    public static DisplayInfo[] GetDisplays()
    {
        var screens = Forms.Screen.AllScreens;
        var primary = screens.Single(screen => screen.Primary);
        var primaryScale = GetScale(primary);
        return screens.Select(screen =>
        {
            var scale = GetScale(screen);
            var bounds = screen.Bounds;
            var workArea = screen.WorkingArea;
            var originScaleX = bounds.Right <= primary.Bounds.Left ? scale : primaryScale;
            var originScaleY = bounds.Bottom <= primary.Bounds.Top ? scale : primaryScale;
            var x = bounds.X / originScaleX + (workArea.X - bounds.X) / scale;
            var y = bounds.Y / originScaleY + (workArea.Y - bounds.Y) / scale;
            return new DisplayInfo(
                screen.DeviceName,
                new PetBounds(x, y, workArea.Width / scale, workArea.Height / scale),
                screen.Primary,
                scale);
        }).ToArray();
    }

    private static double GetScale(Forms.Screen screen)
    {
        var bounds = screen.Bounds;
        var monitor = MonitorFromPoint(new NativePoint(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2), MonitorDefaultToNearest);
        if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, EffectiveDpi, out var dpiX, out _) == 0 && dpiX > 0)
        {
            return dpiX / 96d;
        }

        return 1;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("shcore.dll", ExactSpelling = true)]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint(int x, int y)
    {
        public readonly int X = x;
        public readonly int Y = y;
    }
}
