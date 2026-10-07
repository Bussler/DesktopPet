namespace DesktopPet.Tests;

[TestClass]
public sealed class HoodSessionTests
{
    [TestMethod]
    public void Start_UsesThePrimaryWorkAreaAndControlledIdleFrame()
    {
        var session = CreateSession(new SequenceRandomSource(2), new RecordingSessionStore());

        Assert.AreEqual(new DesktopPet.Core.PetPosition(864, 816), session.Position);
        Assert.AreEqual(DesktopPet.Core.PetAction.Idle, session.Presentation.Action);
        Assert.AreEqual(2, session.Presentation.FrameIndex);
    }

    [TestMethod]
    public void Start_LoadsCompleteFirstReleaseAnimationsWithExpectedPlaybackRules()
    {
        var definition = ReadDefinition();

        Assert.AreEqual(Enum.GetValues<DesktopPet.Core.PetAction>().Length, definition.Animations.Count);
        Assert.IsTrue(definition.GetAnimation(DesktopPet.Core.PetAction.Idle).Loops);
        Assert.IsTrue(definition.GetAnimation(DesktopPet.Core.PetAction.Sleep).Loops);
        Assert.IsFalse(definition.GetAnimation(DesktopPet.Core.PetAction.Feed).Loops);
        Assert.IsFalse(definition.GetAnimation(DesktopPet.Core.PetAction.Wake).Loops);
        Assert.IsFalse(definition.GetAnimation(DesktopPet.Core.PetAction.LookUp).Loops);
        Assert.IsFalse(definition.GetAnimation(DesktopPet.Core.PetAction.LookAround).Loops);
    }

    [TestMethod]
    public void Assets_HigherResolutionAndAdditionalPosesKeepTheSameLogicalSize()
    {
        var definition = ReadDefinition();

        Assert.AreEqual(384, definition.FrameWidth);
        Assert.AreEqual(416, definition.FrameHeight);
        Assert.AreEqual(192d, definition.DisplayWidth);
        Assert.AreEqual(208d, definition.DisplayHeight);
        Assert.AreEqual(new DesktopPet.Core.PetBounds(0, 0, 192, 208), definition.MinimumRoamingArea);
        foreach (var animation in definition.Animations)
        {
            Assert.IsTrue(animation.Frames.Distinct().Count() >= 12, $"{animation.Action} needs additional poses.");
        }
    }

    [TestMethod]
    public void Assets_LegacyManifestsWithoutSpriteScaleRetainTheirOriginalSize()
    {
        var (json, width, height) = ReadPackagedDefinition();
        var root = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        root.Remove("spriteScale");
        root["frameWidth"] = 192;
        root["frameHeight"] = 208;

        var definition = DesktopPet.Core.PetDefinition.Parse(root.ToJsonString(), width, height);

        Assert.AreEqual(1d, definition.SpriteScale);
        Assert.AreEqual(192d, definition.DisplayWidth);
        Assert.AreEqual(208d, definition.DisplayHeight);
    }

    [TestMethod]
    [DataRow(0d)]
    [DataRow(-1d)]
    public void Assets_RejectInvalidSpriteScale(double scale)
    {
        var (json, width, height) = ReadPackagedDefinition();
        var root = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        root["spriteScale"] = scale;

        Assert.Throws<DesktopPet.Core.PetAssetException>(
            () => DesktopPet.Core.PetDefinition.Parse(root.ToJsonString(), width, height));
    }

    [TestMethod]
    public void Advance_ContentPetStillRoamsWithEightSecondBouts()
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Behavior = defaults.Behavior with { Roaming = true, IdleAnimations = false, RandomEvents = false },
            Needs = defaults.Needs with { HungerPointsPerHour = 0, BoredomPointsPerHour = 0, SleepPointsPerHour = 0 }
        };
        var session = CreateSession(new SequenceRandomSource(0), new RecordingSessionStore(), configuration);
        var starts = 0;
        var wasMoving = false;
        for (var second = 0; second < 800; second++)
        {
            session.Advance(TimeSpan.FromSeconds(1));
            var moving = session.Presentation.Action is DesktopPet.Core.PetAction.RunLeft or DesktopPet.Core.PetAction.RunRight;
            if (moving && !wasMoving)
            {
                starts++;
            }

            Assert.AreEqual(second % 400 < 7, moving, "At zero boredom, bouts occur at 10% of the old frequency.");
            wasMoving = moving;
        }

        Assert.AreEqual(2, starts, "A content pet must still roam once per 400 seconds.");
    }

    [TestMethod]
    public void Advance_IdleRepeatsTwentyPercentMoreOftenWithoutSpeedingTheBlink()
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Behavior = defaults.Behavior with { Roaming = false, IdleAnimations = true, RandomEvents = false }
        };
        var session = CreateSession(new SequenceRandomSource(0), new RecordingSessionStore(), configuration);
        var idle = ReadDefinition().GetAnimation(DesktopPet.Core.PetAction.Idle);
        var neutral = idle.Frames[session.Presentation.FrameIndex];

        for (var cycle = 0; cycle < 3; cycle++)
        {
            session.Advance(TimeSpan.FromMilliseconds(7760));
            Assert.AreEqual(neutral, idle.Frames[session.Presentation.FrameIndex]);
            session.Advance(TimeSpan.FromMilliseconds(80));
            for (var column = 1; column <= 15; column++)
            {
                Assert.AreEqual(new DesktopPet.Core.SpriteFrame(column, 0), idle.Frames[session.Presentation.FrameIndex],
                    "Each blink pose must retain its original 80ms playback interval.");
                session.Advance(TimeSpan.FromMilliseconds(80));
            }

            Assert.AreEqual(neutral, idle.Frames[session.Presentation.FrameIndex]);
            Assert.AreEqual(DesktopPet.Core.PetAction.Idle, session.Presentation.Action);
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(22)]
    [DataRow(120)]
    [DataRow(135)]
    public void Start_PreservesSavedIdleFramesWhileShorteningOnlyTheStillPause(int frame)
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Behavior = defaults.Behavior with { Roaming = false, RandomEvents = false }
        };
        var store = new RecordingSessionStore();
        var session = CreateSession(new SequenceRandomSource(0), store, configuration,
            new DesktopPet.Core.HoodSessionSnapshot
            {
                Position = new DesktopPet.Core.PetPosition(864, 816), FrameIndex = frame
            });
        Assert.AreEqual(frame, session.Presentation.FrameIndex);
        session.SaveCurrentState();
        var resumed = CreateSession(new SequenceRandomSource(0), store, configuration, store.Snapshot);
        Assert.AreEqual(session.Presentation, resumed.Presentation);

        session.Advance(TimeSpan.FromMilliseconds(80));
        var idle = ReadDefinition().GetAnimation(DesktopPet.Core.PetAction.Idle);
        var expected = frame < 120 || frame == 135 ? idle.Frames[0] : idle.Frames[121];
        Assert.AreEqual(expected, idle.Frames[session.Presentation.FrameIndex]);
    }

    [TestMethod]
    public void Advance_CompletesTheJumpAndReturnsToAnIdleFrame()
    {
        var session = CreateSession(new SequenceRandomSource(1, 4), new RecordingSessionStore());

        session.React();
        session.Advance(TimeSpan.FromMilliseconds(1199));

        Assert.AreEqual(DesktopPet.Core.PetAction.Jump, session.Presentation.Action);
        Assert.AreEqual(ReadDefinition().GetAnimation(DesktopPet.Core.PetAction.Jump).Frames.Count - 1, session.Presentation.FrameIndex);

        session.Advance(TimeSpan.FromMilliseconds(1));

        Assert.AreEqual(DesktopPet.Core.PetAction.Idle, session.Presentation.Action);
        Assert.AreEqual(4, session.Presentation.FrameIndex);
    }

    [TestMethod]
    public void Advance_ProgressesConfiguredNeedsWhileRoamingAndIdleAnimationsAreDisabled()
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Behavior = defaults.Behavior with { Roaming = false, IdleAnimations = false },
            Needs = defaults.Needs with
            {
                HungerPointsPerHour = 12.5,
                BoredomPointsPerHour = 16,
                SleepPointsPerHour = 20
            }
        };
        var session = CreateSession(new SequenceRandomSource(0), new RecordingSessionStore(), configuration);

        session.Advance(TimeSpan.FromMinutes(30));

        Assert.AreEqual(new DesktopPet.Core.NeedLevels(6.25, 8, 10), session.Needs);
        Assert.AreEqual(DesktopPet.Core.HoodSessionMode.Awake, session.Mode);
    }

    [TestMethod]
    public void Advance_SaturatesNeedsWithoutPunitiveEffects()
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Behavior = defaults.Behavior with { Roaming = false },
            Needs = defaults.Needs with
            {
                HungerPointsPerHour = 12.5,
                BoredomPointsPerHour = 16,
                SleepPointsPerHour = 0
            }
        };
        var saved = new DesktopPet.Core.HoodSessionSnapshot
        {
            Position = new DesktopPet.Core.PetPosition(0, 0),
            Needs = new DesktopPet.Core.NeedLevels(99, 99, 0)
        };
        var session = CreateSession(new SequenceRandomSource(0), new RecordingSessionStore(), configuration, saved);

        session.Advance(TimeSpan.FromHours(1));

        Assert.AreEqual(new DesktopPet.Core.NeedLevels(100, 100, 0), session.Needs);
        Assert.AreEqual(DesktopPet.Core.HoodSessionMode.Awake, session.Mode);
        Assert.IsFalse(session.QuitRequested);
    }

    [TestMethod]
    public void Advance_RoamsWithinBoundsAndRelievesBoredomOnlyForMovementTime()
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Behavior = defaults.Behavior with { Roaming = true, IdleAnimations = false },
            Needs = defaults.Needs with
            {
                HungerPointsPerHour = 0,
                BoredomPointsPerHour = 0,
                SleepPointsPerHour = 0,
                MovementReliefPerSecond = 0.5
            }
        };
        var snapshot = new DesktopPet.Core.HoodSessionSnapshot
        {
            Position = new DesktopPet.Core.PetPosition(864, 816),
            Needs = new DesktopPet.Core.NeedLevels(0, 20, 0),
            RoamingArea = new DesktopPet.Core.PetBounds(720, 816, 480, 208)
        };
        var session = CreateSession(new SequenceRandomSource(1), new RecordingSessionStore(), configuration, snapshot);

        session.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(new DesktopPet.Core.PetPosition(882, 816), session.Position);
        Assert.AreEqual(DesktopPet.Core.PetAction.RunRight, session.Presentation.Action);
        Assert.AreEqual(19.5, session.Needs.Boredom, 1e-9);
        Assert.IsTrue(session.Position.X >= session.RoamingArea.X);
        Assert.IsTrue(session.Position.X + 192 <= session.RoamingArea.Right);
    }

    [TestMethod]
    public void Advance_DoesNotRewardRoamingWhenNoMovementFitsTheArea()
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Needs = defaults.Needs with
            {
                HungerPointsPerHour = 0,
                BoredomPointsPerHour = 0,
                SleepPointsPerHour = 0,
                MovementReliefPerSecond = 0.5
            }
        };
        var snapshot = new DesktopPet.Core.HoodSessionSnapshot
        {
            Position = new DesktopPet.Core.PetPosition(864, 816),
            Needs = new DesktopPet.Core.NeedLevels(0, 20, 0),
            RoamingArea = new DesktopPet.Core.PetBounds(864, 816, 192, 208)
        };
        var session = CreateSession(new SequenceRandomSource(0), new RecordingSessionStore(), configuration, snapshot);

        session.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(new DesktopPet.Core.PetPosition(864, 816), session.Position);
        Assert.AreEqual(20, session.Needs.Boredom);
        Assert.AreEqual(DesktopPet.Core.PetAction.Idle, session.Presentation.Action);
    }

    [TestMethod]
    public void Advance_RoamingBouncesAtAnAreaEdgeWithoutLeavingTheBounds()
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Behavior = defaults.Behavior with { IdleAnimations = false },
            Needs = defaults.Needs with
            {
                HungerPointsPerHour = 0,
                BoredomPointsPerHour = 0,
                SleepPointsPerHour = 0
            }
        };
        var snapshot = new DesktopPet.Core.HoodSessionSnapshot
        {
            Position = new DesktopPet.Core.PetPosition(1000, 816),
            Needs = DesktopPet.Core.NeedLevels.Initial,
            RoamingArea = new DesktopPet.Core.PetBounds(720, 816, 480, 208)
        };
        var session = CreateSession(new SequenceRandomSource(1), new RecordingSessionStore(), configuration, snapshot);

        session.Advance(TimeSpan.FromSeconds(1));

        Assert.IsTrue(session.Position.X >= 720);
        Assert.IsTrue(session.Position.X + 192 <= 1200);
        Assert.AreEqual(DesktopPet.Core.PetAction.RunLeft, session.Presentation.Action);
    }

    [TestMethod]
    public void Advance_RoamingLeavesTimeForTheIdleAnimation()
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Behavior = defaults.Behavior with { Roaming = true, IdleAnimations = true, RandomEvents = false },
            Needs = defaults.Needs with { HungerPointsPerHour = 0, BoredomPointsPerHour = 0, SleepPointsPerHour = 0 }
        };
        var session = CreateSession(new SequenceRandomSource(0), new RecordingSessionStore(), configuration);
        var observedRoaming = false;
        var observedIdleAfterRoaming = false;

        for (var elapsed = TimeSpan.Zero; elapsed < TimeSpan.FromSeconds(30); elapsed += TimeSpan.FromMilliseconds(100))
        {
            session.Advance(TimeSpan.FromMilliseconds(100));
            if (session.Presentation.Action is DesktopPet.Core.PetAction.RunLeft or DesktopPet.Core.PetAction.RunRight)
            {
                observedRoaming = true;
            }
            else if (observedRoaming && session.Presentation.Action == DesktopPet.Core.PetAction.Idle)
            {
                observedIdleAfterRoaming = true;
                break;
            }
        }

        Assert.IsTrue(observedRoaming, "Roaming must still produce movement.");
        Assert.IsTrue(observedIdleAfterRoaming, "Enabled idle animation must appear between ordinary roaming bouts.");
        var idleFrame = session.Presentation.FrameIndex;
        session.Advance(TimeSpan.FromMilliseconds(1400));

        Assert.AreEqual(DesktopPet.Core.PetAction.Idle, session.Presentation.Action);
        Assert.AreNotEqual(idleFrame, session.Presentation.FrameIndex, "The idle animation should advance while Hood is resting.");
    }

    [TestMethod]
    public void IdleAnimationToggle_FreezesRestingFramesWithoutStoppingNeedsOrDirectReactions()
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Behavior = defaults.Behavior with { Roaming = false, IdleAnimations = false },
            Needs = defaults.Needs with { HungerPointsPerHour = 12.5, SleepPointsPerHour = 0 }
        };
        var session = CreateSession(new SequenceRandomSource(2), new RecordingSessionStore(), configuration);

        session.Advance(TimeSpan.FromHours(1));
        Assert.AreEqual(12.5, session.Needs.Hunger, 1e-9);
        Assert.AreEqual(2, session.Presentation.FrameIndex);
        Assert.IsFalse(session.RequiresAnimationFrames);

        session.React();
        Assert.AreEqual(DesktopPet.Core.PetAction.Jump, session.Presentation.Action);
        session.Advance(TimeSpan.FromMilliseconds(ReadDefinition().GetAnimation(DesktopPet.Core.PetAction.Jump).FrameDurationMilliseconds));
        Assert.AreEqual(1, session.Presentation.FrameIndex);
    }

    [TestMethod]
    public void Advance_RoamingPausesForDraggingEditingSleepAndDirectActionsThenResumes()
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Behavior = defaults.Behavior with { IdleAnimations = false },
            Needs = defaults.Needs with
            {
                HungerPointsPerHour = 0,
                BoredomPointsPerHour = 0,
                SleepPointsPerHour = 0
            }
        };
        var session = CreateSession(new SequenceRandomSource(1), new RecordingSessionStore(), configuration);
        session.Advance(TimeSpan.FromSeconds(1));
        var movingPosition = session.Position;
        Assert.AreNotEqual(new DesktopPet.Core.PetPosition(864, 816), movingPosition);

        session.BeginDragging();
        session.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(movingPosition, session.Position);
        session.EndDragging();
        session.BeginAreaEditing();
        session.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(movingPosition, session.Position);
        session.EndAreaEditing();

        session.Sleep();
        session.Advance(TimeSpan.FromMinutes(1));
        Assert.AreEqual(movingPosition, session.Position);
        Assert.AreEqual(DesktopPet.Core.HoodSessionMode.Sleeping, session.Mode);

        session.React();
        session.Advance(TimeSpan.FromMilliseconds(900));
        Assert.AreEqual(DesktopPet.Core.PetAction.Idle, session.Presentation.Action);
        session.Advance(TimeSpan.FromSeconds(1));
        Assert.AreNotEqual(movingPosition, session.Position);
    }

    [TestMethod]
    public void Advance_SamplesRandomIntervalsAtBothConfiguredEndpoints()
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Behavior = defaults.Behavior with { Roaming = false, IdleAnimations = false },
            Needs = defaults.Needs with { SleepPointsPerHour = 0 }
        };
        var minimum = CreateSession(new SequenceRandomSource(0, 0), new RecordingSessionStore(), configuration, enableRandomEvents: true);
        var maximum = CreateSession(new SequenceRandomSource(0, 1_000_000, 0), new RecordingSessionStore(), configuration, enableRandomEvents: true);

        minimum.Advance(TimeSpan.FromTicks(1));
        maximum.Advance(TimeSpan.FromTicks(1));

        Assert.AreEqual(TimeSpan.FromMinutes(1) - TimeSpan.FromTicks(1), minimum.RemainingRandomInterval);
        Assert.AreEqual(TimeSpan.FromMinutes(5) - TimeSpan.FromTicks(1), maximum.RemainingRandomInterval);
        minimum.Advance(minimum.RemainingRandomInterval);
        Assert.AreEqual(DesktopPet.Core.PetAction.Wave, minimum.Presentation.Action);
        maximum.Advance(maximum.RemainingRandomInterval);
        Assert.AreEqual(DesktopPet.Core.PetAction.Wave, maximum.Presentation.Action);
    }

    [TestMethod]
    public void Advance_UsesConfiguredMinutesDuringBothIdleAndRoaming()
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Needs = defaults.Needs with { SleepPointsPerHour = 0 },
            RandomInterval = defaults.RandomInterval with { MinimumMinutes = 1, MaximumMinutes = 1 }
        };
        var session = CreateSession(new SequenceRandomSource(0), new RecordingSessionStore(), configuration, enableRandomEvents: true);

        session.Advance(TimeSpan.FromSeconds(4));
        Assert.AreEqual(DesktopPet.Core.PetAction.RunLeft, session.Presentation.Action);
        Assert.AreEqual(TimeSpan.FromSeconds(56), session.RemainingRandomInterval);

        session.Advance(TimeSpan.FromSeconds(4));
        Assert.AreEqual(DesktopPet.Core.PetAction.Idle, session.Presentation.Action);
        Assert.AreEqual(TimeSpan.FromSeconds(52), session.RemainingRandomInterval);
        session.Advance(TimeSpan.FromSeconds(5));
        Assert.AreEqual(DesktopPet.Core.PetAction.Idle, session.Presentation.Action);
        Assert.AreEqual(TimeSpan.FromSeconds(47), session.RemainingRandomInterval);
    }

    [TestMethod]
    [DataRow(0, DesktopPet.Core.PetAction.Wave, 936)]
    [DataRow(1, DesktopPet.Core.PetAction.LookAround, 2288)]
    [DataRow(3, DesktopPet.Core.PetAction.LookUp, 720)]
    public void Advance_SelectsIdleGesturesAndReturnsToRest(int choice, DesktopPet.Core.PetAction expected, int durationMilliseconds)
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Behavior = defaults.Behavior with { Roaming = false },
            RandomInterval = defaults.RandomInterval with { MinimumMinutes = 1, MaximumMinutes = 1 }
        };
        var session = CreateSession(new SequenceRandomSource(0, choice), new RecordingSessionStore(), configuration, enableRandomEvents: true);
        var position = session.Position;

        session.Advance(TimeSpan.FromMilliseconds(59_999));
        Assert.AreEqual(DesktopPet.Core.PetAction.Idle, session.Presentation.Action);
        session.Advance(TimeSpan.FromMilliseconds(1));
        Assert.AreEqual(expected, session.Presentation.Action);
        Assert.AreEqual(position, session.Position);

        var animation = ReadDefinition().GetAnimation(expected);
        session.Advance(TimeSpan.FromMilliseconds(durationMilliseconds - 1));
        Assert.AreEqual(expected, session.Presentation.Action);
        Assert.AreEqual(animation.Frames.Count - 1, session.Presentation.FrameIndex);
        session.Advance(TimeSpan.FromMilliseconds(1));
        Assert.AreEqual(DesktopPet.Core.PetAction.Idle, session.Presentation.Action);
        Assert.AreEqual(TimeSpan.FromMinutes(1), session.RemainingRandomInterval);
        Assert.AreEqual(position, session.Position);
    }

    [TestMethod]
    public void Advance_FiresFittingRandomReactionAndDirectInputTakesPrecedence()
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Behavior = defaults.Behavior with { Roaming = false, IdleAnimations = false },
            Needs = defaults.Needs with { HungerPointsPerHour = 0, BoredomPointsPerHour = 0, SleepPointsPerHour = 0 },
            RandomInterval = defaults.RandomInterval with { MinimumMinutes = 1d / 60, MaximumMinutes = 1d / 60 }
        };
        var session = CreateSession(new SequenceRandomSource(0, 1), new RecordingSessionStore(), configuration, enableRandomEvents: true);

        session.Advance(TimeSpan.FromMilliseconds(999));
        Assert.AreEqual(DesktopPet.Core.PetAction.Idle, session.Presentation.Action);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1), session.RemainingRandomInterval);

        session.Advance(TimeSpan.FromMilliseconds(1));
        Assert.AreEqual(DesktopPet.Core.PetAction.LookAround, session.Presentation.Action);
        Assert.AreEqual(TimeSpan.FromSeconds(1), session.RemainingRandomInterval);

        session.React();
        session.Advance(TimeSpan.FromMilliseconds(1200));
        Assert.AreEqual(DesktopPet.Core.PetAction.Idle, session.Presentation.Action);
        Assert.AreEqual(TimeSpan.FromSeconds(1), session.RemainingRandomInterval);

        session.Advance(TimeSpan.FromMilliseconds(999));
        Assert.AreEqual(DesktopPet.Core.PetAction.Idle, session.Presentation.Action);
        session.Advance(TimeSpan.FromMilliseconds(1));
        Assert.AreEqual(DesktopPet.Core.PetAction.LookAround, session.Presentation.Action);
        var footprint = ReadDefinition().GetAnimation(session.Presentation.Action).MotionBounds;
        Assert.IsTrue(footprint.Width <= session.RoamingArea.Width);
        Assert.IsTrue(footprint.Height <= session.RoamingArea.Height);
    }

    [TestMethod]
    public void Advance_CanChooseTheFittingRandomHopWhenIdleAnimationsAreDisabled()
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Behavior = defaults.Behavior with { Roaming = false, IdleAnimations = false },
            Needs = defaults.Needs with { HungerPointsPerHour = 0, BoredomPointsPerHour = 0, SleepPointsPerHour = 0 },
            RandomInterval = defaults.RandomInterval with { MinimumMinutes = 1d / 60, MaximumMinutes = 1d / 60 }
        };
        var session = CreateSession(new SequenceRandomSource(0, 2), new RecordingSessionStore(), configuration, enableRandomEvents: true);

        session.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(DesktopPet.Core.PetAction.Jump, session.Presentation.Action);
        Assert.IsTrue(ReadDefinition().GetAnimation(session.Presentation.Action).MotionBounds.Width <= session.RoamingArea.Width);
        Assert.IsTrue(ReadDefinition().GetAnimation(session.Presentation.Action).MotionBounds.Height <= session.RoamingArea.Height);

        var start = session.Position;
        session.Advance(TimeSpan.FromMilliseconds(640));
        Assert.AreEqual(start.X, session.Presentation.Position.X);
        Assert.AreEqual(start.Y - 90, session.Presentation.Position.Y, 1e-9);
        Assert.AreEqual(start, session.Position);
        Assert.AreEqual(TimeSpan.FromSeconds(1), session.RemainingRandomInterval);
        session.Advance(TimeSpan.FromMilliseconds(560));
        Assert.AreEqual(start, session.Presentation.Position);
        Assert.AreEqual(DesktopPet.Core.PetAction.Idle, session.Presentation.Action);
    }

    [TestMethod]
    public void Advance_SuspendsRandomEventsDuringDragEditSleepAndForegroundActions()
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Behavior = defaults.Behavior with { Roaming = false, IdleAnimations = false },
            Needs = defaults.Needs with { HungerPointsPerHour = 0, BoredomPointsPerHour = 0, SleepPointsPerHour = 0 },
            RandomInterval = defaults.RandomInterval with { MinimumMinutes = 2d / 60, MaximumMinutes = 2d / 60 }
        };
        var session = CreateSession(new SequenceRandomSource(0, 0), new RecordingSessionStore(), configuration, enableRandomEvents: true);
        session.Advance(TimeSpan.FromMilliseconds(500));
        var remaining = TimeSpan.FromMilliseconds(1500);

        session.BeginDragging();
        session.Advance(TimeSpan.FromSeconds(3));
        Assert.AreEqual(remaining, session.RemainingRandomInterval);
        session.EndDragging();
        session.BeginAreaEditing();
        session.Advance(TimeSpan.FromSeconds(3));
        Assert.AreEqual(remaining, session.RemainingRandomInterval);
        session.EndAreaEditing();

        session.Sleep();
        session.Advance(TimeSpan.FromSeconds(3));
        Assert.AreEqual(remaining, session.RemainingRandomInterval);
        session.React();
        session.Advance(TimeSpan.FromMilliseconds(900));
        Assert.AreEqual(remaining, session.RemainingRandomInterval);

        session.Feed();
        session.Advance(TimeSpan.FromMilliseconds(900));
        Assert.AreEqual(remaining, session.RemainingRandomInterval);

        session.Advance(remaining);
        Assert.AreEqual(DesktopPet.Core.PetAction.Wave, session.Presentation.Action);
    }

    [TestMethod]
    public void Feed_ReducesHungerToZeroFloorPersistsNeedsAndFinishesItsDedicatedAction()
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Behavior = defaults.Behavior with { Roaming = false },
            Needs = defaults.Needs with
            {
                HungerPointsPerHour = 30,
                BoredomPointsPerHour = 0,
                SleepPointsPerHour = 0,
                FeedGain = 20
            }
        };
        var store = new RecordingSessionStore();
        var session = CreateSession(new SequenceRandomSource(0), store, configuration);
        session.Advance(TimeSpan.FromHours(1));

        session.Feed();

        Assert.AreEqual(10, session.Needs.Hunger, 1e-9);
        Assert.AreEqual(DesktopPet.Core.PetAction.Feed, session.Presentation.Action);
        Assert.AreEqual(0, session.Presentation.FrameIndex);
        Assert.AreEqual(1, store.SaveCount);
        Assert.AreEqual(session.Needs, store.Snapshot!.Needs);

        session.Advance(TimeSpan.FromMilliseconds(900));
        Assert.AreEqual(DesktopPet.Core.PetAction.Idle, session.Presentation.Action);

        session.Feed();
        Assert.AreEqual(0, session.Needs.Hunger);
    }

    [TestMethod]
    public void RepeatedFeed_ReplacesTheCurrentActionWithoutQueueingPlayback()
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Needs = defaults.Needs with { HungerPointsPerHour = 0, BoredomPointsPerHour = 0, SleepPointsPerHour = 0 }
        };
        var store = new RecordingSessionStore();
        var session = CreateSession(new SequenceRandomSource(0), store, configuration);

        session.Feed();
        session.Feed();
        Assert.AreEqual(2, store.SaveCount);
        Assert.AreEqual(DesktopPet.Core.PetAction.Feed, session.Presentation.Action);
        Assert.AreEqual(0, session.Presentation.FrameIndex);

        session.Advance(TimeSpan.FromMilliseconds(900));
        Assert.AreEqual(DesktopPet.Core.PetAction.Idle, session.Presentation.Action);
    }

    [TestMethod]
    public void React_ReducesBoredomForEachStationaryClickAndReplacesTheCurrentAction()
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Behavior = defaults.Behavior with { Roaming = false },
            Needs = defaults.Needs with
            {
                HungerPointsPerHour = 0,
                BoredomPointsPerHour = 100,
                SleepPointsPerHour = 0,
                StationaryClickRelief = 10
            }
        };
        var store = new RecordingSessionStore();
        var session = CreateSession(new SequenceRandomSource(0), store, configuration);
        session.Advance(TimeSpan.FromMinutes(30));

        session.React();
        session.React();

        Assert.AreEqual(30, session.Needs.Boredom);
        Assert.AreEqual(2, store.SaveCount);
        Assert.AreEqual(session.Needs, store.Snapshot!.Needs);
        Assert.AreEqual(DesktopPet.Core.PetAction.Jump, session.Presentation.Action);
        Assert.AreEqual(0, session.Presentation.FrameIndex);

        session.Advance(TimeSpan.FromMilliseconds(1200));
        Assert.AreEqual(DesktopPet.Core.PetAction.Idle, session.Presentation.Action);
    }

    [TestMethod]
    public void React_DoesNotReduceBoredomWhileRoamingIsEnabled()
    {
        var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
        var configuration = defaults with
        {
            Needs = defaults.Needs with
            {
                HungerPointsPerHour = 0,
                BoredomPointsPerHour = 10,
                SleepPointsPerHour = 0,
                MovementReliefPerSecond = 0
            }
        };
        var store = new RecordingSessionStore();
        var session = CreateSession(new SequenceRandomSource(0), store, configuration);
        session.Advance(TimeSpan.FromHours(1));

        session.React();

        Assert.AreEqual(10, session.Needs.Boredom, 1e-9);
        Assert.AreEqual(0, store.SaveCount);
        Assert.AreEqual(DesktopPet.Core.PetAction.Jump, session.Presentation.Action);
    }

    [TestMethod]
    public void Feed_PersistsNeedChangesForTheNextSession()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hood-feed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var store = new DesktopPet.Core.JsonSessionStore(Path.Combine(directory, "state.json"));
            var defaults = DesktopPet.Core.HoodConfiguration.Defaults;
            var configuration = defaults with
            {
                Needs = defaults.Needs with
                {
                    HungerPointsPerHour = 30,
                    BoredomPointsPerHour = 0,
                    SleepPointsPerHour = 0,
                    FeedGain = 20
                }
            };
            var session = CreateSession(new SequenceRandomSource(0), store, configuration);
            session.Advance(TimeSpan.FromHours(1));
            session.Feed();

            var resumed = CreateSession(new SequenceRandomSource(0), store, configuration, store.Load());

            Assert.AreEqual(10, resumed.Needs.Hunger, 1e-9);
            Assert.AreEqual(0, resumed.Needs.Boredom);
            Assert.AreEqual(0, resumed.Needs.Sleep);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void Start_RejectsAMissingBaselineAnimationWithAnActionableError()
    {
        var (json, width, height) = ReadPackagedDefinition();
        var root = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        var animations = root["animations"]!.AsArray();
        var jumpIndex = animations.ToList().FindIndex(animation =>
            string.Equals(animation?["action"]?.GetValue<string>(), "jump", StringComparison.OrdinalIgnoreCase));
        animations.RemoveAt(jumpIndex);

        var exception = Assert.Throws<DesktopPet.Core.PetAssetException>(
            () => DesktopPet.Core.PetDefinition.Parse(root.ToJsonString(), width, height));

        Assert.IsTrue(exception.Message.Contains("Jump", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(exception.Message.Contains("missing", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Start_RejectsFramesOutsideThePackagedSheet()
    {
        var (json, width, height) = ReadPackagedDefinition();
        var root = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        var jump = root["animations"]!.AsArray().Single(animation =>
            string.Equals(animation?["action"]?.GetValue<string>(), "jump", StringComparison.OrdinalIgnoreCase))!;
        jump["frames"]!.AsArray()[4]!.AsObject()["column"] = width / root["frameWidth"]!.GetValue<int>();

        var exception = Assert.Throws<DesktopPet.Core.PetAssetException>(
            () => DesktopPet.Core.PetDefinition.Parse(root.ToJsonString(), width, height));

        Assert.IsTrue(exception.Message.Contains("Jump", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(exception.Message.Contains("outside", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Quit_SavesTheCurrentSnapshotThroughTheStorageBoundary()
    {
        var store = new RecordingSessionStore();
        var session = CreateSession(new SequenceRandomSource(0), store);

        session.Quit();
        session.Quit();

        Assert.IsTrue(session.QuitRequested);
        Assert.AreEqual(1, store.SaveCount);
        Assert.AreEqual(new DesktopPet.Core.PetPosition(864, 816), store.Snapshot!.Position);
        Assert.AreEqual(DesktopPet.Core.PetAction.Idle, store.Snapshot.Action);
        Assert.AreEqual(DesktopPet.Core.NeedLevels.Initial, store.Snapshot.Needs);
        Assert.AreEqual(DesktopPet.Core.HoodSessionMode.Awake, store.Snapshot.Mode);
        Assert.AreEqual(new DesktopPet.Core.PetBounds(720, 816, 480, 208), store.Snapshot.RoamingArea);
    }

    [TestMethod]
    public void JsonSessionStore_WritesACompleteSnapshotAndRemovesItsTemporaryFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hood-session-{Guid.NewGuid():N}");
        var nestedDirectory = Path.Combine(directory, "local");
        var path = Path.Combine(nestedDirectory, "state.json");
        var snapshot = new DesktopPet.Core.HoodSessionSnapshot
        {
            Position = new DesktopPet.Core.PetPosition(-40, 120),
            Action = DesktopPet.Core.PetAction.Jump,
            FrameIndex = 3
        };

        try
        {
            new DesktopPet.Core.JsonSessionStore(path).Save(snapshot);

            var json = File.ReadAllText(path);
            StringAssert.Contains(json, "\"schemaVersion\": 2");
            StringAssert.Contains(json, "\"position\"");
            StringAssert.Contains(json, "\"action\": \"jump\"");
            Assert.AreEqual(0, Directory.GetFiles(nestedDirectory, "*.tmp").Length);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(nestedDirectory)) Directory.Delete(nestedDirectory);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    private static DesktopPet.Core.HoodSession CreateSession(
        DesktopPet.Core.IPetRandomSource random,
        DesktopPet.Core.ISessionStore store,
        DesktopPet.Core.HoodConfiguration? configuration = null,
        DesktopPet.Core.HoodSessionSnapshot? restoredSession = null,
        bool enableRandomEvents = false)
    {
        var definition = ReadDefinition();
        DesktopPet.Core.DisplayInfo[] displays =
        [
            new("Secondary", new DesktopPet.Core.PetBounds(-1600, 0, 1600, 900), IsPrimary: false),
            new("Primary", new DesktopPet.Core.PetBounds(0, 0, 1920, 1040), IsPrimary: true)
        ];
        configuration ??= DesktopPet.Core.HoodConfiguration.Defaults;
        if (!enableRandomEvents)
        {
            configuration = configuration with
            {
                Behavior = configuration.Behavior with { RandomEvents = false }
            };
        }

        return DesktopPet.Core.HoodSession.Start(definition, displays, random, store, configuration, restoredSession);
    }

    private static DesktopPet.Core.PetDefinition ReadDefinition()
    {
        var (json, width, height) = ReadPackagedDefinition();
        return DesktopPet.Core.PetDefinition.Parse(json, width, height);
    }

    private static (string Json, int Width, int Height) ReadPackagedDefinition()
    {
        var assetDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Hood");
        var json = File.ReadAllText(Path.Combine(assetDirectory, "hood.animations.json"));
        var png = File.ReadAllBytes(Path.Combine(assetDirectory, "hood_spritesheet.png"));
        byte[] signature = [137, 80, 78, 71, 13, 10, 26, 10];
        Assert.IsTrue(png.AsSpan(0, signature.Length).SequenceEqual(signature), "The packaged Hood sprite sheet must be a PNG.");

        return (
            json,
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)),
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
    }

    private sealed class SequenceRandomSource(params int[] values) : DesktopPet.Core.IPetRandomSource
    {
        private int _index;

        public int NextIndex(int exclusiveUpperBound)
        {
            var value = values[Math.Min(_index++, values.Length - 1)];
            if (value >= exclusiveUpperBound)
            {
                throw new InvalidOperationException("The requested test random value is outside the allowed range.");
            }

            return value;
        }
    }

    private sealed class RecordingSessionStore : DesktopPet.Core.ISessionStore
    {
        public int SaveCount { get; private set; }
        public DesktopPet.Core.HoodSessionSnapshot? Snapshot { get; private set; }

        public DesktopPet.Core.HoodSessionSnapshot? Load() => null;

        public string? ResetAfterAcknowledgement() => null;

        public void Save(DesktopPet.Core.HoodSessionSnapshot snapshot)
        {
            SaveCount++;
            Snapshot = snapshot;
        }
    }
}
