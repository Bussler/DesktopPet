using System.Buffers.Binary;
using DesktopPet.Core;

namespace DesktopPet.Tests;

[TestClass]
public sealed class HoodRoamingTests
{
    [TestMethod]
    public void ScaledMinimumAreasRemainIdleAtEverySliderSize()
    {
        for (var percent = 50; percent <= 200; percent += 5)
        {
            var session = CreateSession(new PetBounds(100, 100, 640, 500), new PetPosition(300, 300),
                scale: percent / 100d);
            session.ResizeRoamingArea(0, 0, persist: false);
            var start = session.Position;
            session.Advance(TimeSpan.FromTicks(1));
            Assert.AreEqual(PetAction.Idle, session.Presentation.Action, $"Size {percent}% must stay idle.");
            session.Advance(TimeSpan.FromSeconds(1));
            Assert.AreEqual(start, session.Position);
            Assert.IsTrue(session.Configuration.Behavior.Roaming);
        }
    }

    [TestMethod]
    [DataRow(17.9, 0d, 0d, 0d)]
    [DataRow(18d, 0d, 1.8, 0d)]
    [DataRow(18.1, 0d, 1.8, 0d)]
    [DataRow(0d, 17.9, 0d, 0d)]
    [DataRow(0d, 18d, 0d, 1.8)]
    [DataRow(0d, 18.1, 0d, 1.8)]
    [DataRow(17.9, 18d, 0d, 1.8)]
    [DataRow(18d, 17.9, 1.8, 0d)]
    public void RoamingRequiresOneSecondOfSpaceOnEachAxis(double horizontalSpace, double verticalSpace,
        double dx, double dy)
    {
        for (var percent = 50; percent <= 200; percent += 5)
        {
            var start = new PetPosition(100, 100);
            var session = CreateSession(new PetBounds(100, 100, 640, 500), start, scale: percent / 100d);
            session.ResizeRoamingArea(session.PetWidth + horizontalSpace, session.PetHeight + verticalSpace, persist: false);
            session.MoveRoamingAreaBy(start.X - session.RoamingArea.X, start.Y - session.RoamingArea.Y, persist: false);
            session.Advance(TimeSpan.FromMilliseconds(100));
            Assert.AreEqual(start.X + dx, session.Position.X, 1e-9, $"Size {percent}% horizontal threshold.");
            Assert.AreEqual(start.Y + dy, session.Position.Y, 1e-9, $"Size {percent}% vertical threshold.");
            if (dx == 0 && dy == 0) Assert.AreEqual(PetAction.Idle, session.Presentation.Action);
        }
    }

    [TestMethod]
    public void ShrinkingDuringRoamingRestsImmediatelyAndEnlargingResumesWithoutChangingTheSetting()
    {
        var session = CreateSession(new PetBounds(100, 100, 480, 400), new PetPosition(244, 196));
        session.Advance(TimeSpan.FromSeconds(1));
        session.ResizeRoamingArea(200, 216, persist: false);
        var start = session.Position;
        Assert.AreEqual(PetAction.Idle, session.Presentation.Action);
        session.Advance(TimeSpan.FromSeconds(2));
        Assert.AreEqual(start, session.Position);
        session.ResizeRoamingArea(480, 400, persist: false);
        session.Advance(TimeSpan.FromSeconds(1));
        Assert.AreNotEqual(start, session.Position);
        Assert.IsTrue(session.Configuration.Behavior.Roaming);
    }

    [TestMethod]
    public void ScaledFractionalEdgeBouncesMatchLargeAndSmallTimeUpdates()
    {
        for (var percent = 50; percent <= 200; percent += 5)
        {
            var scale = percent / 100d;
            var width = 384 / (2 / scale);
            var height = 416 / (2 / scale);
            var area = new PetBounds(100.1, 100.1, width + 36.1, height + 36.1);
            var single = CreateSession(area, new PetPosition(100.2, 100.2), new SequenceRandomSource(1, 0, 0), scale: scale);
            var sliced = CreateSession(area, new PetPosition(100.2, 100.2), new SequenceRandomSource(1, 0, 0), scale: scale);
            single.Advance(TimeSpan.FromSeconds(8));
            for (var step = 0; step < 80; step++) sliced.Advance(TimeSpan.FromMilliseconds(100));
            Assert.AreEqual(single.Position.X, sliced.Position.X, 1e-5);
            Assert.AreEqual(single.Position.Y, sliced.Position.Y, 1e-5);
            Assert.AreEqual(single.Needs.Boredom, sliced.Needs.Boredom, 1e-6);
            Assert.AreEqual(PetAction.Idle, single.Presentation.Action);
            Assert.AreEqual(PetAction.Idle, sliced.Presentation.Action);
        }
    }

    [TestMethod]
    [DataRow(0, 0, -18d, 0d)]
    [DataRow(0, 1, 18d, 0d)]
    [DataRow(2, 0, 0d, -18d)]
    [DataRow(2, 1, 0d, 18d)]
    public void Advance_ChoosesBothAxesAndDirectionsWithoutRelievingBoredom(
        int axisSample, int directionSample, double dx, double dy)
    {
        var session = CreateSession(new PetBounds(100, 100, 480, 400), new PetPosition(244, 196),
            new SequenceRandomSource(axisSample, directionSample));

        session.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(new PetPosition(244 + dx, 196 + dy), session.Position);
        Assert.AreEqual(20, session.Needs.Boredom, 1e-9);
        Assert.IsTrue(session.RequiresAnimationFrames, "Movement animates even with idle animations disabled.");
    }

    [TestMethod]
    [DataRow(208d)]
    [DataRow(225.9d)]
    public void Advance_ShallowAreaKeepsRoamingHorizontal(double height)
    {
        var session = CreateSession(new PetBounds(100, 100, 480, height), new PetPosition(244, 100));

        for (var second = 0; second < 100; second++)
        {
            session.Advance(TimeSpan.FromSeconds(1));
            Assert.AreEqual(100, session.Position.Y);
            AssertContained(session);
        }

        Assert.AreNotEqual(244, session.Position.X);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Advance_MinimumSizedAxisKeepsItsFractionalCoordinateWhileTheOtherAxisRoams(bool vertical)
    {
        var position = vertical
            ? new PetPosition(Math.BitIncrement(896), 100)
            : new PetPosition(100, Math.BitIncrement(832));
        var area = new PetBounds(position.X, position.Y, vertical ? 192 : 480, vertical ? 400 : 208);
        var session = CreateSession(area, position);

        session.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(vertical ? position.X : position.X + 18, session.Position.X);
        Assert.AreEqual(vertical ? position.Y + 18 : position.Y, session.Position.Y);
        AssertContained(session);
    }

    [TestMethod]
    [DataRow(192d, 226d)]
    [DataRow(192d, 400d)]
    [DataRow(480d, 226d)]
    public void Advance_VerticalRoamingFitsEvenWithoutHorizontalSpace(double width, double height)
    {
        var session = CreateSession(new PetBounds(100, 100, width, height), new PetPosition(100, 100),
            new SequenceRandomSource(2, 0));

        session.Advance(TimeSpan.FromMilliseconds(500));

        Assert.AreEqual(new PetPosition(100, 109), session.Position);
        Assert.AreEqual(20, session.Needs.Boredom, 1e-9);
        AssertContained(session);
    }

    [TestMethod]
    [DataRow(100d, 127d, PetAction.RunLeft)]
    [DataRow(136d, 109d, PetAction.RunRight)]
    public void Advance_VerticalRoamingBouncesAtBothEdges(double y, double expectedY, PetAction expectedAction)
    {
        var session = CreateSession(new PetBounds(100, 100, 192, 244), new PetPosition(100, y));

        session.Advance(TimeSpan.FromSeconds(2.5));

        Assert.AreEqual(expectedY, session.Position.Y, 1e-9);
        Assert.AreEqual(expectedAction, session.Presentation.Action);
        Assert.AreEqual(20, session.Needs.Boredom, 1e-9);
        for (var second = 0; second < 100; second++)
        {
            session.Advance(TimeSpan.FromSeconds(1));
            AssertContained(session);
        }
    }

    [TestMethod]
    public void Advance_VerticalRoamingKeepsEightSecondBoutsWithBoredomScaledRest()
    {
        var session = CreateSession(new PetBounds(100, 100, 192, 400), new PetPosition(100, 100));
        session.Advance(TimeSpan.FromSeconds(8));
        var restedPosition = session.Position;
        Assert.AreEqual(PetAction.Idle, session.Presentation.Action);
        Assert.AreEqual(20, session.Needs.Boredom, 1e-9);

        var restSeconds = 40 / (0.1 + 1.1 * 20 / 100) - 8;
        session.Advance(TimeSpan.FromSeconds(restSeconds));
        Assert.AreEqual(restedPosition, session.Position);
        Assert.AreEqual(20, session.Needs.Boredom, 1e-9);
        session.Advance(TimeSpan.FromSeconds(1));
        Assert.AreNotEqual(restedPosition, session.Position);
    }

    [TestMethod]
    [DataRow(0d, 392d)]
    [DataRow(25d, 98.6666666667d)]
    [DataRow(50d, 53.5384615385d)]
    [DataRow(100d, 25.3333333333d)]
    public void Advance_BoredomScalesBoutFrequencyFromTenToOneHundredTwentyPercent(
        double boredom, double restSeconds)
    {
        var session = CreateSession(new PetBounds(100, 100, 192, 400), new PetPosition(100, 100),
            boredom: boredom, needs: HoodConfiguration.Defaults.Needs with
            {
                HungerPointsPerHour = 0, BoredomPointsPerHour = 0, SleepPointsPerHour = 0
            });

        session.Advance(TimeSpan.FromSeconds(8));
        Assert.AreEqual(new PetPosition(100, 244), session.Position);
        Assert.AreEqual(PetAction.Idle, session.Presentation.Action);
        var restingPosition = session.Position;
        session.Advance(TimeSpan.FromSeconds(restSeconds - 0.001));
        Assert.AreEqual(restingPosition, session.Position);
        Assert.AreEqual(PetAction.Idle, session.Presentation.Action);

        session.Advance(TimeSpan.FromMilliseconds(2));
        Assert.AreNotEqual(restingPosition, session.Position);
        Assert.IsTrue(session.Presentation.Action is PetAction.RunLeft or PetAction.RunRight);
        Assert.AreEqual(boredom, session.Needs.Boredom);
    }

    [TestMethod]
    [DataRow(3600d, 60)]
    [DataRow(36000d, 36)]
    public void Advance_RisingBoredomShortensTheCurrentRestIndependentOfTimeChunks(double boredomPerHour, int seconds)
    {
        var needs = HoodConfiguration.Defaults.Needs with
        {
            HungerPointsPerHour = 0, BoredomPointsPerHour = boredomPerHour,
            SleepPointsPerHour = 0
        };
        var area = new PetBounds(100, 100, 192, 400);
        var position = new PetPosition(100, 100);
        var single = CreateSession(area, position, boredom: 0, needs: needs);
        var sliced = CreateSession(area, position, boredom: 0, needs: needs);
        single.Advance(TimeSpan.FromSeconds(seconds));
        for (var step = 0; step < seconds * 10; step++)
        {
            sliced.Advance(TimeSpan.FromMilliseconds(100));
        }

        Assert.IsTrue(single.Presentation.Action is PetAction.RunLeft or PetAction.RunRight,
            "Increasing boredom must bring the next bout forward during the current rest.");
        Assert.AreEqual(single.Presentation.Action, sliced.Presentation.Action);
        Assert.AreEqual(single.Position.Y, sliced.Position.Y, 1e-5);
        Assert.AreEqual(single.Needs.Boredom, sliced.Needs.Boredom, 1e-5);
    }

    [TestMethod]
    [DataRow(16d, 25.334d)]
    [DataRow(200d, 25.334d)]
    public void Advance_SaturatedBoredomKeepsTheNextBoutIndependentOfTimeChunks(
        double boredomPerHour, double restSeconds)
    {
        var needs = HoodConfiguration.Defaults.Needs with
        {
            HungerPointsPerHour = 0, BoredomPointsPerHour = boredomPerHour, SleepPointsPerHour = 0
        };
        var area = new PetBounds(100, 100, 192, 400);
        var position = new PetPosition(100, 100);
        var single = CreateSession(area, position, boredom: 100, needs: needs);
        var sliced = CreateSession(area, position, boredom: 100, needs: needs);
        single.Advance(TimeSpan.FromSeconds(8));
        for (var step = 0; step < 80; step++)
        {
            sliced.Advance(TimeSpan.FromMilliseconds(100));
        }

        single.Advance(TimeSpan.FromSeconds(restSeconds));
        sliced.Advance(TimeSpan.FromSeconds(restSeconds));

        Assert.AreEqual(sliced.Presentation.Action, single.Presentation.Action,
            "Equivalent elapsed time must start the next roaming bout at the same time.");
        Assert.IsTrue(single.Presentation.Action is PetAction.RunLeft or PetAction.RunRight);
        Assert.AreEqual(sliced.Position.Y, single.Position.Y, 1e-5);
        Assert.AreEqual(sliced.Needs.Boredom, single.Needs.Boredom, 1e-8);
    }

    [TestMethod]
    [DataRow(100d, 16d, 100d)]
    [DataRow(99d, 3600d, 100d)]
    [DataRow(0d, 16d, 0.0355555556d)]
    [DataRow(1d, 16d, 1.0355555556d)]
    public void Advance_MovementDoesNotRelieveBoredomAndGrowthClampsAt100(
        double initialBoredom, double boredomPerHour, double expectedBoredom)
    {
        var needs = HoodConfiguration.Defaults.Needs with
        {
            HungerPointsPerHour = 0, BoredomPointsPerHour = boredomPerHour, SleepPointsPerHour = 0
        };
        var area = new PetBounds(100, 100, 192, 400);
        var position = new PetPosition(100, 100);
        var single = CreateSession(area, position, boredom: initialBoredom, needs: needs);
        var sliced = CreateSession(area, position, boredom: initialBoredom, needs: needs);
        single.Advance(TimeSpan.FromSeconds(8));
        for (var step = 0; step < 80; step++)
        {
            sliced.Advance(TimeSpan.FromMilliseconds(100));
        }

        Assert.AreEqual(expectedBoredom, single.Needs.Boredom, 1e-8);
        Assert.AreEqual(expectedBoredom, sliced.Needs.Boredom, 1e-8);
        Assert.AreEqual(single.Position.X, sliced.Position.X, 1e-5);
        Assert.AreEqual(single.Position.Y, sliced.Position.Y, 1e-5);
        Assert.AreEqual(single.Presentation.Action, sliced.Presentation.Action);
    }

    [TestMethod]
    public void Advance_BoredomScaledRestPausesDuringDraggingEditingActionsAndSleep()
    {
        var session = CreateSession(new PetBounds(100, 100, 192, 400), new PetPosition(100, 100),
            boredom: 0);
        session.Advance(TimeSpan.FromSeconds(8));
        var position = session.Position;
        session.BeginDragging();
        session.Advance(TimeSpan.FromSeconds(400));
        session.EndDragging();
        session.BeginAreaEditing();
        session.Advance(TimeSpan.FromSeconds(400));
        session.EndAreaEditing();
        session.React();
        session.Advance(TimeSpan.FromMilliseconds(1200));
        session.Sleep();
        session.Advance(TimeSpan.FromSeconds(1));
        session.React();
        session.Advance(TimeSpan.FromMilliseconds(900));

        session.Advance(TimeSpan.FromSeconds(392));
        Assert.AreEqual(position, session.Position);
        Assert.AreEqual(PetAction.Idle, session.Presentation.Action);
        session.Advance(TimeSpan.FromSeconds(1));
        Assert.AreNotEqual(position, session.Position);
    }

    [TestMethod]
    public void Resize_ChangesTheActiveAxisWhenItsTravelSpaceDisappears()
    {
        var session = CreateSession(new PetBounds(100, 100, 480, 400), new PetPosition(100, 100),
            new SequenceRandomSource(2, 0));
        session.Advance(TimeSpan.FromSeconds(1));
        session.ResizeRoamingArea(480, 208);
        var start = session.Position;

        session.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(start.Y, session.Position.Y);
        Assert.AreEqual(start.X + 18, session.Position.X);
        AssertContained(session);

        session.ResizeRoamingArea(192, 400);
        start = session.Position;
        session.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(start.X, session.Position.X);
        Assert.AreEqual(18, Math.Abs(session.Position.Y - start.Y), 1e-9);
        AssertContained(session);
    }

    [TestMethod]
    public void Advance_VerticalRoamingPausesForDraggingEditingActionsAndSleep()
    {
        var session = CreateSession(new PetBounds(100, 100, 192, 400), new PetPosition(100, 100));
        session.Advance(TimeSpan.FromSeconds(1));
        var start = session.Position;

        session.BeginDragging();
        session.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(start, session.Position);
        Assert.AreEqual(20, session.Needs.Boredom, 1e-9);
        session.EndDragging();
        session.BeginAreaEditing();
        session.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(start, session.Position);
        session.EndAreaEditing();
        session.React();
        session.Advance(TimeSpan.FromMilliseconds(1200));
        Assert.AreEqual(start, session.Position);
        session.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(18, Math.Abs(session.Position.Y - start.Y), 1e-9);

        start = session.Position;
        session.Sleep();
        session.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(start, session.Position);
        session.React();
        session.Advance(TimeSpan.FromMilliseconds(900));
        Assert.AreEqual(start, session.Position);
        session.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(18, Math.Abs(session.Position.Y - start.Y), 1e-9);
    }

    [TestMethod]
    public void Advance_DisabledRoamingStaysStillDespiteVerticalSpace()
    {
        var session = CreateSession(new PetBounds(100, 100, 192, 400), new PetPosition(100, 100), roaming: false);

        session.Advance(TimeSpan.FromSeconds(60));

        Assert.AreEqual(new PetPosition(100, 100), session.Position);
        Assert.AreEqual(20, session.Needs.Boredom);
        Assert.AreEqual(PetAction.Idle, session.Presentation.Action);
    }

    [TestMethod]
    [DataRow(0, 0, -1d, -1d)]
    [DataRow(0, 1, -1d, 1d)]
    [DataRow(1, 0, 1d, -1d)]
    [DataRow(1, 1, 1d, 1d)]
    public void Advance_RunsInAllFourDiagonalDirectionsAtTheNormalSpeed(
        int xSample, int ySample, double dx, double dy)
    {
        var session = CreateSession(new PetBounds(100, 100, 480, 400), new PetPosition(244, 196),
            new SequenceRandomSource(1, xSample, ySample));

        session.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(244 + dx * 18 / Math.Sqrt(2), session.Position.X, 1e-9);
        Assert.AreEqual(196 + dy * 18 / Math.Sqrt(2), session.Position.Y, 1e-9);
        Assert.AreEqual(20, session.Needs.Boredom, 1e-9);
        Assert.AreEqual(dx < 0 ? PetAction.RunLeft : PetAction.RunRight, session.Presentation.Action);
        AssertContained(session);
    }

    [TestMethod]
    [DataRow(101d, 196d, 0, 1, 1d, 1d)]
    [DataRow(387d, 196d, 1, 0, -1d, -1d)]
    [DataRow(244d, 101d, 1, 0, 1d, 1d)]
    [DataRow(244d, 291d, 0, 1, -1d, -1d)]
    [DataRow(101d, 101d, 0, 0, 1d, 1d)]
    public void Advance_DiagonalTravelReflectsOnlyTheAxisThatHitsAnEdge(
        double x, double y, int xSample, int ySample, double expectedDx, double expectedDy)
    {
        var session = CreateSession(new PetBounds(100, 100, 480, 400), new PetPosition(x, y),
            new SequenceRandomSource(1, xSample, ySample));
        session.Advance(TimeSpan.FromSeconds(1));
        AssertContained(session);
        var afterBounce = session.Position;

        session.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(afterBounce.X + expectedDx * 18 / Math.Sqrt(2), session.Position.X, 1e-9);
        Assert.AreEqual(afterBounce.Y + expectedDy * 18 / Math.Sqrt(2), session.Position.Y, 1e-9);
        Assert.AreEqual(20, session.Needs.Boredom, 1e-6);
        Assert.AreEqual(expectedDx < 0 ? PetAction.RunLeft : PetAction.RunRight, session.Presentation.Action);
        AssertContained(session);
    }

    [TestMethod]
    public void Advance_DiagonalEdgeBouncesAreIndependentOfElapsedTimeChunks()
    {
        var area = new PetBounds(100, 100, 228, 244);
        var position = new PetPosition(101, 101);
        var single = CreateSession(area, position, new SequenceRandomSource(1, 0, 0));
        var sliced = CreateSession(area, position, new SequenceRandomSource(1, 0, 0));
        single.Advance(TimeSpan.FromSeconds(8));
        for (var step = 0; step < 80; step++)
        {
            sliced.Advance(TimeSpan.FromMilliseconds(100));
            AssertContained(sliced);
        }

        Assert.AreEqual(single.Position.X, sliced.Position.X, 1e-5);
        Assert.AreEqual(single.Position.Y, sliced.Position.Y, 1e-5);
        Assert.AreEqual(single.Needs.Boredom, sliced.Needs.Boredom, 1e-6);
        Assert.AreEqual(PetAction.Idle, single.Presentation.Action);
        Assert.AreEqual(PetAction.Idle, sliced.Presentation.Action);
    }

    [TestMethod]
    public void Advance_DiagonalRoamingPausesForEditingDraggingAndDirectActions()
    {
        var session = CreateSession(new PetBounds(100, 100, 480, 400), new PetPosition(244, 196),
            new SequenceRandomSource(1, 1, 1));
        session.Advance(TimeSpan.FromSeconds(1));
        var start = session.Position;
        session.BeginDragging();
        session.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(start, session.Position);
        session.EndDragging();
        session.BeginAreaEditing();
        session.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(start, session.Position);
        session.EndAreaEditing();
        session.React();
        session.Advance(TimeSpan.FromMilliseconds(1200));
        Assert.AreEqual(start, session.Position);

        session.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(start.X + 18 / Math.Sqrt(2), session.Position.X, 1e-9);
        Assert.AreEqual(start.Y + 18 / Math.Sqrt(2), session.Position.Y, 1e-9);
        Assert.AreEqual(20, session.Needs.Boredom, 1e-9);
    }
    [TestMethod]
    [DataRow(480d, 208d, 18d, 0d)]
    [DataRow(192d, 400d, 0d, 18d)]
    [DataRow(192d, 208d, 0d, 0d)]
    public void Resize_DiagonalTravelUsesOnlyTheRemainingSpaceAtNormalSpeed(
        double width, double height, double dx, double dy)
    {
        var session = CreateSession(new PetBounds(100, 100, 480, 400), new PetPosition(244, 196),
            new SequenceRandomSource(1, 1, 1));
        session.Advance(TimeSpan.FromSeconds(1));
        session.ResizeRoamingArea(width, height);
        var start = session.Position;
        var boredom = session.Needs.Boredom;

        session.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(start.X + dx, session.Position.X, 1e-9);
        Assert.AreEqual(start.Y + dy, session.Position.Y, 1e-9);
        Assert.AreEqual(boredom, session.Needs.Boredom, 1e-9);
        AssertContained(session);
    }

    private static void AssertContained(HoodSession session)
    {
        Assert.IsTrue(session.Position.X >= session.RoamingArea.X);
        Assert.IsTrue(session.Position.X + 192 <= session.RoamingArea.Right);
        Assert.IsTrue(session.Position.Y >= session.RoamingArea.Y);
        Assert.IsTrue(session.Position.Y + 208 <= session.RoamingArea.Bottom);
    }

    private static HoodSession CreateSession(PetBounds area, PetPosition position,
        IPetRandomSource? random = null, bool roaming = true, double boredom = 20, NeedSettings? needs = null,
        double scale = 1)
    {
        var assetDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Hood");
        var png = File.ReadAllBytes(Path.Combine(assetDirectory, "hood_spritesheet.png"));
        var definition = PetDefinition.Parse(File.ReadAllText(Path.Combine(assetDirectory, "hood.animations.json")),
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)),
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
        var defaults = HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            PetScale = scale,
            Behavior = defaults.Behavior with { Roaming = roaming, IdleAnimations = false, RandomEvents = false },
            Needs = needs ?? defaults.Needs with { HungerPointsPerHour = 0, BoredomPointsPerHour = 0, SleepPointsPerHour = 0 }
        };
        return HoodSession.Start(definition,
            [new DisplayInfo("Primary", new PetBounds(0, 0, 1920, 1040), true)],
            random ?? new SequenceRandomSource(0), new NoopSessionStore(), configuration,
            new HoodSessionSnapshot { Position = position, RoamingArea = area, Needs = new NeedLevels(0, boredom, 0) });
    }

    private sealed class SequenceRandomSource(params int[] values) : IPetRandomSource
    {
        private int _index;
        public int NextIndex(int exclusiveUpperBound)
        {
            var value = values[Math.Min(_index++, values.Length - 1)];
            if (value < 0 || value >= exclusiveUpperBound)
            {
                throw new InvalidOperationException("The test random value is outside the allowed range.");
            }

            return value;
        }
    }

    private sealed class NoopSessionStore : ISessionStore
    {
        public HoodSessionSnapshot? Load() => null;
        public void Save(HoodSessionSnapshot snapshot) { }
        public string? ResetAfterAcknowledgement() => null;
    }
}
