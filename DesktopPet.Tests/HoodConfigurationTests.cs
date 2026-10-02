using System.Text.RegularExpressions;
using DesktopPet.Core;

namespace DesktopPet.Tests;

[TestClass]
public sealed class HoodConfigurationTests
{
    [TestMethod]
    public void LoadOrCreate_WritesDocumentedDefaultsAndReloadsManualEdits()
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));

        var defaults = store.LoadOrCreate();

        Assert.AreEqual(HoodConfiguration.Defaults, defaults);
        Assert.IsTrue(File.Exists(store.Path));
        var yaml = File.ReadAllText(store.Path);
        StringAssert.Contains(yaml, "schema_version: 1");
        StringAssert.Contains(yaml, "hunger_points_per_hour: 12.5");
        StringAssert.Contains(yaml, "minimum_minutes: 1");
        StringAssert.Contains(yaml, "maximum_minutes: 5");
        Assert.AreEqual(0, Directory.GetFiles(directory.Path, "*.tmp").Length);

        File.WriteAllText(store.Path, ReplaceSetting(yaml, "hunger_points_per_hour", "37.25"));

        var edited = store.LoadOrCreate();
        Assert.AreEqual(37.25, edited.Needs.HungerPointsPerHour);
        Assert.AreEqual(16, edited.Needs.BoredomPointsPerHour);
    }

    [TestMethod]
    public void SaveAndApply_PersistsOneUiFieldAndPreservesAdvancedYamlValues()
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        store.LoadOrCreate();
        var yaml = File.ReadAllText(store.Path);
        var customYaml = ReplaceSetting(yaml, "hunger_points_per_hour", "37.25");
        customYaml = ReplaceSetting(customYaml, "early_wake_grace_minutes", "23");
        File.WriteAllText(store.Path, customYaml);
        var initial = store.LoadOrCreate();
        var session = CreateSession(initial);
        var controller = new HoodConfigurationController(session, store);

        var candidate = controller.Current with
        {
            Behavior = controller.Current.Behavior with { AlwaysOnTop = false }
        };
        controller.SaveAndApply("behavior.always_on_top", candidate);

        Assert.IsFalse(controller.Current.Behavior.AlwaysOnTop);
        Assert.AreEqual(37.25, controller.Current.Needs.HungerPointsPerHour);
        Assert.AreEqual(23, controller.Current.Sleep.EarlyWakeGraceMinutes);
        Assert.IsFalse(controller.Current.Behavior.StartWithWindows);
        var reloaded = store.LoadOrCreate();
        Assert.AreEqual(controller.Current, reloaded);
    }

    [TestMethod]
    public void SaveAndApply_PersistsBehaviorTogglesIndependently()
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        store.LoadOrCreate();
        var yaml = File.ReadAllText(store.Path);
        yaml = ReplaceSetting(yaml, "hunger_points_per_hour", "37.25");
        yaml = ReplaceSetting(yaml, "early_wake_grace_minutes", "23");
        File.WriteAllText(store.Path, yaml);

        var initial = store.LoadOrCreate();
        var session = CreateSession(initial);
        var controller = new HoodConfigurationController(session, store);
        session.Advance(TimeSpan.FromSeconds(1));
        var positionBeforeDisable = session.Position;
        Assert.AreNotEqual(new PetPosition(864, 816), positionBeforeDisable);

        controller.SaveAndApply("behavior", initial with
        {
            Behavior = initial.Behavior with { Roaming = false }
        });

        Assert.IsFalse(session.Configuration.Behavior.Roaming);
        Assert.IsTrue(session.Configuration.Behavior.IdleAnimations);
        Assert.AreEqual(PetAction.Idle, session.Presentation.Action);
        session.Advance(TimeSpan.FromSeconds(3));
        Assert.AreEqual(positionBeforeDisable, session.Position);
        Assert.IsFalse(store.LoadOrCreate().Behavior.Roaming);

        controller.SaveAndApply("behavior", controller.Current with
        {
            Behavior = controller.Current.Behavior with { IdleAnimations = false }
        });

        Assert.IsFalse(session.Configuration.Behavior.Roaming);
        Assert.IsFalse(session.Configuration.Behavior.IdleAnimations);
        Assert.IsTrue(session.Configuration.Behavior.RandomEvents);
        Assert.AreEqual(37.25, controller.Current.Needs.HungerPointsPerHour);
        Assert.AreEqual(23, controller.Current.Sleep.EarlyWakeGraceMinutes);
        Assert.AreEqual(controller.Current, store.LoadOrCreate());

        controller.SaveAndApply("behavior", controller.Current with
        {
            Behavior = controller.Current.Behavior with { RandomEvents = false }
        });
        Assert.IsFalse(session.Configuration.Behavior.RandomEvents);
        Assert.IsFalse(session.Configuration.Behavior.Roaming);
        Assert.IsFalse(session.Configuration.Behavior.IdleAnimations);
        Assert.AreEqual(controller.Current, store.LoadOrCreate());

        controller.SaveAndApply("behavior", controller.Current with
        {
            Behavior = controller.Current.Behavior with { Roaming = true }
        });
        session.Advance(TimeSpan.FromSeconds(1));
        Assert.AreNotEqual(positionBeforeDisable, session.Position);
        Assert.AreEqual(controller.Current, store.LoadOrCreate());
    }

    [TestMethod]
    public void SaveAndApply_RandomEventsToggleChangesAmbientSchedulingIndependently()
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        var defaults = store.LoadOrCreate();
        var configuration = defaults with
        {
            Behavior = defaults.Behavior with { Roaming = false, IdleAnimations = false, RandomEvents = false },
            Needs = defaults.Needs with { HungerPointsPerHour = 0, BoredomPointsPerHour = 0, SleepPointsPerHour = 0 },
            RandomInterval = defaults.RandomInterval with { MinimumMinutes = 1d / 60, MaximumMinutes = 1d / 60 }
        };
        store.Save(configuration);
        var session = CreateSession(configuration);
        var controller = new HoodConfigurationController(session, store);

        session.Advance(TimeSpan.FromSeconds(3));
        Assert.AreEqual(PetAction.Idle, session.Presentation.Action);
        Assert.AreEqual(TimeSpan.Zero, session.RemainingRandomInterval);

        controller.SaveAndApply("behavior.random_events", controller.Current with
        {
            Behavior = controller.Current.Behavior with { RandomEvents = true }
        });
        session.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(PetAction.Wave, session.Presentation.Action);
        Assert.IsFalse(session.Configuration.Behavior.Roaming);
        Assert.IsFalse(session.Configuration.Behavior.IdleAnimations);

        controller.SaveAndApply("behavior.random_events", controller.Current with
        {
            Behavior = controller.Current.Behavior with { RandomEvents = false }
        });
        session.Advance(TimeSpan.FromSeconds(3));
        Assert.AreEqual(PetAction.Idle, session.Presentation.Action);
        Assert.AreEqual(TimeSpan.FromSeconds(1), session.RemainingRandomInterval);
        Assert.AreEqual(controller.Current, store.LoadOrCreate());

        controller.SaveAndApply("behavior.random_events", controller.Current with
        {
            Behavior = controller.Current.Behavior with { RandomEvents = true }
        });
        session.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(PetAction.Wave, session.Presentation.Action);
    }

    [TestMethod]
    public void SaveAndApply_PreservesAllAmbientToggleCombinationsAndIndependentAppOptions()
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        var defaults = store.LoadOrCreate();
        var initial = defaults with
        {
            Needs = defaults.Needs with { HungerPointsPerHour = 37.25 },
            Sleep = defaults.Sleep with { EarlyWakeGraceMinutes = 23 }
        };
        store.Save(initial);
        var session = CreateSession(initial);
        var startup = new RecordingStartupRegistration();
        var controller = new HoodConfigurationController(session, store, startup);

        for (var combination = 0; combination < 8; combination++)
        {
            var candidate = controller.Current with
            {
                Behavior = controller.Current.Behavior with
                {
                    Roaming = (combination & 1) != 0,
                    IdleAnimations = (combination & 2) != 0,
                    RandomEvents = (combination & 4) != 0
                }
            };
            controller.SaveAndApply("behavior", candidate);

            Assert.AreEqual((combination & 1) != 0, controller.Current.Behavior.Roaming);
            Assert.AreEqual((combination & 2) != 0, controller.Current.Behavior.IdleAnimations);
            Assert.AreEqual((combination & 4) != 0, controller.Current.Behavior.RandomEvents);
            Assert.IsTrue(controller.Current.Behavior.AlwaysOnTop);
            Assert.IsFalse(controller.Current.Behavior.StartWithWindows);
            Assert.AreEqual(37.25, controller.Current.Needs.HungerPointsPerHour);
            Assert.AreEqual(23, controller.Current.Sleep.EarlyWakeGraceMinutes);
            Assert.AreEqual(controller.Current, store.LoadOrCreate());
        }

        controller.SaveAndApply("behavior.start_with_windows", controller.Current with
        {
            Behavior = controller.Current.Behavior with { StartWithWindows = true }
        });
        Assert.IsTrue(startup.Enabled);
        Assert.IsTrue(controller.Current.Behavior.AlwaysOnTop);

        controller.SaveAndApply("behavior.always_on_top", controller.Current with
        {
            Behavior = controller.Current.Behavior with { AlwaysOnTop = false }
        });
        Assert.IsTrue(startup.Enabled);
        Assert.IsFalse(controller.Current.Behavior.AlwaysOnTop);

        controller.SaveAndApply("behavior.start_with_windows", controller.Current with
        {
            Behavior = controller.Current.Behavior with { StartWithWindows = false }
        });
        Assert.IsFalse(startup.Enabled);
        Assert.IsFalse(controller.Current.Behavior.AlwaysOnTop);
        Assert.AreEqual(controller.Current, store.LoadOrCreate());
    }

    [TestMethod]
    public void LoadOrCreate_RejectsInvalidYamlWithoutChangingTheFile()
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        store.LoadOrCreate();
        var baseline = File.ReadAllText(store.Path);
        var invalidDocuments = new (string Name, string Yaml)[]
        {
            ("malformed YAML", "behavior: [\n"),
            ("duplicate key", baseline + "schema_version: 1\n"),
            ("unknown key", baseline + "unknown_setting: true\n"),
            ("unsupported version", ReplaceSetting(baseline, "schema_version", "2")),
            ("wrong type", ReplaceSetting(baseline, "roaming", "not-a-boolean")),
            ("non-finite number", ReplaceSetting(baseline, "hunger_points_per_hour", ".nan")),
            ("negative rate", ReplaceSetting(baseline, "hunger_points_per_hour", "-0.1")),
            ("negative gain", ReplaceSetting(baseline, "feed_gain", "-1")),
            ("zero threshold", ReplaceSetting(baseline, "automatic_nap_threshold", "0")),
            ("threshold over 100", ReplaceSetting(baseline, "automatic_nap_threshold", "101")),
            ("zero nap duration", ReplaceSetting(baseline, "duration_minutes", "0")),
            ("zero grace duration", ReplaceSetting(baseline, "early_wake_grace_minutes", "0")),
            ("zero random duration", ReplaceSetting(baseline, "minimum_minutes", "0")),
            ("inverted random range", ReplaceSetting(baseline, "maximum_minutes", "0.5")),
            ("zero area width", ReplaceSetting(baseline, "width", "0")),
            ("zero area height", ReplaceSetting(baseline, "height", "0")),
            ("negative margin", ReplaceSetting(baseline, "bottom_margin", "-1"))
        };

        foreach (var (name, invalidYaml) in invalidDocuments)
        {
            File.WriteAllText(store.Path, invalidYaml);
            var exception = Assert.Throws<HoodConfigurationException>(
                () => store.LoadOrCreate(), name);

            Assert.IsFalse(string.IsNullOrWhiteSpace(exception.Message), name);
            Assert.AreEqual(invalidYaml, File.ReadAllText(store.Path), $"The {name} file must remain intact.");
        }
    }

    [TestMethod]
    public void SaveAndApply_ReactionTimingPersistsBothBoundsAndReschedulesTheCurrentWait()
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        var initial = store.LoadOrCreate() with
        {
            Behavior = HoodConfiguration.Defaults.Behavior with { Roaming = false }
        };
        store.Save(initial);
        var session = CreateSession(initial);
        var controller = new HoodConfigurationController(session, store);
        session.Advance(TimeSpan.FromSeconds(4));

        var candidate = initial with
        {
            RandomInterval = new RandomIntervalSettings { MinimumMinutes = 2d / 60, MaximumMinutes = 3d / 60 }
        };
        controller.SaveAndApply("random_interval", candidate);

        Assert.AreEqual(candidate, store.LoadOrCreate());
        Assert.AreEqual(candidate, session.Configuration);
        session.Advance(TimeSpan.FromMilliseconds(1999));
        Assert.AreEqual(PetAction.Idle, session.Presentation.Action);
        session.Advance(TimeSpan.FromMilliseconds(1));
        Assert.AreEqual(PetAction.Wave, session.Presentation.Action);
    }

    [TestMethod]
    public void SaveAndApply_WhenStorageFails_KeepsTheEffectiveSettingsAndNamesTheField()
    {
        var initial = HoodConfiguration.Defaults;
        var session = CreateSession(initial);
        var controller = new HoodConfigurationController(session, new FailingConfigurationStore());
        var candidate = initial with
        {
            Behavior = initial.Behavior with { AlwaysOnTop = false },
            RandomInterval = new RandomIntervalSettings { MinimumMinutes = 2, MaximumMinutes = 3 }
        };

        var exception = Assert.Throws<HoodConfigurationException>(
            () => controller.SaveAndApply("behavior.always_on_top", candidate));

        Assert.AreEqual("behavior.always_on_top", exception.SettingPath);
        Assert.AreEqual(initial, controller.Current);
        Assert.AreEqual(initial, session.Configuration);
        Assert.IsTrue(controller.Current.Behavior.AlwaysOnTop);
    }

    [TestMethod]
    public void SaveAndApply_WhenStartupRegistrationFails_KeepsThePreviousConfiguration()
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        var initial = store.LoadOrCreate();
        var session = CreateSession(initial);
        var startup = new RecordingStartupRegistration { FailOnValue = true };
        var controller = new HoodConfigurationController(session, store, startup);
        var candidate = initial with
        {
            Behavior = initial.Behavior with { StartWithWindows = true }
        };

        var exception = Assert.Throws<HoodConfigurationException>(
            () => controller.SaveAndApply("behavior.start_with_windows", candidate));

        Assert.AreEqual("behavior.start_with_windows", exception.SettingPath);
        Assert.IsTrue(exception.Message.Contains("Windows startup", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(initial, controller.Current);
        Assert.AreEqual(initial, session.Configuration);
        Assert.AreEqual(initial, store.LoadOrCreate());
        CollectionAssert.AreEqual(new[] { true }, startup.Calls.ToArray());
        Assert.IsFalse(startup.Enabled);
    }

    [TestMethod]
    public void SaveAndApply_WhenYamlSaveFails_RollsBackStartupRegistration()
    {
        var initial = HoodConfiguration.Defaults;
        var session = CreateSession(initial);
        var startup = new RecordingStartupRegistration();
        var controller = new HoodConfigurationController(session, new FailingConfigurationStore(), startup);
        var candidate = initial with
        {
            Behavior = initial.Behavior with { StartWithWindows = true }
        };

        var exception = Assert.Throws<HoodConfigurationException>(
            () => controller.SaveAndApply("behavior.start_with_windows", candidate));

        Assert.AreEqual("behavior.start_with_windows", exception.SettingPath);
        Assert.IsTrue(exception.Message.Contains("previous settings remain in effect", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(initial, controller.Current);
        Assert.AreEqual(initial, session.Configuration);
        CollectionAssert.AreEqual(new[] { true, false }, startup.Calls.ToArray());
        Assert.IsFalse(startup.Enabled);
    }

    [TestMethod]
    public void SaveAndApply_WhenStartupRollbackFails_ReportsThePossibleMismatch()
    {
        var initial = HoodConfiguration.Defaults;
        var session = CreateSession(initial);
        var startup = new RecordingStartupRegistration { FailOnValue = false };
        var controller = new HoodConfigurationController(session, new FailingConfigurationStore(), startup);
        var candidate = initial with
        {
            Behavior = initial.Behavior with { StartWithWindows = true }
        };

        var exception = Assert.Throws<HoodConfigurationException>(
            () => controller.SaveAndApply("behavior.start_with_windows", candidate));

        Assert.IsTrue(exception.Message.Contains("may not match config.yaml", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(initial, controller.Current);
        Assert.AreEqual(initial, session.Configuration);
        CollectionAssert.AreEqual(new[] { true, false }, startup.Calls.ToArray());
        Assert.IsTrue(startup.Enabled);
    }

    [TestMethod]
    public void Start_RejectsInvalidConfigurationBeforeCreatingSessionBehavior()
    {
        var invalid = HoodConfiguration.Defaults with
        {
            Needs = HoodConfiguration.Defaults.Needs with { FeedGain = double.PositiveInfinity }
        };

        var exception = Assert.Throws<HoodConfigurationException>(() => CreateSession(invalid));

        Assert.AreEqual("needs.feed_gain", exception.SettingPath);
    }

    private static string ReplaceSetting(string yaml, string setting, string value)
    {
        var pattern = $"(?m)^(\\s*{Regex.Escape(setting)}:\\s*).*$";
        var updated = Regex.Replace(yaml, pattern, $"${{1}}{value}");
        Assert.AreNotEqual(yaml, updated, $"The generated YAML should contain '{setting}'.");
        return updated;
    }

    private static HoodSession CreateSession(HoodConfiguration configuration)
    {
        var assetDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Hood");
        var json = File.ReadAllText(Path.Combine(assetDirectory, "hood.animations.json"));
        var png = File.ReadAllBytes(Path.Combine(assetDirectory, "hood_spritesheet.png"));
        var definition = PetDefinition.Parse(
            json,
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)),
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
        DisplayInfo[] displays = [new("Primary", new PetBounds(0, 0, 1920, 1040), IsPrimary: true)];

        return HoodSession.Start(definition, displays, new FixedRandomSource(), new NoopSessionStore(), configuration);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"hood-config-{Guid.NewGuid():N}");
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

    private sealed class FailingConfigurationStore : IYamlConfigurationStore
    {
        public HoodConfiguration LoadOrCreate() => throw new NotSupportedException();

        public void Save(HoodConfiguration configuration) => throw new IOException("Simulated write failure.");
    }

    private sealed class RecordingStartupRegistration : IWindowsStartupRegistration
    {
        public bool? FailOnValue { get; init; }
        public bool Enabled { get; private set; }
        public List<bool> Calls { get; } = [];

        public void SetEnabled(bool enabled)
        {
            Calls.Add(enabled);
            if (FailOnValue == enabled)
            {
                throw new IOException("Simulated Windows startup failure.");
            }

            Enabled = enabled;
        }
    }
}
