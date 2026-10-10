using DesktopPet.Core;

namespace DesktopPet.Tests;

[TestClass]
public sealed class NotePlacementPolicyTests
{
    [TestMethod]
    public void Create_OffsetsPastNotesNearTheDefaultAndNextCascadePositions()
    {
        var bounds = new NoteBounds(96, 96, 320, 300);
        var existing = new[] { bounds with { Left = 100, Top = 100 }, bounds with { Left = 120, Top = 120 } };

        Assert.AreEqual(new NoteBounds(144, 144, 320, 300), NotePlacementPolicy.Create(bounds, existing));
    }

    [TestMethod]
    public void Create_FindsAnotherPositionWhenTheCascadeReachesTheWorkAreaEdge()
    {
        var bounds = new NoteBounds(96, 96, 320, 300);
        var display = new DisplayInfo("Small", new PetBounds(96, 96, 368, 348), true);
        var existing = new[] { bounds, bounds with { Left = 120, Top = 120 }, bounds with { Left = 144, Top = 144 } };

        Assert.AreEqual(new NoteBounds(120, 96, 320, 300), NotePlacementPolicy.Create(bounds, existing, [display]));
    }

    [TestMethod]
    public void Create_KeepsMaximumNoteCountAtDistinctAccessiblePositions()
    {
        var bounds = new NoteBounds(96, 96, 320, 300);
        var display = new DisplayInfo("Primary", new PetBounds(0, 0, 1920, 1040), true);
        var existing = new List<NoteBounds>();

        for (var index = 0; index < NotesSnapshotValidator.MaximumNotes; index++)
        {
            var created = NotePlacementPolicy.Create(bounds, existing, [display]);
            Assert.AreEqual(created, NotePlacementPolicy.Restore(created, [display]));
            Assert.IsFalse(existing.Any(note => Math.Abs(note.Left - created.Left) < 24 && Math.Abs(note.Top - created.Top) < 24));
            existing.Add(created);
        }
    }

    [TestMethod]
    public void Create_OffsetsOnNegativeOriginScaledDisplay()
    {
        var display = new DisplayInfo("Left", new PetBounds(-800, 40, 640, 460), true, ScaleFactor: 1.5);
        var bounds = new NoteBounds(96, 96, 320, 300);
        var first = NotePlacementPolicy.Create(bounds, [], [display]);

        Assert.AreEqual(new NoteBounds(-480, 120, 320, 300), NotePlacementPolicy.Create(bounds, [first], [display]));
    }

    [TestMethod]
    public void Create_WhenOnlyOnePositionFitsKeepsTheNoteAccessible()
    {
        var display = new DisplayInfo("Tiny", new PetBounds(0, 0, 240, 180), true);
        var bounds = new NoteBounds(96, 96, 320, 300);
        var first = NotePlacementPolicy.Create(bounds, [], [display]);

        Assert.AreEqual(new NoteBounds(0, 0, 240, 180), NotePlacementPolicy.Create(bounds, [first], [display]));
    }

    [TestMethod]
    public void Restore_KeepsAccessibleBoundsOnNegativeOriginScaledDisplay()
    {
        var bounds = new NoteBounds(-1100, 80, 420, 360);
        var display = new DisplayInfo("Scaled left", new PetBounds(-1280, 40, 1280, 680), false, ScaleFactor: 1.5);

        var restored = NotePlacementPolicy.Restore(bounds, [display]);

        Assert.AreSame(bounds, restored);
    }

    [TestMethod]
    public void Restore_ShrinksAndClampsToTheSuppliedWorkArea()
    {
        var workArea = new PetBounds(-800, 20, 640, 460);
        var display = new DisplayInfo("Small display", workArea, true, ScaleFactor: 1.25);
        var inaccessible = new NoteBounds(-2000, -900, 1200, 900);

        var restored = NotePlacementPolicy.Restore(inaccessible, [display]);

        Assert.AreEqual(new NoteBounds(-800, 20, 640, 460), restored);
        Assert.IsTrue(restored.Left >= workArea.X);
        Assert.IsTrue(restored.Top >= workArea.Y);
        Assert.IsTrue(restored.Left + restored.Width <= workArea.Right);
        Assert.IsTrue(restored.Top + restored.Height <= workArea.Bottom);
    }

    [TestMethod]
    public void Restore_UsesRemainingPrimaryAfterNegativeOriginMonitorIsRemoved()
    {
        var remaining = new DisplayInfo("Primary", new PetBounds(0, 0, 1280, 760), true);
        var boundsOnRemovedDisplay = new NoteBounds(-1540, 100, 360, 320);

        var restored = NotePlacementPolicy.Restore(boundsOnRemovedDisplay, [remaining]);

        Assert.AreEqual(new NoteBounds(0, 100, 360, 320), restored);
    }

    [TestMethod]
    public void Restore_PreservesUsablePositionRegardlessOfScaleDescriptionAndReservedArea()
    {
        var bounds = new NoteBounds(600, 300, 400, 320);
        var workArea = new PetBounds(0, 0, 1200, 760);
        var highScale = new DisplayInfo("Display", workArea, true, ScaleFactor: 2.0);
        var lowScale = highScale with { ScaleFactor = 1.0 };

        Assert.AreEqual(bounds, NotePlacementPolicy.Restore(bounds, [highScale]));
        Assert.AreEqual(bounds, NotePlacementPolicy.Restore(bounds, [lowScale]));
        Assert.IsTrue(bounds.Top + bounds.Height <= workArea.Bottom);
    }

    [TestMethod]
    public void Restore_ChoosesAreaWithGreatestOverlapWhenNoteSpansDisplays()
    {
        var primary = new DisplayInfo("Primary", new PetBounds(0, 0, 800, 600), true);
        var secondary = new DisplayInfo("Right", new PetBounds(800, 0, 800, 600), false);

        var restored = NotePlacementPolicy.Restore(new NoteBounds(650, 50, 500, 360), [primary, secondary]);

        Assert.AreEqual(new NoteBounds(800, 50, 500, 360), restored);
    }

    [TestMethod]
    public void Restore_RequiresAWorkAreaLargeEnoughForTheDocumentedMinimum()
    {
        var tooSmall = new DisplayInfo("Tiny", new PetBounds(0, 0, 200, 160), true);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            NotePlacementPolicy.Restore(new NoteBounds(0, 0, 320, 300), [tooSmall]));

        StringAssert.Contains(exception.Message, "240×180");
    }
}
