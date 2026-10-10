using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using DesktopPet.Core;

namespace DesktopPet.Windows;

internal static class WindowsDisplayProvider
{
    private const int EffectiveDpi = 0;
    private const uint MonitorDefaultToNearest = 2;
    private const uint PositionOnly = 0x0001 | 0x0004 | 0x0010; // No resize, z-order change, or activation.

    public static DisplayInfo[] GetDisplays() => GetDisplays(Forms.Screen.AllScreens);

    private static DisplayInfo[] GetDisplays(Forms.Screen[] screens)
    {
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

    public static void ApplyPosition(Window window, PetPosition position)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            window.Left = position.X;
            window.Top = position.Y;
            return;
        }

        var screens = Forms.Screen.AllScreens;
        var display = GetDisplays(screens).MinBy(display =>
        {
            var area = display.WorkArea;
            var dx = position.X - Math.Clamp(position.X, area.X, area.Right);
            var dy = position.Y - Math.Clamp(position.Y, area.Y, area.Bottom);
            return dx * dx + dy * dy;
        })!;
        var workArea = screens.Single(screen => screen.DeviceName == display.Name).WorkingArea;
        var point = ToScreenPosition(position, display, new PetBounds(workArea.X, workArea.Y, workArea.Width, workArea.Height));
        // Physical positioning avoids WPF scaling the desktop origin with the window's DPI.
        if (!SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(point.X), (int)Math.Round(point.Y), 0, 0, PositionOnly))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    internal static PetPosition ToScreenPosition(PetPosition position, DisplayInfo display, PetBounds physicalWorkArea) =>
        new(physicalWorkArea.X + (position.X - display.WorkArea.X) * display.ScaleFactor,
            physicalWorkArea.Y + (position.Y - display.WorkArea.Y) * display.ScaleFactor);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    public static double GetScaleForPet(PetPosition position, PetDefinition definition, IReadOnlyCollection<DisplayInfo> displays)
    {
        var x = position.X + definition.DisplayWidth / 2;
        var y = position.Y + definition.DisplayHeight / 2;
        var display = displays.FirstOrDefault(candidate =>
            x >= candidate.WorkArea.X && x < candidate.WorkArea.Right &&
            y >= candidate.WorkArea.Y && y < candidate.WorkArea.Bottom);
        if (display is not null)
        {
            return display.ScaleFactor;
        }

        return displays.MinBy(candidate =>
        {
            var dx = Math.Max(candidate.WorkArea.X - x, Math.Max(0, x - candidate.WorkArea.Right));
            var dy = Math.Max(candidate.WorkArea.Y - y, Math.Max(0, y - candidate.WorkArea.Bottom));
            return dx * dx + dy * dy;
        })?.ScaleFactor ?? 1;
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
