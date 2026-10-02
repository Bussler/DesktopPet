using System.Buffers.Binary;
using DesktopPet.Core;

namespace DesktopPet.Tests;

[TestClass]
public sealed class HoodPlacementTests
{
    [TestMethod]
    public void Start_CreatesAnAreaLargeEnoughForTheCompletePetFootprint()
    {
        var session = CreateSession([Primary(1920, 1040)]);

        Assert.AreEqual(new PetPosition(864, 816), session.Position);
        Assert.AreEqual(new PetBounds(720, 816, 480, 208), session.RoamingArea);
    }

    [TestMethod]
    public void MoveTo_TranslatesPetAndAreaTogetherAndPersistsTheChosenPlacement()
    {
        var store = new RecordingSessionStore();
        var session = CreateSession([Primary(1920, 1040)], store: store);
        var action = session.Presentation.Action;
        var offsetX = session.Position.X - session.RoamingArea.X;
        var offsetY = session.Position.Y - session.RoamingArea.Y;

        session.MoveTo(new PetPosition(300, 400));

        Assert.AreEqual(new PetPosition(300, 400), session.Position);
        Assert.AreEqual(offsetX, session.Position.X - session.RoamingArea.X);
        Assert.AreEqual(offsetY, session.Position.Y - session.RoamingArea.Y);
        Assert.AreEqual(action, session.Presentation.Action);
        Assert.AreEqual(session.Position, store.Snapshot!.Position);
        Assert.AreEqual(session.RoamingArea, store.Snapshot.RoamingArea);
    }

    [TestMethod]
    public void MoveTo_UsesDestinationMonitorLogicalBoundsWithNegativeOriginAndDifferentScale()
    {
        var primary = Primary(1920, 1040);
        var secondary = new DisplayInfo("Scaled left", new PetBounds(-1280, 0, 1280, 720), false, ScaleFactor: 1.5);
        var session = CreateSession([primary, secondary]);

        session.MoveTo(new PetPosition(-900, 100));

        Assert.AreEqual(-900, session.Position.X);
        Assert.AreEqual(secondary, PetPlacementPolicy.Move(
            Definition(),
            session.Position,
            session.RoamingArea,
            session.Position,
            [primary, secondary]).Display);
        Assert.IsTrue(session.RoamingArea.X >= secondary.WorkArea.X);
        Assert.IsTrue(session.RoamingArea.Right <= secondary.WorkArea.Right);
    }

    [TestMethod]
    public void MoveTo_ClampsPetAndAreaTogetherAtTheWorkAreaEdges()
    {
        var session = CreateSession([Primary(1920, 1040)]);

        session.MoveTo(new PetPosition(1900, 900));

        Assert.AreEqual(1440, session.RoamingArea.X);
        Assert.AreEqual(832, session.Position.Y);
        Assert.AreEqual(session.Position.X - 144, session.RoamingArea.X);
        Assert.AreEqual(session.Position.Y, session.RoamingArea.Y);
        Assert.IsTrue(session.RoamingArea.Right <= 1920);
        Assert.IsTrue(session.RoamingArea.Bottom <= 1040);
        Assert.IsTrue(session.Position.X + 192 <= 1920);
        Assert.IsTrue(session.Position.Y + 208 <= 1040);
    }

    [TestMethod]
    public void Restore_ShrinksOversizedSavedAreaAroundThePetAndKeepsItOnScreen()
    {
        var saved = new HoodSessionSnapshot
        {
            Position = new PetPosition(400, 150),
            RoamingArea = new PetBounds(-500, -300, 4000, 1800)
        };

        var session = CreateSession([Primary(1000, 500)], restored: saved);

        Assert.AreEqual(1000, session.RoamingArea.Width);
        Assert.AreEqual(500, session.RoamingArea.Height);
        Assert.IsTrue(session.RoamingArea.X <= session.Position.X);
        Assert.IsTrue(session.RoamingArea.Y <= session.Position.Y);
        Assert.IsTrue(session.RoamingArea.Right >= session.Position.X + 192);
        Assert.IsTrue(session.RoamingArea.Bottom >= session.Position.Y + 208);
        Assert.IsTrue(session.RoamingArea.X >= 0);
        Assert.IsTrue(session.RoamingArea.Y >= 0);
    }

    [TestMethod]
    public void UpdateDisplays_RelocatesSavedPlacementToThePrimaryAfterMonitorRemoval()
    {
        var store = new RecordingSessionStore();
        var primary = Primary(1280, 800);
        var secondary = new DisplayInfo("Left", new PetBounds(-1280, 0, 1280, 800), false, ScaleFactor: 1.5);
        var session = CreateSession([primary, secondary], store: store);
        session.MoveTo(new PetPosition(-900, 100));

        session.UpdateDisplays([primary]);

        Assert.IsTrue(session.Position.X >= primary.WorkArea.X);
        Assert.IsTrue(session.Position.Y >= primary.WorkArea.Y);
        Assert.IsTrue(session.Position.X + 192 <= primary.WorkArea.Right);
        Assert.IsTrue(session.Position.Y + 208 <= primary.WorkArea.Bottom);
        Assert.IsTrue(session.RoamingArea.X >= primary.WorkArea.X);
        Assert.IsTrue(session.RoamingArea.Right <= primary.WorkArea.Right);
        Assert.AreEqual(session.Position, store.Snapshot!.Position);
    }

    [TestMethod]
    public void MoveTo_RemainsAvailableWhenRoamingIsOffAndDoesNotWakeSleepingHood()
    {
        var configuration = HoodConfiguration.Defaults with
        {
            Behavior = HoodConfiguration.Defaults.Behavior with { Roaming = false }
        };
        var session = CreateSession([Primary(1920, 1040)], configuration);
        session.Sleep();
        var areaOffsetX = session.Position.X - session.RoamingArea.X;
        var areaOffsetY = session.Position.Y - session.RoamingArea.Y;

        session.MoveTo(new PetPosition(300, 400));

        Assert.AreEqual(HoodSessionMode.Sleeping, session.Mode);
        Assert.AreEqual(PetAction.Sleep, session.Presentation.Action);
        Assert.AreEqual(new PetPosition(300, 400), session.Position);
        Assert.AreEqual(areaOffsetX, session.Position.X - session.RoamingArea.X);
        Assert.AreEqual(areaOffsetY, session.Position.Y - session.RoamingArea.Y);
    }

    [TestMethod]
    public void MoveTo_PersistsPlacementThatRestoresWithoutReturningToInitialPosition()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonSessionStore(Path.Combine(directory.Path, "state.json"));
        var displays = new[] { Primary(1920, 1040) };
        var session = CreateSession(displays, store: store);
        session.MoveTo(new PetPosition(500, 250));
        session.Quit();

        var restored = CreateSession(displays, restored: store.Load(), store: store);

        Assert.AreEqual(new PetPosition(500, 250), restored.Position);
        Assert.AreEqual(session.RoamingArea, restored.RoamingArea);
    }

    [TestMethod]
    public void AreaEditing_MovesTheBoundariesAndPetTogetherAndSuspendsAmbientBehavior()
    {
        var session = CreateSession([Primary(1920, 1040)]);
        var initialPosition = session.Position;
        var initialArea = session.RoamingArea;

        session.BeginAreaEditing();
        session.MoveRoamingAreaBy(100, 0, persist: false);
        session.MoveRoamingAreaBy(0, -50, persist: false);

        Assert.IsTrue(session.IsEditingArea);
        Assert.AreEqual(new PetPosition(initialPosition.X, initialPosition.Y - 50), session.Position);
        Assert.AreEqual(new PetBounds(initialArea.X + 100, initialArea.Y - 50, initialArea.Width, initialArea.Height), session.RoamingArea);
        session.EndAreaEditing();
        Assert.IsFalse(session.IsEditingArea);
    }

    [TestMethod]
    public void MoveRoamingArea_ClampsHoodInsideWhenTheAreaMovesPastHim()
    {
        var session = CreateSession([Primary(1920, 1040)]);

        session.MoveRoamingAreaBy(500, 0);

        Assert.IsTrue(session.Position.X >= session.RoamingArea.X);
        Assert.IsTrue(session.Position.X + 192 <= session.RoamingArea.Right);
        Assert.IsTrue(session.Position.Y >= session.RoamingArea.Y);
        Assert.IsTrue(session.Position.Y + 208 <= session.RoamingArea.Bottom);
    }

    [TestMethod]
    public void ResizeRoamingArea_EnforcesTheMinimumPetFootprintAndContainsHood()
    {
        var session = CreateSession([Primary(1920, 1040)]);

        session.ResizeRoamingArea(10, 10);

        Assert.AreEqual(session.MinimumRoamingArea.Width, session.RoamingArea.Width);
        Assert.AreEqual(session.MinimumRoamingArea.Height, session.RoamingArea.Height);
        Assert.IsTrue(session.RoamingArea.X <= session.Position.X);
        Assert.IsTrue(session.RoamingArea.Y <= session.Position.Y);
        Assert.IsTrue(session.RoamingArea.Right >= session.Position.X + 192);
        Assert.IsTrue(session.RoamingArea.Bottom >= session.Position.Y + 208);
    }

    [TestMethod]
    public void ResizeRoamingArea_ClampsExpandedBoundsToTheMonitorWorkArea()
    {
        var session = CreateSession([Primary(1000, 500)]);

        session.ResizeRoamingArea(3000, 3000);

        Assert.AreEqual(1000, session.RoamingArea.Width);
        Assert.AreEqual(500, session.RoamingArea.Height);
        Assert.AreEqual(0, session.RoamingArea.X);
        Assert.AreEqual(0, session.RoamingArea.Y);
        Assert.IsTrue(session.RoamingArea.X <= session.Position.X);
        Assert.IsTrue(session.RoamingArea.Y <= session.Position.Y);
        Assert.IsTrue(session.RoamingArea.Right >= session.Position.X + 192);
        Assert.IsTrue(session.RoamingArea.Bottom >= session.Position.Y + 208);
    }

    [TestMethod]
    public void ResizeRoamingArea_PersistsEditorChangesThroughTheExistingSnapshot()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonSessionStore(Path.Combine(directory.Path, "state.json"));
        var displays = new[] { Primary(1920, 1040) };
        var session = CreateSession(displays, store: store);
        session.ResizeRoamingArea(640, 360);
        session.Quit();

        var restored = CreateSession(displays, restored: store.Load(), store: store);

        Assert.AreEqual(session.Position, restored.Position);
        Assert.AreEqual(session.RoamingArea, restored.RoamingArea);
    }

    [TestMethod]
    public void React_JumpsAboveTheRoamingAreaAndLandsAtTheSameSpotAfterOnePointTwoSeconds()
    {
        var defaults = HoodConfiguration.Defaults;
        var configuration = defaults with { Behavior = defaults.Behavior with { Roaming = false, RandomEvents = false } };
        var store = new RecordingSessionStore();
        var session = CreateSession([Primary(1920, 1040)], configuration, store: store);
        var start = session.Position;
        var area = session.RoamingArea;

        session.React();
        session.Advance(TimeSpan.FromMilliseconds(320));
        Assert.AreEqual(start, session.Presentation.Position, "Preparation stays on the ground.");
        session.Advance(TimeSpan.FromMilliseconds(320));
        Assert.AreEqual(start.X, session.Presentation.Position.X);
        Assert.AreEqual(start.Y - 90, session.Presentation.Position.Y, 1e-9);
        Assert.IsTrue(session.Presentation.Position.Y < area.Y);
        Assert.AreEqual(start, session.Position);
        Assert.AreEqual(area, session.RoamingArea);

        session.SaveCurrentState();
        Assert.AreEqual(start, store.Snapshot!.Position, "Saving mid-jump must retain the landing position.");
        var restored = CreateSession([Primary(1920, 1040)], configuration, restored: store.Snapshot);
        Assert.AreEqual(session.Presentation.Position, restored.Presentation.Position);

        session.Advance(TimeSpan.FromMilliseconds(559));
        Assert.AreEqual(PetAction.Jump, session.Presentation.Action);
        Assert.AreEqual(start, session.Presentation.Position, "Landing settles on the original baseline.");
        session.Advance(TimeSpan.FromMilliseconds(1));
        Assert.AreEqual(PetAction.Idle, session.Presentation.Action);
        Assert.AreEqual(start, session.Presentation.Position);
    }

    [TestMethod]
    [DataRow(0d, 0d, 1d)]
    [DataRow(0d, 25d, 1d)]
    [DataRow(-600d, 25d, 1.5d)]
    [DataRow(-600d, 150d, 2d)]
    public void React_LimitsTheEntireJumpToTheCurrentMonitorsWorkArea(double top, double clearance, double scale)
    {
        var display = new DisplayInfo("Left", new PetBounds(-1280, top, 1280, 720), false, scale);
        var defaults = HoodConfiguration.Defaults;
        var configuration = defaults with { Behavior = defaults.Behavior with { Roaming = false, RandomEvents = false } };
        var session = CreateSession([Primary(1920, 1040), display], configuration);
        session.MoveTo(new PetPosition(-900, top + clearance));
        var start = session.Position;
        var highest = start.Y;
        var displayHeight = Definition().DisplayHeight;
        session.React();

        for (var milliseconds = 0; milliseconds < 1200; milliseconds += 10)
        {
            session.Advance(TimeSpan.FromMilliseconds(10));
            var position = session.Presentation.Position;
            Assert.AreEqual(start.X, position.X);
            Assert.IsTrue(position.Y >= display.WorkArea.Y);
            Assert.IsTrue(position.Y + displayHeight <= display.WorkArea.Bottom);
            highest = Math.Min(highest, position.Y);
        }

        Assert.AreEqual(start.Y - Math.Min(90, clearance), highest, 1e-9);
        Assert.AreEqual(start, session.Presentation.Position);
    }

    [TestMethod]
    public void React_PausesHorizontalRoamingDuringFlightAndResumesAfterLanding()
    {
        var session = CreateSession([Primary(1920, 1040)]);
        session.Advance(TimeSpan.FromMilliseconds(100));
        var start = session.Position;
        session.React();
        session.Advance(TimeSpan.FromMilliseconds(640));
        Assert.AreEqual(start, session.Position);
        Assert.AreEqual(start.X, session.Presentation.Position.X);
        session.Advance(TimeSpan.FromMilliseconds(560));
        Assert.AreEqual(start, session.Presentation.Position);
        session.Advance(TimeSpan.FromMilliseconds(100));
        Assert.AreEqual(1.8, Math.Abs(session.Position.X - start.X), 1e-9);
        Assert.AreEqual(start.Y, session.Position.Y);
    }

    [TestMethod]
    public void React_RepeatAndFeedInterruptionsDoNotAccumulateVerticalDisplacement()
    {
        var session = CreateSession([Primary(1920, 1040)]);
        var start = session.Position;
        session.React();
        session.Advance(TimeSpan.FromMilliseconds(640));
        session.React();
        Assert.AreEqual(start, session.Presentation.Position);
        session.Advance(TimeSpan.FromMilliseconds(640));
        Assert.AreEqual(start.Y - 90, session.Presentation.Position.Y, 1e-9);
        session.Feed();
        Assert.AreEqual(PetAction.Feed, session.Presentation.Action);
        Assert.AreEqual(start, session.Presentation.Position);
        Assert.AreEqual(start, session.Position);
    }

    private static DisplayInfo Primary(double width, double height) =>
        new("Primary", new PetBounds(0, 0, width, height), IsPrimary: true);

    private static HoodSession CreateSession(
        IReadOnlyCollection<DisplayInfo> displays,
        HoodConfiguration? configuration = null,
        HoodSessionSnapshot? restored = null,
        ISessionStore? store = null) =>
        HoodSession.Start(
            Definition(),
            displays,
            new FixedRandomSource(),
            store ?? new NoopSessionStore(),
            configuration,
            restored);

    private static PetDefinition Definition()
    {
        var assetDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Hood");
        var json = File.ReadAllText(Path.Combine(assetDirectory, "hood.animations.json"));
        var png = File.ReadAllBytes(Path.Combine(assetDirectory, "hood_spritesheet.png"));
        return PetDefinition.Parse(
            json,
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)),
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"hood-placement-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class FixedRandomSource : IPetRandomSource
    {
        public int NextIndex(int exclusiveUpperBound) => 0;
    }

    private sealed class NoopSessionStore : ISessionStore
    {
        public HoodSessionSnapshot? Load() => null;
        public void Save(HoodSessionSnapshot snapshot) { }
        public string? ResetAfterAcknowledgement() => null;
    }

    private sealed class RecordingSessionStore : ISessionStore
    {
        public HoodSessionSnapshot? Snapshot { get; private set; }
        public HoodSessionSnapshot? Load() => null;
        public void Save(HoodSessionSnapshot snapshot) => Snapshot = snapshot;
        public string? ResetAfterAcknowledgement() => null;
    }
}
