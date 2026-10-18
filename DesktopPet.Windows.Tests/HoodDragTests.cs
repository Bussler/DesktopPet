using System.IO;
using System.Reflection;
using System.Windows;
using DesktopPet.Core;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[STATestClass]
public sealed class HoodDragTests
{
    [TestMethod]
    [DataRow(1d, 1.5d)]
    [DataRow(1.25d, 1d)]
    [DataRow(1.5d, 1.25d)]
    public void ScaledRightMonitor_MapsBothWorkAreaEdgesToTheirPhysicalPositions(double primaryScale, double secondaryScale)
    {
        var workArea = new PetBounds(1920, 60, 2560, 1380);
        var display = new DisplayInfo("Right", new PetBounds(1920 / primaryScale, 60 / secondaryScale,
            workArea.Width / secondaryScale, workArea.Height / secondaryScale), false, secondaryScale);
        foreach (var position in new[]
        {
            new PetPosition(display.WorkArea.X, display.WorkArea.Y),
            new PetPosition(display.WorkArea.Right - 480, display.WorkArea.Bottom - 208)
        })
        {
            var actual = WindowsDisplayProvider.ToScreenPosition(position, display, workArea);
            Assert.AreEqual(workArea.X + (position.X - display.WorkArea.X) * secondaryScale, actual.X, 0.001);
            Assert.AreEqual(workArea.Y + (position.Y - display.WorkArea.Y) * secondaryScale, actual.Y, 0.001);
        }
    }

    [TestMethod]
    [DataRow(1d, 1d, 1d)]
    [DataRow(-1d, 1d, 1d)]
    [DataRow(1d, 1d, 1.5d)]
    [DataRow(-1d, 1.5d, 1d)]
    public void SlowDrag_CrossesMonitorBoundaryWithoutLosingPointerMovement(double direction, double startScale, double endScale)
    {
        var assets = HoodAssets.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Hood"));
        var definition = assets.Definition;
        var secondary = new DisplayInfo("Secondary", new PetBounds(direction > 0 ? 1920 : -1920, 0, 1920, 1040), false, endScale);
        var session = HoodSession.Start(definition,
            [new("Primary", new PetBounds(0, 0, 1920, 1040), true, startScale), secondary],
            new FixedRandomSource(), new NoopSessionStore());
        var start = new PetPosition(direction > 0 ? 1400 : 300, 400);
        session.MoveTo(start);
        var areaOffset = new PetPosition(start.X - session.RoamingArea.X, start.Y - session.RoamingArea.Y);
        session.BeginDragging();
        Action noop = () => { };
        var pet = new HoodOverlayWindow(session, assets, noop, noop, noop, noop, noop, noop);
        try
        {
            typeof(HoodOverlayWindow).GetField("_dragStartPosition", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(pet, start);
            var move = typeof(HoodOverlayWindow).GetMethod("MoveDraggedPet", BindingFlags.Instance | BindingFlags.NonPublic)!;
            // Replay physical pointer samples through the same path used by OnMouseMove.
            var pointerX = 0d;
            for (var distance = 2; distance <= 1000; distance += 2)
            {
                var scale = distance <= 500 ? startScale : endScale;
                pointerX += direction * 2 * scale;
                move.Invoke(pet, [new Point(pointerX, 0), new DpiScale(scale, scale)]);
            }

            Assert.AreEqual(start.X + direction * 1000, session.Position.X, 0.001,
                "Slow pointer samples must accumulate past the old monitor's clamp.");
            Assert.IsTrue(session.RoamingArea.X >= secondary.WorkArea.X);
            Assert.IsTrue(session.RoamingArea.Right <= secondary.WorkArea.Right);
            Assert.AreEqual(areaOffset.X, session.Position.X - session.RoamingArea.X, 0.001);
            Assert.AreEqual(areaOffset.Y, session.Position.Y - session.RoamingArea.Y, 0.001);
            for (var distance = 1000; distance > 0; distance -= 2)
            {
                var scale = distance <= 500 ? startScale : endScale;
                pointerX -= direction * 2 * scale;
                move.Invoke(pet, [new Point(pointerX, 0), new DpiScale(scale, scale)]);
            }
            Assert.AreEqual(start, session.Position, "Dragging back must recover the original placement.");
        }
        finally { pet.Close(); }
    }

    private sealed class FixedRandomSource : IPetRandomSource
    {
        public int NextIndex(int exclusiveUpperBound) => 0;
    }

    private sealed class NoopSessionStore : ISessionStore
    {
        public void Save(HoodSessionSnapshot snapshot) { }
    }
}
