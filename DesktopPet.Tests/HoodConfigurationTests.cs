using System.Text.RegularExpressions;
using DesktopPet.Core;

namespace DesktopPet.Tests;

[TestClass]
public sealed class HoodConfigurationTests
{
    [TestMethod]
    [DataRow(0.5)]
    [DataRow(1.35)]
    [DataRow(2.0)]
    public void PetSizeDefaultsForOlderFilesAndRoundTrips(double scale)
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        store.LoadOrCreate();
        File.WriteAllText(store.Path, Regex.Replace(File.ReadAllText(store.Path), @"(?m)^pet_scale:.*\r?\n", string.Empty));
        var legacy = store.LoadOrCreate();
        Assert.AreEqual(1d, legacy.PetScale);
        store.Save(legacy with { PetScale = scale });
        Assert.AreEqual(scale, store.LoadOrCreate().PetScale);
    }

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(0.49)]
    [DataRow(2.01)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    public void InvalidPetSizeDoesNotReplaceSavedConfiguration(double scale)
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        var configuration = store.LoadOrCreate();
        var original = File.ReadAllText(store.Path);
        var exception = Assert.Throws<HoodConfigurationException>(() => store.Save(configuration with { PetScale = scale }));
        Assert.AreEqual("pet_scale", exception.SettingPath);
        Assert.AreEqual(original, File.ReadAllText(store.Path));
    }

    [TestMethod]
    public void SpriteVisibilityDefaultsForOlderFilesAndRoundTripsHiddenChoice()
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        Assert.IsTrue(store.LoadOrCreate().Behavior.ShowSprites);
        File.WriteAllText(store.Path, Regex.Replace(File.ReadAllText(store.Path), @"(?m)^  show_sprites:.*\r?\n", string.Empty));
        var legacy = store.LoadOrCreate();
        Assert.IsTrue(legacy.Behavior.ShowSprites);
        store.Save(legacy with { Behavior = legacy.Behavior with { ShowSprites = false } });
        Assert.IsFalse(store.LoadOrCreate().Behavior.ShowSprites);
    }

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
        StringAssert.Contains(yaml, "pet: hood");
        StringAssert.Contains(yaml, "ui_style: sticky-notes");
        StringAssert.Contains(yaml, "hunger_points_per_hour: 12.5");
        StringAssert.Contains(yaml, "minimum_minutes: 1");
        StringAssert.Contains(yaml, "maximum_minutes: 5");
        StringAssert.Contains(yaml, "default_color: yellow");
        StringAssert.Contains(yaml, "text_size: 16");
        StringAssert.Contains(yaml, "line_spacing: 2");
        StringAssert.Contains(yaml, "theme: system");
        StringAssert.Contains(yaml, "always_on_top: false");
        StringAssert.Contains(yaml, "animate_show_hide: true");
        Assert.AreEqual(0, Directory.GetFiles(directory.Path, "*.tmp").Length);

        File.WriteAllText(store.Path, ReplaceSetting(yaml, "hunger_points_per_hour", "37.25"));

        var edited = store.LoadOrCreate();
        Assert.AreEqual(37.25, edited.Needs.HungerPointsPerHour);
        Assert.AreEqual(16, edited.Needs.BoredomPointsPerHour);
    }

    [TestMethod]
    public void LoadOrCreate_LegacyConfigurationWithoutNotesUsesDefaultsAndPreservesHoodValues()
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        store.LoadOrCreate();
        var legacyYaml = Regex.Replace(
            File.ReadAllText(store.Path),
            @"(?m)^notes:\r?\n(?:[ \t]+[^\r\n]*(?:\r?\n|$))*",
            string.Empty);
        legacyYaml = ReplaceSetting(legacyYaml, "hunger_points_per_hour", "37.25");
        legacyYaml = ReplaceSetting(legacyYaml, "always_on_top", "false");
        File.WriteAllText(store.Path, legacyYaml);

        var configuration = store.LoadOrCreate();

        Assert.AreEqual(new NotesConfiguration(), configuration.Notes);
        Assert.AreEqual(37.25, configuration.Needs.HungerPointsPerHour);
        Assert.IsFalse(configuration.Behavior.AlwaysOnTop);
    }

    [TestMethod]
    [DataRow(1.0)]
    [DataRow(1.25)]
    [DataRow(3.0)]
    public void LineSpacingDefaultsForOlderFilesAndRoundTrips(double spacing)
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        store.LoadOrCreate();
        File.WriteAllText(store.Path, Regex.Replace(File.ReadAllText(store.Path), @"(?m)^  line_spacing:.*\r?\n", string.Empty));
        var legacy = store.LoadOrCreate();
        Assert.AreEqual(2.0, legacy.Notes.LineSpacing);
        store.Save(legacy with { Notes = legacy.Notes with { LineSpacing = spacing } });
        Assert.AreEqual(spacing, store.LoadOrCreate().Notes.LineSpacing);
    }

    [TestMethod]
    public void LoadOrCreate_ConfigurationWithoutUiStyleDefaultsToStickyNotesAndPreservesValues()
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        store.LoadOrCreate();
        var yaml = File.ReadAllText(store.Path);
        yaml = Regex.Replace(yaml, @"(?m)^ui_style:\s*.*(?:\r?\n|$)", string.Empty);
        yaml = ReplaceSetting(yaml, "hunger_points_per_hour", "37.25");
        yaml = ReplaceSetting(yaml, "theme", "dark");
        File.WriteAllText(store.Path, yaml);

        var restored = store.LoadOrCreate();

        Assert.AreEqual(UiStyleCatalog.StickyNotes, restored.UiStyle);
        Assert.AreEqual("dark", restored.Notes.Theme);
        Assert.AreEqual(37.25, restored.Needs.HungerPointsPerHour);
        store.Save(restored);
        Assert.AreEqual(UiStyleCatalog.StickyNotes, store.LoadOrCreate().UiStyle);
    }

    [TestMethod]
    [DataRow("STICKY-NOTES", "sticky-notes")]
    [DataRow("MINIMAL", "minimal")]
    [DataRow("SoFt", "soft")]
    [DataRow("STuDiO", "studio")]
    public void UiStyleAcceptsCaseInsensitiveInputAndSavesCanonicalLowercase(string input, string canonical)
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        store.LoadOrCreate();
        File.WriteAllText(store.Path, ReplaceSetting(File.ReadAllText(store.Path), "ui_style", input));

        var restored = store.LoadOrCreate();
        Assert.AreEqual(canonical, restored.UiStyle);
        store.Save(restored with { UiStyle = input });

        StringAssert.Contains(File.ReadAllText(store.Path), $"ui_style: {canonical}");
        Assert.AreEqual(canonical, store.LoadOrCreate().UiStyle);
    }

    [TestMethod]
    public void LoadOrCreate_RejectsBlankNullNumericAndUnknownUiStylesWithoutChangingTheFile()
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        store.LoadOrCreate();
        var baseline = File.ReadAllText(store.Path);

        foreach (var value in new[] { new string('"', 2), "null", "123", "existing", "unknown" })
        {
            var invalid = ReplaceSetting(baseline, "ui_style", value);
            File.WriteAllText(store.Path, invalid);
            var exception = Assert.Throws<HoodConfigurationException>(() => store.LoadOrCreate(), value);
            Assert.AreEqual("ui_style", exception.SettingPath, value);
            Assert.AreEqual(invalid, File.ReadAllText(store.Path), "Invalid configuration must remain available for correction.");
        }
    }

    [TestMethod]
    public void NotesAppearanceSettings_RoundTripWithoutChangingHoodsIndependentTopmostSetting()
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        var initial = store.LoadOrCreate();
        var candidate = initial with
        {
            Notes = initial.Notes with
            {
                DefaultColor = "blue",
                TextSize = 24,
                LineSpacing = 1.75,
                Theme = "dark",
                AlwaysOnTop = true,
                AnimateShowHide = false
            }
        };

        store.Save(candidate);
        var restored = store.LoadOrCreate();

        Assert.AreEqual(candidate, restored);
        Assert.IsTrue(restored.Notes.AlwaysOnTop);
        Assert.IsFalse(restored.Notes.AnimateShowHide);
        Assert.IsTrue(restored.Behavior.AlwaysOnTop);
        StringAssert.Contains(File.ReadAllText(store.Path), "default_color: blue");
        StringAssert.Contains(File.ReadAllText(store.Path), "theme: dark");
        StringAssert.Contains(File.ReadAllText(store.Path), "animate_show_hide: false");
    }

    [TestMethod]
    public void SaveAndApply_NotesSettingsPreserveHoodBehaviorAndContinuationState()
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        var initial = store.LoadOrCreate();
        var session = CreateSession(initial);
        session.Advance(TimeSpan.FromSeconds(2));
        var position = session.Position;
        var needs = session.Needs;
        var action = session.Presentation.Action;
        var mode = session.Mode;
        var remainingRandomInterval = session.RemainingRandomInterval;
        var remainingNap = session.RemainingNap;
        var controller = new HoodConfigurationController(session, store);

        controller.SaveAndApply("notes", initial with
        {
            Notes = initial.Notes with
            {
                AlwaysOnTop = true,
                TextSize = 20,
                Theme = "dark",
                AnimateShowHide = false
            }
        });
        Assert.IsTrue(controller.Current.Behavior.AlwaysOnTop);
        Assert.IsTrue(controller.Current.Notes.AlwaysOnTop);
        Assert.IsFalse(controller.Current.Notes.AnimateShowHide);
        Assert.AreEqual(position, session.Position);
        Assert.AreEqual(needs, session.Needs);
        Assert.AreEqual(action, session.Presentation.Action);
        Assert.AreEqual(mode, session.Mode);
        Assert.AreEqual(remainingRandomInterval, session.RemainingRandomInterval);
        Assert.AreEqual(remainingNap, session.RemainingNap);

        controller.SaveAndApply("behavior.always_on_top", controller.Current with
        {
            Behavior = controller.Current.Behavior with { AlwaysOnTop = false }
        });
        Assert.IsFalse(controller.Current.Behavior.AlwaysOnTop);
        Assert.IsTrue(controller.Current.Notes.AlwaysOnTop);
    }

    [TestMethod]
    public void Validate_ReportsInvalidNotesAppearanceSettingPathsAndRanges()
    {
        var defaults = HoodConfiguration.Defaults;
        var invalid = new (HoodConfiguration Configuration, string SettingPath)[]
        {
            (defaults with { Notes = defaults.Notes with { DefaultColor = "orange" } }, "notes.default_color"),
            (defaults with { Notes = defaults.Notes with { TextSize = 11 } }, "notes.text_size"),
            (defaults with { Notes = defaults.Notes with { TextSize = 33 } }, "notes.text_size"),
            (defaults with { Notes = defaults.Notes with { LineSpacing = 0.99 } }, "notes.line_spacing"),
            (defaults with { Notes = defaults.Notes with { LineSpacing = 3.01 } }, "notes.line_spacing"),
            (defaults with { Notes = defaults.Notes with { LineSpacing = double.NaN } }, "notes.line_spacing"),
            (defaults with { Notes = defaults.Notes with { LineSpacing = double.PositiveInfinity } }, "notes.line_spacing"),
            (defaults with { Notes = defaults.Notes with { LineSpacing = double.NegativeInfinity } }, "notes.line_spacing"),
            (defaults with { Notes = defaults.Notes with { Theme = "sepia" } }, "notes.theme")
        };

        foreach (var (configuration, settingPath) in invalid)
        {
            var exception = Assert.Throws<HoodConfigurationException>(
                () => HoodConfigurationValidator.Validate(configuration));
            Assert.AreEqual(settingPath, exception.SettingPath);
        }
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
            Behavior = initial.Behavior with { AlwaysOnTop = false, ShowSprites = false },
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
    public void SaveAndApply_WhenUiStyleStorageFails_KeepsThePreviousEffectiveStyle()
    {
        var initial = HoodConfiguration.Defaults;
        var session = CreateSession(initial);
        var controller = new HoodConfigurationController(session, new FailingConfigurationStore());

        var exception = Assert.Throws<HoodConfigurationException>(() =>
            controller.SaveAndApply("ui_style", initial with { UiStyle = UiStyleCatalog.Minimal }));

        Assert.AreEqual("ui_style", exception.SettingPath);
        Assert.AreEqual(UiStyleCatalog.StickyNotes, controller.Current.UiStyle);
        Assert.AreEqual(initial, session.Configuration);
    }

    [TestMethod]
    public void SaveAndApply_WhenNotesAppearanceSaveFails_KeepsThePreviousSettingsAndNamesTheField()
    {
        var initial = HoodConfiguration.Defaults;
        var session = CreateSession(initial);
        var controller = new HoodConfigurationController(session, new FailingConfigurationStore());
        var candidate = initial with
        {
            Notes = initial.Notes with { DefaultColor = "pink", TextSize = 24, AlwaysOnTop = true }
        };

        var exception = Assert.Throws<HoodConfigurationException>(
            () => controller.SaveAndApply("notes.text_size", candidate));

        Assert.AreEqual("notes.text_size", exception.SettingPath);
        Assert.AreEqual(initial, controller.Current);
        Assert.AreEqual(initial, session.Configuration);
        Assert.AreEqual("yellow", controller.Current.Notes.DefaultColor);
        Assert.IsFalse(controller.Current.Notes.AlwaysOnTop);
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

    [TestMethod]
    public void ConfigurationWithoutPetDefaultsToHoodAndPreservesExistingSettings()
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        var original = HoodConfiguration.Defaults with { UiStyle = UiStyleCatalog.Studio };
        store.Save(original);
        var legacy = Regex.Replace(File.ReadAllText(store.Path), @"(?m)^pet:.*(?:\r?\n|$)", string.Empty);
        File.WriteAllText(store.Path, legacy);

        Assert.AreEqual(original, store.LoadOrCreate());
        Assert.AreEqual(legacy, File.ReadAllText(store.Path), "Loading must not rewrite a legacy file.");
    }

    [TestMethod]
    [DataRow("HOOD", "hood")]
    [DataRow("PiP", "pip")]
    [DataRow("CRUMB", "crumb")]
    [DataRow("Puddle", "puddle")]
    public void PetSelectionRoundTripsCanonicalNamesAndPreservesTheSession(string input, string canonical)
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        var original = store.LoadOrCreate();
        File.WriteAllText(store.Path, ReplaceSetting(File.ReadAllText(store.Path), "pet", input));
        Assert.AreEqual(canonical, store.LoadOrCreate().Pet);
        var session = CreateSession(original);
        session.Sleep();
        session.Advance(TimeSpan.FromMilliseconds(900));
        var presentation = session.Presentation;
        var nap = session.RemainingNap;
        var needs = session.Needs;
        var controller = new HoodConfigurationController(session, store);

        controller.SaveAndApply("pet", original with { Pet = input });

        Assert.AreEqual(canonical, controller.Current.Pet);
        Assert.AreEqual(original with { Pet = canonical }, store.LoadOrCreate());
        StringAssert.Contains(File.ReadAllText(store.Path), $"pet: {canonical}");
        Assert.AreEqual(presentation, session.Presentation);
        Assert.AreEqual(needs, session.Needs);
        Assert.AreEqual(nap, session.RemainingNap);
    }

    [TestMethod]
    public void InvalidPetIdentifiersAreRejectedWithoutRewritingConfiguration()
    {
        using var directory = new TemporaryDirectory();
        var store = new YamlConfigurationStore(Path.Combine(directory.Path, "config.yaml"));
        store.LoadOrCreate();
        var baseline = File.ReadAllText(store.Path);
        foreach (var value in new[] { "\"\"", "null", "123", "fox", "../Pip" })
        {
            var invalid = ReplaceSetting(baseline, "pet", value);
            File.WriteAllText(store.Path, invalid);
            var error = Assert.Throws<HoodConfigurationException>(() => store.LoadOrCreate());
            Assert.AreEqual("pet", error.SettingPath);
            Assert.AreEqual(invalid, File.ReadAllText(store.Path));
        }
    }

    [TestMethod]
    public void PetSaveFailureKeepsTheEffectivePetAndContinuationState()
    {
        var session = CreateSession(HoodConfiguration.Defaults);
        session.Feed();
        session.Advance(TimeSpan.FromMilliseconds(180));
        var presentation = session.Presentation;
        var controller = new HoodConfigurationController(session, new FailingConfigurationStore());

        var error = Assert.Throws<HoodConfigurationException>(() =>
            controller.SaveAndApply("pet", controller.Current with { Pet = PetCatalog.Pip }));

        Assert.AreEqual("pet", error.SettingPath);
        Assert.AreEqual(PetCatalog.Hood, controller.Current.Pet);
        Assert.AreEqual(presentation, session.Presentation);
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
