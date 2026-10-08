using System.Buffers.Binary;
using DesktopPet.Core;

namespace DesktopPet.Tests;

[TestClass]
public sealed class HoodPlacementTests
{
    [TestMethod]
    [DataRow(-20d, PetAction.RunLeft)]
    [DataRow(20d, PetAction.RunRight)]
    public void Dragging_AnimatesHorizontalMovementEvenWhenRoamingAndIdleAnimationsAreDisabled(
        double dx, PetAction expectedAction)
    {
        var configuration = HoodConfiguration.Defaults with
        {
            Behavior = HoodConfiguration.Defaults.Behavior with
            {
                Roaming = false, IdleAnimations = false, RandomEvents = false
            }
        };
        var store = new RecordingSessionStore();
        var session = CreateSession([Primary(1920, 1040)], configuration, store: store);
        session.BeginDragging();
        session.MoveTo(session.Position with { X = session.Position.X + dx }, persist: false);
        var position = session.Position;
        var animation = Definition().GetAnimation(expectedAction);

        Assert.AreEqual(expectedAction, session.Presentation.Action);
        Assert.IsTrue(session.RequiresAnimationFrames);
        session.Advance(TimeSpan.FromMilliseconds(animation.FrameDurationMilliseconds));
        Assert.AreEqual(1, session.Presentation.FrameIndex);
        Assert.AreEqual(position, session.Position, "Dragging must not cause autonomous movement.");
        session.MoveTo(position with { X = position.X + dx }, persist: false);
        Assert.AreEqual(1, session.Presentation.FrameIndex, "Mouse moves must not restart the walk cycle.");
        session.Advance(TimeSpan.FromMilliseconds(animation.FrameDurationMilliseconds * animation.Frames.Count));
        Assert.AreEqual(1, session.Presentation.FrameIndex, "The walk cycle must loop.");
        session.SaveCurrentState();
        Assert.AreEqual(PetAction.Idle, store.Snapshot!.Action, "Drag feedback must not enter saved behavior.");
        session.EndDragging();
        Assert.AreEqual(PetAction.Idle, session.Presentation.Action);
        Assert.IsFalse(session.RequiresAnimationFrames);
    }

    [TestMethod]
    public void Dragging_ReversesDirectionAndResumesThePausedRoamingAnimationOnRelease()
    {
        var session = CreateSession([Primary(1920, 1040)]);
        session.Advance(TimeSpan.FromMilliseconds(250));
        var before = session.Presentation;
        Assert.AreEqual(PetAction.RunLeft, before.Action);
        session.BeginDragging();
        session.MoveTo(session.Position with { X = session.Position.X + 20 }, persist: false);
        session.Advance(TimeSpan.FromMilliseconds(100));
        Assert.AreEqual(PetAction.RunRight, session.Presentation.Action);
        session.MoveTo(session.Position with { X = session.Position.X - 10 }, persist: false);
        Assert.AreEqual(PetAction.RunLeft, session.Presentation.Action);
        Assert.AreEqual(0, session.Presentation.FrameIndex);
        session.EndDragging();

        Assert.AreEqual(before.Action, session.Presentation.Action);
        Assert.AreEqual(before.FrameIndex, session.Presentation.FrameIndex);
        var position = session.Position;
        session.Advance(TimeSpan.FromMilliseconds(100));
        Assert.IsTrue(session.Position.X < position.X, "Roaming must resume after release.");
    }

    [TestMethod]
    public void Dragging_VerticalOrClampedMovementDoesNotChooseANewHorizontalAnimation()
    {
        var session = CreateSession([Primary(1920, 1040)]);
        session.BeginDragging();
        session.MoveTo(session.Position with { Y = 400 }, persist: false);
        Assert.AreEqual(PetAction.Idle, session.Presentation.Action);
        session.MoveTo(session.Position with { X = -10000 }, persist: false);
        Assert.AreEqual(PetAction.RunLeft, session.Presentation.Action);
        session.Advance(TimeSpan.FromMilliseconds(100));
        var frame = session.Presentation.FrameIndex;
        session.MoveTo(session.Position with { X = -10000 }, persist: false);
        Assert.AreEqual(PetAction.RunLeft, session.Presentation.Action);
        Assert.AreEqual(frame, session.Presentation.FrameIndex);
        session.EndDragging();
        session.BeginDragging();
        session.MoveTo(session.Position with { Y = 300 }, persist: false);
        Assert.AreEqual(PetAction.Idle, session.Presentation.Action, "A new drag must clear old feedback.");
    }

    [TestMethod]
    public void Dragging_PreservesSleepAndRestoresItsPresentationOnRelease()
    {
        var store = new RecordingSessionStore();
        var session = CreateSession([Primary(1920, 1040)], store: store);
        session.Sleep();
        var remainingNap = session.RemainingNap;
        session.BeginDragging();
        session.MoveTo(session.Position with { X = session.Position.X + 20 }, persist: false);
        Assert.AreEqual(PetAction.RunRight, session.Presentation.Action);
        session.Advance(TimeSpan.FromMilliseconds(100));
        Assert.AreEqual(HoodSessionMode.Sleeping, session.Mode);
        Assert.AreEqual(remainingNap - TimeSpan.FromMilliseconds(100), session.RemainingNap);
        session.SaveCurrentState();
        HoodSessionSnapshotValidator.Validate(store.Snapshot!);
        Assert.AreEqual(PetAction.Sleep, store.Snapshot!.Action);
        session.EndDragging();
        Assert.AreEqual(PetAction.Sleep, session.Presentation.Action);
    }

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

    [TestMethod]
    [DataRow(-40d, 0d, true, false, 60d, 100d, 520d, 400d)]
    [DataRow(40d, 0d, false, false, 100d, 100d, 520d, 400d)]
    [DataRow(0d, -40d, false, true, 100d, 60d, 480d, 440d)]
    [DataRow(0d, 40d, false, false, 100d, 100d, 480d, 440d)]
    [DataRow(-40d, -40d, true, true, 60d, 60d, 520d, 440d)]
    [DataRow(40d, -40d, false, true, 100d, 60d, 520d, 440d)]
    [DataRow(-40d, 40d, true, false, 60d, 100d, 520d, 440d)]
    [DataRow(40d, 40d, false, false, 100d, 100d, 520d, 440d)]
    public void ResizeRoamingAreaBy_MovesEachEdgeOrCornerAndKeepsTheOppositeEdgesFixed(
        double dx, double dy, bool fromLeft, bool fromTop, double x, double y, double width, double height)
    {
        var saved = new HoodSessionSnapshot
        {
            Position = new PetPosition(244, 196),
            RoamingArea = new PetBounds(100, 100, 480, 400)
        };
        var store = new RecordingSessionStore();
        var session = CreateSession([Primary(1920, 1040)], restored: saved, store: store);

        session.ResizeRoamingAreaBy(dx, dy, fromLeft, fromTop, persist: false);

        Assert.AreEqual(new PetBounds(x, y, width, height), session.RoamingArea);
        Assert.AreEqual(saved.Position, session.Position);
        Assert.IsNull(store.Snapshot, "Dragging saves only when the edit is completed.");
        session.SaveCurrentState();
        var restored = CreateSession([Primary(1920, 1040)], restored: store.Snapshot);
        Assert.AreEqual(session.RoamingArea, restored.RoamingArea);
        Assert.AreEqual(session.Position, restored.Position);
    }

    [TestMethod]
    [DataRow(true, true, 388d, 292d)]
    [DataRow(false, false, 100d, 100d)]
    [DataRow(true, false, 388d, 100d)]
    [DataRow(false, true, 100d, 292d)]
    public void ResizeRoamingAreaBy_StopsAtTheMinimumSizeAndKeepsHoodInside(
        bool fromLeft, bool fromTop, double expectedX, double expectedY)
    {
        var session = CreateSession([Primary(1920, 1040)], restored: new HoodSessionSnapshot
        {
            Position = new PetPosition(244, 196),
            RoamingArea = new PetBounds(100, 100, 480, 400)
        });

        session.ResizeRoamingAreaBy(fromLeft ? 1000 : -1000, fromTop ? 1000 : -1000, fromLeft, fromTop);

        Assert.AreEqual(new PetBounds(expectedX, expectedY, 192, 208), session.RoamingArea);
        Assert.AreEqual(new PetPosition(expectedX, expectedY), session.Position);
    }

    [TestMethod]
    [DataRow(true, true, -1280d, -300d, 860d, 600d)]
    [DataRow(false, false, -900d, -100d, 900d, 600d)]
    public void ResizeRoamingAreaBy_ClampsEdgesToTheCurrentScaledMonitor(
        bool fromLeft, bool fromTop, double x, double y, double width, double height)
    {
        var secondary = new DisplayInfo("Scaled left", new PetBounds(-1280, -300, 1280, 800), false, 1.5);
        var session = CreateSession([Primary(1920, 1040), secondary], restored: new HoodSessionSnapshot
        {
            Position = new PetPosition(-756, 0),
            RoamingArea = new PetBounds(-900, -100, 480, 400)
        });

        session.ResizeRoamingAreaBy(fromLeft ? -10000 : 10000, fromTop ? -10000 : 10000, fromLeft, fromTop);

        Assert.AreEqual(new PetBounds(x, y, width, height), session.RoamingArea);
        Assert.AreEqual(new PetPosition(-756, 0), session.Position);
    }

    [TestMethod]
    [DataRow(double.NaN, 0d)]
    [DataRow(0d, double.PositiveInfinity)]
    public void ResizeRoamingAreaBy_RejectsNonFiniteChangesWithoutChangingPlacement(double dx, double dy)
    {
        var session = CreateSession([Primary(1920, 1040)]);
        var position = session.Position;
        var area = session.RoamingArea;

        Assert.Throws<ArgumentOutOfRangeException>(() => session.ResizeRoamingAreaBy(dx, dy));

        Assert.AreEqual(position, session.Position);
        Assert.AreEqual(area, session.RoamingArea);
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
