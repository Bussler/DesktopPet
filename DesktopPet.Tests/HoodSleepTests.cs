using System.Buffers.Binary;
using DesktopPet.Core;

namespace DesktopPet.Tests;

[TestClass]
public sealed class HoodSleepTests
{
    [TestMethod]
    public void Sleep_ManualNapRecoversSleepProportionallyWithoutChangingOtherNeeds()
    {
        var configuration = Config(durationMinutes: 4, threshold: 90);
        var session = CreateSession(configuration, AwakeSnapshot(new NeedLevels(30, 50, 80)));

        session.Sleep();
        Assert.AreEqual(HoodSessionMode.Sleeping, session.Mode);
        Assert.AreEqual(PetAction.Sleep, session.Presentation.Action);
        Assert.AreEqual(TimeSpan.FromMinutes(4), session.RemainingNap);

        session.Advance(TimeSpan.FromMinutes(2));

        Assert.AreEqual(40, session.Needs.Sleep, 1e-9);
        Assert.AreEqual(30, session.Needs.Hunger);
        Assert.AreEqual(50, session.Needs.Boredom);
        Assert.AreEqual(TimeSpan.FromMinutes(2), session.RemainingNap);

        session.Advance(TimeSpan.FromMinutes(2));

        Assert.AreEqual(HoodSessionMode.Awake, session.Mode);
        Assert.AreEqual(PetAction.Wake, session.Presentation.Action);
        Assert.AreEqual(0, session.Needs.Sleep);
        Assert.AreEqual(TimeSpan.Zero, session.RemainingNap);
        session.Advance(TimeSpan.FromMilliseconds(900));
        Assert.AreEqual(PetAction.Idle, session.Presentation.Action);
    }

    [TestMethod]
    public void Advance_AutomaticNapBeginsAtTheConfiguredThresholdWhileResting()
    {
        var configuration = Config(durationMinutes: 1, threshold: 5, sleepPointsPerHour: 3600);
        var session = CreateSession(configuration);

        session.Advance(TimeSpan.FromSeconds(4.999));
        Assert.AreEqual(HoodSessionMode.Awake, session.Mode);
        Assert.AreEqual(PetAction.Idle, session.Presentation.Action);

        session.Advance(TimeSpan.FromMilliseconds(1));

        Assert.AreEqual(HoodSessionMode.Sleeping, session.Mode);
        Assert.AreEqual(5, session.Needs.Sleep, 1e-9);
        Assert.AreEqual(TimeSpan.FromMinutes(1), session.RemainingNap);
    }

    [TestMethod]
    public void Advance_DefersAutomaticNapUntilTheForegroundActionFinishes()
    {
        var configuration = Config(durationMinutes: 1, threshold: 1, sleepPointsPerHour: 7200);
        var session = CreateSession(configuration);
        session.React();

        session.Advance(TimeSpan.FromMilliseconds(500));
        Assert.AreEqual(1, session.Needs.Sleep, 1e-9);
        Assert.AreEqual(HoodSessionMode.Awake, session.Mode);
        Assert.AreEqual(PetAction.Jump, session.Presentation.Action);

        session.Advance(TimeSpan.FromMilliseconds(700));

        Assert.AreEqual(HoodSessionMode.Sleeping, session.Mode);
        Assert.AreEqual(PetAction.Sleep, session.Presentation.Action);
    }

    [TestMethod]
    public void Advance_ProducesEquivalentNapTransitionsForDifferentTimeSlices()
    {
        var configuration = Config(durationMinutes: 2d / 60, threshold: 5, sleepPointsPerHour: 3600);
        var singleStep = CreateSession(configuration);
        var smallerSteps = CreateSession(configuration);

        singleStep.Advance(TimeSpan.FromSeconds(10));
        for (var index = 0; index < 10; index++)
        {
            smallerSteps.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.AreEqual(singleStep.Needs, smallerSteps.Needs);
        Assert.AreEqual(singleStep.Mode, smallerSteps.Mode);
        Assert.AreEqual(singleStep.RemainingNap, smallerSteps.RemainingNap);
        Assert.AreEqual(singleStep.RemainingEarlyWakeGrace, smallerSteps.RemainingEarlyWakeGrace);
        Assert.AreEqual(singleStep.Presentation.Action, smallerSteps.Presentation.Action);
    }

    [TestMethod]
    public void React_WhileSleepingWakesAndConsumesTheClickWithoutJumping()
    {
        var configuration = Config(durationMinutes: 4, threshold: 90, earlyWakeGraceMinutes: 3);
        var session = CreateSession(configuration, AwakeSnapshot(new NeedLevels(10, 20, 80)));
        session.Sleep();
        session.Advance(TimeSpan.FromMinutes(1));

        session.React();

        Assert.AreEqual(HoodSessionMode.Awake, session.Mode);
        Assert.AreEqual(PetAction.Wake, session.Presentation.Action);
        Assert.AreEqual(60, session.Needs.Sleep, 1e-9);
        Assert.AreEqual(TimeSpan.FromMinutes(3), session.RemainingEarlyWakeGrace);
        Assert.AreEqual(TimeSpan.Zero, session.RemainingNap);
    }

    [TestMethod]
    public void Advance_EarlyWakeGraceSuppressesThenAllowsAutomaticSleep()
    {
        var configuration = Config(
            durationMinutes: 4,
            threshold: 55,
            sleepPointsPerHour: 120,
            earlyWakeGraceMinutes: 3);
        var session = CreateSession(configuration, AwakeSnapshot(new NeedLevels(0, 0, 80)));
        session.Sleep();
        session.Advance(TimeSpan.FromMinutes(1));
        session.React();

        session.Advance(TimeSpan.FromMinutes(2));
        Assert.AreEqual(HoodSessionMode.Awake, session.Mode);
        Assert.AreEqual(64, session.Needs.Sleep, 1e-9);
        Assert.AreEqual(TimeSpan.FromMinutes(1), session.RemainingEarlyWakeGrace);

        session.Advance(TimeSpan.FromMinutes(1));
        Assert.AreEqual(HoodSessionMode.Sleeping, session.Mode);
        Assert.AreEqual(66, session.Needs.Sleep, 1e-9);
    }

    [TestMethod]
    [DataRow(PetAction.Feed)]
    [DataRow(PetAction.Pet)]
    public void Care_WhileSleepingWakesWithoutLosingEarnedRecovery(PetAction action)
    {
        var configuration = Config(durationMinutes: 4, threshold: 90, earlyWakeGraceMinutes: 3, feedGain: 40);
        var session = CreateSession(configuration, AwakeSnapshot(new NeedLevels(50, 20, 80)));
        session.Sleep();
        session.Advance(TimeSpan.FromMinutes(1));

        if (action == PetAction.Feed) session.Feed();
        else session.Pet();

        Assert.AreEqual(HoodSessionMode.Awake, session.Mode);
        Assert.AreEqual(action, session.Presentation.Action);
        Assert.AreEqual(action == PetAction.Feed ? 10 : 50, session.Needs.Hunger);
        Assert.AreEqual(action == PetAction.Pet ? 0 : 20, session.Needs.Boredom);
        Assert.AreEqual(60, session.Needs.Sleep, 1e-9);
        Assert.AreEqual(TimeSpan.FromMinutes(3), session.RemainingEarlyWakeGrace);
    }

    [TestMethod]
    [DataRow(PetAction.Feed, 900)]
    [DataRow(PetAction.Pet, 1920)]
    public void Advance_AutomaticNapWaitsForCareAnimationToFinish(PetAction action, int milliseconds)
    {
        var configuration = Config(durationMinutes: 1, threshold: 1, sleepPointsPerHour: 7200);
        var session = CreateSession(configuration);
        if (action == PetAction.Feed) session.Feed();
        else session.Pet();

        session.Advance(TimeSpan.FromMilliseconds(milliseconds - 100));
        Assert.AreEqual(HoodSessionMode.Awake, session.Mode);
        Assert.AreEqual(action, session.Presentation.Action);

        session.Advance(TimeSpan.FromMilliseconds(100));
        Assert.AreEqual(HoodSessionMode.Sleeping, session.Mode);
        Assert.AreEqual(PetAction.Sleep, session.Presentation.Action);
    }

    [TestMethod]
    public void QuitAndRestore_PreservesPartialNapAndItsRecoveryAfterDurationSettingsChange()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonSessionStore(Path.Combine(directory.Path, "state.json"));
        var originalConfiguration = Config(durationMinutes: 4, threshold: 90);
        var session = CreateSession(originalConfiguration, AwakeSnapshot(new NeedLevels(0, 0, 80)), store);
        session.Sleep();
        session.Advance(TimeSpan.FromMinutes(1));
        session.Quit();

        var restored = CreateSession(Config(durationMinutes: 1, threshold: 90), store.Load(), store);

        Assert.AreEqual(HoodSessionMode.Sleeping, restored.Mode);
        Assert.AreEqual(TimeSpan.FromMinutes(3), restored.RemainingNap);
        Assert.AreEqual(session.NapRecoveryPointsPerSecond, restored.NapRecoveryPointsPerSecond);
        Assert.AreEqual(session.Needs.Sleep, restored.Needs.Sleep);
    }

    [TestMethod]
    public void QuitAndRestore_PreservesRemainingEarlyWakeGraceAfterSettingsChange()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonSessionStore(Path.Combine(directory.Path, "state.json"));
        var originalConfiguration = Config(durationMinutes: 4, threshold: 95, earlyWakeGraceMinutes: 3);
        var session = CreateSession(originalConfiguration, AwakeSnapshot(new NeedLevels(0, 0, 80)), store);
        session.Sleep();
        session.Advance(TimeSpan.FromMinutes(1));
        session.React();
        session.Advance(TimeSpan.FromMinutes(1));
        session.Quit();

        var restored = CreateSession(Config(durationMinutes: 1, threshold: 95, earlyWakeGraceMinutes: 0.5), store.Load(), store);

        Assert.AreEqual(HoodSessionMode.Awake, restored.Mode);
        Assert.AreEqual(TimeSpan.FromMinutes(2), restored.RemainingEarlyWakeGrace);
        Assert.AreEqual(session.Needs, restored.Needs);
    }

    private static HoodConfiguration Config(
        double durationMinutes,
        double threshold,
        double sleepPointsPerHour = 0,
        double earlyWakeGraceMinutes = 15,
        double feedGain = 40)
    {
        var defaults = HoodConfiguration.Defaults;
        return defaults with
        {
            Behavior = defaults.Behavior with { Roaming = false },
            Needs = defaults.Needs with
            {
                HungerPointsPerHour = 0,
                BoredomPointsPerHour = 0,
                SleepPointsPerHour = sleepPointsPerHour,
                FeedGain = feedGain
            },
            Sleep = defaults.Sleep with
            {
                AutomaticNapThreshold = threshold,
                DurationMinutes = durationMinutes,
                EarlyWakeGraceMinutes = earlyWakeGraceMinutes
            }
        };
    }

    private static HoodSessionSnapshot AwakeSnapshot(NeedLevels needs) => new()
    {
        Position = new PetPosition(0, 0),
        Needs = needs
    };

    private static HoodSession CreateSession(
        HoodConfiguration configuration,
        HoodSessionSnapshot? restored = null,
        ISessionStore? store = null)
    {
        var assetDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Hood");
        var json = File.ReadAllText(Path.Combine(assetDirectory, "hood.animations.json"));
        var png = File.ReadAllBytes(Path.Combine(assetDirectory, "hood_spritesheet.png"));
        var definition = PetDefinition.Parse(
            json,
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)),
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
        DisplayInfo[] displays = [new("Primary", new PetBounds(0, 0, 1920, 1040), IsPrimary: true)];

        return HoodSession.Start(definition, displays, new FixedRandomSource(), store ?? new NoopSessionStore(), configuration, restored);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"hood-sleep-{Guid.NewGuid():N}");
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
}
