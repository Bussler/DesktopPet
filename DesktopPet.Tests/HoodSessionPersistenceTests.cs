using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopPet.Core;

namespace DesktopPet.Tests;

[TestClass]
public sealed class HoodSessionPersistenceTests
{
    [TestMethod]
    public void WritingCannotBeSavedOrRestoredAsDurableSessionAction()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "state.json");
        var store = new JsonSessionStore(path);
        var valid = CreateAwakeSnapshot();
        store.Save(valid);
        var original = File.ReadAllText(path);
        var transient = valid with { Action = PetAction.Write, FrameIndex = 0 };
        Assert.Throws<SessionStateException>(() => store.Save(transient));
        Assert.AreEqual(original, File.ReadAllText(path));
        var edited = JsonNode.Parse(original)!;
        edited["action"] = "write";
        File.WriteAllText(path, edited.ToJsonString());
        var malformed = File.ReadAllText(path);
        Assert.Throws<SessionStateException>(() => store.Load());
        Assert.AreEqual(malformed, File.ReadAllText(path));
        Assert.Throws<InvalidDataException>(() => CreateSession(transient, HoodConfiguration.Defaults));
    }

    [TestMethod]
    [DataRow(PetAction.Idle)]
    [DataRow(PetAction.RunRight)]
    [DataRow(PetAction.Jump)]
    [DataRow(PetAction.Sleep)]
    public void HiddenSpritesPauseAnimationsMovementNeedsAndTimersUntilShown(PetAction action)
    {
        var snapshot = action == PetAction.Sleep ? CreateSleepingSnapshot() : CreateAwakeSnapshot() with { Action = action };
        var configuration = HoodConfiguration.Defaults;
        var session = CreateSession(snapshot, configuration);
        using var directory = new TemporaryDirectory();
        var controller = new HoodConfigurationController(session, new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml")));
        controller.SaveAndApply("behavior.show_sprites", configuration with
        {
            Behavior = configuration.Behavior with { ShowSprites = false }
        });
        var presentation = session.Presentation;
        Assert.IsFalse(session.RequiresAnimationFrames);

        session.Advance(TimeSpan.FromDays(10));

        Assert.AreEqual(presentation, session.Presentation);
        Assert.AreEqual(snapshot.Needs, session.Needs);
        Assert.AreEqual(snapshot.RemainingNap, session.RemainingNap);
        Assert.AreEqual(snapshot.RemainingRandomInterval, session.RemainingRandomInterval);
        Assert.AreEqual(snapshot.RemainingEarlyWakeGrace, session.RemainingEarlyWakeGrace);
        var restored = CreateSession(snapshot, controller.Current);
        restored.Advance(TimeSpan.FromDays(10));
        Assert.AreEqual(presentation, restored.Presentation, "Starting hidden must also pause a restored action.");
        controller.SaveAndApply("behavior.show_sprites", configuration);
        session.Advance(TimeSpan.FromMilliseconds(250));
        Assert.IsTrue(session.RequiresAnimationFrames);
        Assert.AreNotEqual(snapshot.Needs, session.Needs, "Active time resumes without catching up hidden time.");
    }

    [TestMethod]
    public void JsonSessionStore_LoadsNoFileAsFreshAndRoundTripsEveryContinuationField()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonSessionStore(Path.Combine(directory.Path, "state.json"));
        Assert.IsNull(store.Load());

        var snapshot = CreateSleepingSnapshot();
        store.Save(snapshot);

        Assert.AreEqual(snapshot, store.Load());
    }

    [TestMethod]
    public void JsonSessionStore_MigratesVersionOneAndIgnoresArbitraryClosedWallClockTime()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "state.json");
        File.WriteAllText(path, """
            {"schemaVersion":1,"position":{"x":-40,"y":120},"action":"jump","frameIndex":2}
            """);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddYears(-20));
        var store = new JsonSessionStore(path);

        var migrated = store.Load();

        Assert.IsNotNull(migrated);
        Assert.AreEqual(2, migrated.SchemaVersion);
        Assert.AreEqual(new PetPosition(-40, 120), migrated.Position);
        Assert.AreEqual(PetAction.Jump, migrated.Action);
        Assert.AreEqual(NeedLevels.Initial, migrated.Needs);
        Assert.AreEqual(HoodSessionMode.Awake, migrated.Mode);
        Assert.IsNull(migrated.RoamingArea);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddYears(20));
        Assert.AreEqual(migrated, store.Load());
    }

    [TestMethod]
    public void HoodSession_StartRestoresSavedNeedsModePositionAreaAndNapAfterConfigChanges()
    {
        var saved = CreateSleepingSnapshot();
        var changedConfiguration = HoodConfiguration.Defaults with
        {
            Sleep = HoodConfiguration.Defaults.Sleep with { DurationMinutes = 1 }
        };

        var session = CreateSession(saved, changedConfiguration);

        Assert.AreEqual(saved.Position, session.Position);
        Assert.AreEqual(saved.RoamingArea!.Value.Width, session.RoamingArea.Width);
        Assert.AreEqual(208, session.RoamingArea.Height);
        Assert.IsTrue(session.RoamingArea.X <= session.Position.X);
        Assert.IsTrue(session.RoamingArea.Y <= session.Position.Y);
        Assert.IsTrue(session.RoamingArea.Right >= session.Position.X + 192);
        Assert.IsTrue(session.RoamingArea.Bottom >= session.Position.Y + 208);
        Assert.AreEqual(saved.Needs, session.Needs);
        Assert.AreEqual(HoodSessionMode.Sleeping, session.Mode);
        Assert.AreEqual(saved.RemainingNap, session.RemainingNap);
        Assert.AreEqual(saved.NapRecoveryPointsPerSecond, session.NapRecoveryPointsPerSecond);
        Assert.AreEqual(1, session.Configuration.Sleep.DurationMinutes);
    }

    [TestMethod]
    public void JsonSessionStore_LeavesDamagedStateUntouchedUntilAcknowledgedResetAndArchivesIt()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "state.json");
        var original = "{ broken state";
        File.WriteAllText(path, original);
        var store = new JsonSessionStore(path);

        Assert.Throws<SessionStateException>(() => store.Load());
        Assert.AreEqual(original, File.ReadAllText(path));

        var archivedPath = store.ResetAfterAcknowledgement();

        Assert.IsFalse(File.Exists(path));
        Assert.IsNotNull(archivedPath);
        Assert.AreEqual(original, File.ReadAllText(archivedPath));
        Assert.AreEqual(0, Directory.GetFiles(directory.Path, "*.tmp").Length);
    }

    [TestMethod]
    public void JsonSessionStore_RejectsUnsupportedAndInvalidStateWithoutChangingTheFile()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "state.json");
        var store = new JsonSessionStore(path);
        store.Save(CreateAwakeSnapshot());
        var baseline = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var mutations = new (string Name, Action<JsonObject> Mutate)[]
        {
            ("unsupported schema", node => node["schemaVersion"] = 99),
            ("out of range need", node => node["needs"]!["hunger"] = 101),
            ("negative timer", node => node["remainingRandomInterval"] = "-00:00:01"),
            ("sleep without remaining time", node => { node["mode"] = "sleeping"; node["action"] = "sleep"; }),
            ("sleep without recovery context", node =>
            {
                node["mode"] = "sleeping";
                node["action"] = "sleep";
                node["remainingNap"] = "00:01:00";
                node["needs"]!["sleep"] = 40;
                node["napRecoveryPointsPerSecond"] = 0;
            }),
            ("awake with active nap timer", node => node["remainingNap"] = "00:01:00"),
            ("unknown action", node => node["action"] = "glide"),
            ("unknown property", node => node["unknown"] = true),
            ("invalid area", node => node["roamingArea"]!["width"] = 0)
        };

        foreach (var (name, mutate) in mutations)
        {
            var changed = (JsonObject)baseline.DeepClone();
            mutate(changed);
            var invalidJson = changed.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, invalidJson);

            Assert.Throws<SessionStateException>(() => store.Load(), name);
            Assert.AreEqual(invalidJson, File.ReadAllText(path), $"The {name} file must remain intact.");
        }
    }

    [TestMethod]
    public void HoodSession_StartRejectsAFrameMissingFromPackagedAssetsWithoutChangingSavedState()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "state.json");
        var store = new JsonSessionStore(path);
        var invalidSnapshot = CreateAwakeSnapshot() with { FrameIndex = 99 };
        store.Save(invalidSnapshot);
        var original = File.ReadAllText(path);
        var loaded = store.Load();

        Assert.Throws<InvalidDataException>(() => CreateSession(loaded!, HoodConfiguration.Defaults));
        Assert.AreEqual(original, File.ReadAllText(path));
    }

    [TestMethod]
    public void JsonSessionStore_WhenAtomicReplacementFails_PreservesThePreviousStateFile()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "state.json");
        var store = new JsonSessionStore(path);
        store.Save(CreateAwakeSnapshot());
        var original = File.ReadAllText(path);

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Throws<SessionStateException>(() => store.Save(CreateSleepingSnapshot()));
        }

        Assert.AreEqual(original, File.ReadAllText(path));
        Assert.AreEqual(0, Directory.GetFiles(directory.Path, "*.tmp").Length);
    }

    [TestMethod]
    public void SessionAndConfigurationRestoreFromSeparateFiles()
    {
        using var directory = new TemporaryDirectory();
        var configurationStore = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        var stateStore = new JsonSessionStore(Path.Combine(directory.Path, "state.json"));
        var configuration = configurationStore.LoadOrCreate() with
        {
            Needs = HoodConfiguration.Defaults.Needs with { HungerPointsPerHour = 7.5 }
        };
        configurationStore.Save(configuration);
        var snapshot = CreateAwakeSnapshot();
        stateStore.Save(snapshot);

        Assert.AreEqual(configuration, configurationStore.LoadOrCreate());
        Assert.AreEqual(snapshot, stateStore.Load());
        Assert.IsFalse(File.ReadAllText(stateStore.StatePath).Contains("hungerPointsPerHour", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RandomReactionCountdown_RestoresAndContinuesWithoutClosedTime()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonSessionStore(Path.Combine(directory.Path, "state.json"));
        var configuration = HoodConfiguration.Defaults with
        {
            Behavior = HoodConfiguration.Defaults.Behavior with { Roaming = false, IdleAnimations = false },
            RandomInterval = HoodConfiguration.Defaults.RandomInterval with
            {
                MinimumMinutes = 1d / 60,
                MaximumMinutes = 1d / 60
            }
        };
        var startingSnapshot = CreateAwakeSnapshot() with
        {
            Action = PetAction.Idle,
            FrameIndex = 0,
            Needs = NeedLevels.Initial,
            RemainingRandomInterval = TimeSpan.FromSeconds(1),
            RemainingEarlyWakeGrace = TimeSpan.Zero,
            RoamingArea = new PetBounds(-160, 92, 480, 208)
        };
        store.Save(startingSnapshot);
        var session = CreateSession(startingSnapshot, configuration, store);

        session.Advance(TimeSpan.FromMilliseconds(400));
        session.Quit();
        var saved = store.Load();

        Assert.IsNotNull(saved);
        Assert.AreEqual(TimeSpan.FromMilliseconds(600), saved.RemainingRandomInterval);
        var resumed = CreateSession(saved, configuration, store);
        resumed.Advance(TimeSpan.FromMilliseconds(599));
        Assert.AreEqual(PetAction.Idle, resumed.Presentation.Action);
        resumed.Advance(TimeSpan.FromMilliseconds(1));

        Assert.AreEqual(PetAction.Wave, resumed.Presentation.Action);
        Assert.AreEqual(TimeSpan.FromSeconds(1), resumed.RemainingRandomInterval);
    }

    [TestMethod]
    public void RandomReactionCountdown_RestoreCapsAnOlderLongCountdownToTheConfiguredMaximum()
    {
        var saved = CreateAwakeSnapshot() with
        {
            Action = PetAction.Idle,
            FrameIndex = 0,
            RemainingRandomInterval = TimeSpan.FromMinutes(15)
        };

        var session = CreateSession(saved, HoodConfiguration.Defaults);

        Assert.AreEqual(TimeSpan.FromMinutes(5), session.RemainingRandomInterval);
        session.Advance(TimeSpan.FromMinutes(5));
        Assert.AreEqual(PetAction.Wave, session.Presentation.Action);
    }

    private static HoodSessionSnapshot CreateAwakeSnapshot() => new()
    {
        Position = new PetPosition(-40, 120),
        Action = PetAction.Jump,
        FrameIndex = 2,
        Needs = new NeedLevels(70, 18, 35),
        RemainingRandomInterval = TimeSpan.FromMinutes(4),
        RemainingEarlyWakeGrace = TimeSpan.FromMinutes(9),
        RoamingArea = new PetBounds(-160, 92, 480, 180)
    };

    private static HoodSessionSnapshot CreateSleepingSnapshot() => new()
    {
        Position = new PetPosition(-40, 120),
        Action = PetAction.Sleep,
        FrameIndex = 1,
        Needs = new NeedLevels(70, 18, 35),
        Mode = HoodSessionMode.Sleeping,
        RemainingNap = TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(15),
        NapRecoveryPointsPerSecond = 0.125,
        RemainingRandomInterval = TimeSpan.FromMinutes(4),
        RoamingArea = new PetBounds(-160, 92, 480, 180)
    };

    private static HoodSession CreateSession(
        HoodSessionSnapshot snapshot,
        HoodConfiguration configuration,
        ISessionStore? store = null)
    {
        var assetDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Hood");
        var json = File.ReadAllText(Path.Combine(assetDirectory, "hood.animations.json"));
        var png = File.ReadAllBytes(Path.Combine(assetDirectory, "hood_spritesheet.png"));
        var definition = PetDefinition.Parse(
            json,
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)),
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
        DisplayInfo[] displays = [new("Primary", new PetBounds(-1600, 0, 3520, 1040), IsPrimary: true)];

        return HoodSession.Start(definition, displays, new FixedRandomSource(), store ?? new NoopSessionStore(), configuration, snapshot);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"hood-state-{Guid.NewGuid():N}");
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
        public void Save(HoodSessionSnapshot snapshot) { }
    }
}
