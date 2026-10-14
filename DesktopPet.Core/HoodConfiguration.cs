using System.Text;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DesktopPet.Core;

public sealed record HoodConfiguration
{
    public int? SchemaVersion { get; init; } = 1;
    public string Pet { get; init; } = PetCatalog.Hood;
    public double PetScale { get; init; } = 1;
    public string UiStyle { get; init; } = UiStyleCatalog.StickyNotes;
    public BehaviorSettings Behavior { get; init; } = new();
    public NeedSettings Needs { get; init; } = new();
    public SleepSettings Sleep { get; init; } = new();
    public RandomIntervalSettings RandomInterval { get; init; } = new();
    public InitialAreaSettings InitialArea { get; init; } = new();
    public NotesConfiguration Notes { get; init; } = new();

    public static HoodConfiguration Defaults => new();
}

public static class PetCatalog
{
    public const string Hood = "hood";
    public const string Pip = "pip";
    public const string Crumb = "crumb";
    public const string Puddle = "puddle";

    public static IReadOnlyList<string> Supported { get; } = Array.AsReadOnly([Hood, Pip, Crumb, Puddle]);

    public static bool TryCanonicalize(string? value, out string canonical)
    {
        canonical = value?.ToLowerInvariant() ?? string.Empty;
        return Supported.Contains(canonical, StringComparer.Ordinal);
    }

    public static string Canonicalize(string? value) => TryCanonicalize(value, out var canonical)
        ? canonical
        : throw new HoodConfigurationException("pet", "Choose hood, pip, crumb, or puddle.");
}

public static class UiStyleCatalog
{
    public const string StickyNotes = "sticky-notes";
    public const string Minimal = "minimal";
    public const string Soft = "soft";
    public const string Studio = "studio";

    public static IReadOnlyList<string> Supported { get; } = Array.AsReadOnly([StickyNotes, Minimal, Soft, Studio]);

    public static bool TryCanonicalize(string? value, out string canonical)
    {
        canonical = value?.ToLowerInvariant() ?? string.Empty;
        return Supported.Contains(canonical, StringComparer.Ordinal);
    }

    public static string Canonicalize(string? value) => TryCanonicalize(value, out var canonical)
        ? canonical
        : throw new HoodConfigurationException("ui_style", "Choose sticky-notes, minimal, soft, or studio.");
}

public sealed record BehaviorSettings
{
    public bool ShowSprites { get; init; } = true;
    public bool MediaHeadphones { get; init; } = true;
    public bool Roaming { get; init; } = true;
    public bool IdleAnimations { get; init; } = true;
    public bool RandomEvents { get; init; } = true;
    public bool AlwaysOnTop { get; init; } = true;
    public bool StartWithWindows { get; init; }
}

public sealed record NeedSettings
{
    public double HungerPointsPerHour { get; init; } = 12.5;
    public double BoredomPointsPerHour { get; init; } = 16;
    public double SleepPointsPerHour { get; init; } = 20;
    public double FeedGain { get; init; } = 40;
    public double MovementReliefPerSecond { get; init; } = 0.5;
    public double StationaryClickRelief { get; init; } = 10;
}

public sealed record SleepSettings
{
    public double AutomaticNapThreshold { get; init; } = 80;
    public double DurationMinutes { get; init; } = 5;
    public double EarlyWakeGraceMinutes { get; init; } = 15;
}

public sealed record RandomIntervalSettings
{
    public double MinimumMinutes { get; init; } = 1;
    public double MaximumMinutes { get; init; } = 5;
}

public sealed record InitialAreaSettings
{
    public double Width { get; init; } = 480;
    public double Height { get; init; } = 180;
    public double BottomMargin { get; init; } = 16;
}

public sealed record NotesConfiguration
{
    public string DefaultColor { get; init; } = "yellow";
    public int TextSize { get; init; } = 16;
    public double LineSpacing { get; init; } = 2;
    public string Theme { get; init; } = "system";
    public bool AlwaysOnTop { get; init; }
    public bool AnimateShowHide { get; init; } = true;
}

public enum NoteColor
{
    Yellow,
    Blue,
    Green,
    Pink,
    Lavender
}

public enum NoteTheme
{
    System,
    Light,
    Dark
}

public static class NoteColorPalette
{
    private sealed record StyleBackgrounds(string[] Light, string[] Dark);

    private static readonly IReadOnlyDictionary<string, StyleBackgrounds> Backgrounds =
        new Dictionary<string, StyleBackgrounds>(StringComparer.Ordinal)
        {
            [UiStyleCatalog.StickyNotes] = new(
                ["#FFFFF4B8", "#FFDCEEFF", "#FFE2F4E7", "#FFFBE1E7", "#FFEDE3FA"],
                ["#FF4A421F", "#FF243A4D", "#FF284333", "#FF49303A", "#FF3F344F"]),
            [UiStyleCatalog.Minimal] = new(
                ["#FFF4F0D8", "#FFDDEAF3", "#FFE1EEE5", "#FFF2E3E5", "#FFE8E5F1"],
                ["#FF48432C", "#FF273C4B", "#FF2B4034", "#FF46343B", "#FF39354A"]),
            [UiStyleCatalog.Soft] = new(
                ["#FFF5E2BC", "#FFD9EAE7", "#FFDCEBDD", "#FFF4DED5", "#FFEBDFEB"],
                ["#FF4B3826", "#FF27413F", "#FF2D4234", "#FF493330", "#FF453447"]),
            [UiStyleCatalog.Studio] = new(
                ["#FFFFE9AF", "#FFD8E5FB", "#FFD6EEE0", "#FFF5DDE8", "#FFE5DCF7"],
                ["#FF51401F", "#FF293A5A", "#FF294437", "#FF492E3C", "#FF3D3155"])
        };

    public static IReadOnlyList<NoteColor> Colors { get; } = Array.AsReadOnly(Enum.GetValues<NoteColor>());

    public static bool TryParse(string? value, out NoteColor color) =>
        Enum.TryParse(value, ignoreCase: true, out color) && Enum.IsDefined(color);

    public static NoteColor Parse(string value) => TryParse(value, out var color)
        ? color
        : throw new ArgumentOutOfRangeException(nameof(value), value, "Choose a supported note color.");

    public static string GetBackgroundHex(
        NoteColor color,
        bool darkTheme,
        string style = UiStyleCatalog.StickyNotes)
    {
        if (!Enum.IsDefined(color))
        {
            throw new ArgumentOutOfRangeException(nameof(color), color, "Choose a supported note color.");
        }

        var palette = Backgrounds[UiStyleCatalog.Canonicalize(style)];
        return (darkTheme ? palette.Dark : palette.Light)[(int)color];
    }

    public static string GetForegroundHex(bool darkTheme) => darkTheme ? "#FFF2F4F7" : "#FF1B1F24";
}

public sealed class HoodConfigurationException : Exception
{
    public HoodConfigurationException(string settingPath, string message, Exception? innerException = null)
        : base($"{message} Setting: {settingPath}. Correct config.yaml and relaunch; the original file is preserved.", innerException)
    {
        SettingPath = settingPath;
    }

    public string SettingPath { get; }
}

public static class HoodConfigurationValidator
{
    public static void Validate(HoodConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.SchemaVersion != 1)
        {
            throw new HoodConfigurationException("schema_version", "This application requires schema version 1.");
        }

        if (!UiStyleCatalog.TryCanonicalize(configuration.UiStyle, out _))
        {
            throw new HoodConfigurationException("ui_style", "Choose sticky-notes, minimal, soft, or studio.");
        }

        if (!PetCatalog.TryCanonicalize(configuration.Pet, out _))
        {
            throw new HoodConfigurationException("pet", "Choose hood, pip, crumb, or puddle.");
        }

        ValidatePetScale(configuration.PetScale);

        if (configuration.Behavior is null)
        {
            throw new HoodConfigurationException("behavior", "This section must be a YAML mapping.");
        }

        if (configuration.Needs is null)
        {
            throw new HoodConfigurationException("needs", "This section must be a YAML mapping.");
        }

        if (configuration.Sleep is null)
        {
            throw new HoodConfigurationException("sleep", "This section must be a YAML mapping.");
        }

        if (configuration.RandomInterval is null)
        {
            throw new HoodConfigurationException("random_interval", "This section must be a YAML mapping.");
        }

        if (configuration.InitialArea is null)
        {
            throw new HoodConfigurationException("initial_area", "This section must be a YAML mapping.");
        }

        if (configuration.Notes is null)
        {
            throw new HoodConfigurationException("notes", "This section must be a YAML mapping.");
        }

        if (!NoteColorPalette.TryParse(configuration.Notes.DefaultColor, out _))
        {
            throw new HoodConfigurationException("notes.default_color", "Choose yellow, blue, green, pink, or lavender.");
        }

        if (configuration.Notes.TextSize is < 12 or > 32)
        {
            throw new HoodConfigurationException("notes.text_size", "Note text size must be a whole number from 12 through 32 points.");
        }

        if (!double.IsFinite(configuration.Notes.LineSpacing) || configuration.Notes.LineSpacing is < 1 or > 3)
        {
            throw new HoodConfigurationException("notes.line_spacing", "Note line spacing must be a finite number from 1 through 3.");
        }

        if (!Enum.TryParse<NoteTheme>(configuration.Notes.Theme, ignoreCase: true, out var noteTheme) || !Enum.IsDefined(noteTheme))
        {
            throw new HoodConfigurationException("notes.theme", "Choose system, light, or dark.");
        }

        RequireFiniteNonnegative(configuration.Needs.HungerPointsPerHour, "needs.hunger_points_per_hour");
        RequireFiniteNonnegative(configuration.Needs.BoredomPointsPerHour, "needs.boredom_points_per_hour");
        RequireFiniteNonnegative(configuration.Needs.SleepPointsPerHour, "needs.sleep_points_per_hour");
        RequireFiniteNonnegative(configuration.Needs.FeedGain, "needs.feed_gain");
        RequireFiniteNonnegative(configuration.Needs.MovementReliefPerSecond, "needs.movement_relief_per_second");
        RequireFiniteNonnegative(configuration.Needs.StationaryClickRelief, "needs.stationary_click_relief");

        var threshold = configuration.Sleep.AutomaticNapThreshold;
        if (!double.IsFinite(threshold) || threshold <= 0 || threshold > 100)
        {
            throw new HoodConfigurationException("sleep.automatic_nap_threshold", "The sleep threshold must be greater than 0 and no more than 100 need points.");
        }

        RequirePositive(configuration.Sleep.DurationMinutes, "sleep.duration_minutes");
        RequirePositive(configuration.Sleep.EarlyWakeGraceMinutes, "sleep.early_wake_grace_minutes");
        RequirePositive(configuration.RandomInterval.MinimumMinutes, "random_interval.minimum_minutes");
        RequirePositive(configuration.RandomInterval.MaximumMinutes, "random_interval.maximum_minutes");
        if (configuration.RandomInterval.MinimumMinutes > configuration.RandomInterval.MaximumMinutes)
        {
            throw new HoodConfigurationException("random_interval", "Minimum interval must not exceed maximum interval.");
        }

        RequirePositive(configuration.InitialArea.Width, "initial_area.width");
        RequirePositive(configuration.InitialArea.Height, "initial_area.height");
        RequireFiniteNonnegative(configuration.InitialArea.BottomMargin, "initial_area.bottom_margin");
    }

    public static void ValidatePetScale(double scale)
    {
        if (!double.IsFinite(scale) || scale is < 0.5 or > 2)
        {
            throw new HoodConfigurationException("pet_scale", "Pet size must be a finite number from 0.5 through 2 (50% through 200%).");
        }
    }

    private static void RequirePositive(double value, string settingPath)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new HoodConfigurationException(settingPath, "The value must be a finite number greater than 0.");
        }
    }

    private static void RequireFiniteNonnegative(double value, string settingPath)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new HoodConfigurationException(settingPath, "The value must be a finite number equal to or greater than 0.");
        }
    }
}

public interface IYamlConfigurationStore
{
    HoodConfiguration LoadOrCreate();
    void Save(HoodConfiguration configuration);
}

public interface IWindowsStartupRegistration
{
    void SetEnabled(bool enabled);
}

public sealed class YamlConfigurationStore : IYamlConfigurationStore
{
    private const long MaximumFileSizeBytes = 64 * 1024;
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithDuplicateKeyChecking()
        .WithMaximumRecursion(32)
        .Build();
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .DisableAliases()
        .Build();

    private readonly string _path;

    public YamlConfigurationStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = System.IO.Path.GetFullPath(path);
    }

    public string Path => _path;

    public HoodConfiguration LoadOrCreate()
    {
        if (!File.Exists(_path))
        {
            var defaults = HoodConfiguration.Defaults;
            Save(defaults);
            return defaults;
        }

        try
        {
            var file = new FileInfo(_path);
            if (file.Length > MaximumFileSizeBytes)
            {
                throw new HoodConfigurationException("config.yaml", "The configuration file exceeds the 64 KiB size limit.");
            }

            return Parse(File.ReadAllText(_path));
        }
        catch (HoodConfigurationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new HoodConfigurationException("config.yaml", $"The configuration file could not be read: {exception.Message}", exception);
        }
    }

    public void Save(HoodConfiguration configuration)
    {
        HoodConfigurationValidator.Validate(configuration);
        var yaml = Serializer.Serialize(configuration with
        {
            Pet = PetCatalog.Canonicalize(configuration.Pet),
            UiStyle = UiStyleCatalog.Canonicalize(configuration.UiStyle)
        });
        try
        {
            AtomicFile.WriteAllText(_path, yaml);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new HoodConfigurationException("config.yaml", $"The configuration could not be saved: {exception.Message}", exception);
        }
    }

    private static HoodConfiguration Parse(string yaml)
    {
        try
        {
            var yamlStream = new YamlStream();
            yamlStream.Load(new Parser(new StringReader(yaml)));
            if (yamlStream.Documents.Count != 1 || yamlStream.Documents[0].RootNode is not YamlMappingNode)
            {
                throw new HoodConfigurationException("config.yaml", "The configuration must contain exactly one YAML mapping document.");
            }

            using var reader = new StringReader(yaml);
            var configuration = Deserializer.Deserialize<HoodConfiguration>(reader)
                ?? throw new HoodConfigurationException("config.yaml", "The configuration document is empty.");
            HoodConfigurationValidator.Validate(configuration);
            return configuration with
            {
                Pet = PetCatalog.Canonicalize(configuration.Pet),
                UiStyle = UiStyleCatalog.Canonicalize(configuration.UiStyle)
            };
        }
        catch (HoodConfigurationException)
        {
            throw;
        }
        catch (YamlException exception)
        {
            var mark = exception.Start;
            throw new HoodConfigurationException(
                "config.yaml",
                $"The YAML is invalid near line {mark.Line}, column {mark.Column}: {exception.Message}",
                exception);
        }
    }
}

public sealed class HoodConfigurationController
{
    private readonly HoodSession _session;
    private readonly IYamlConfigurationStore _store;
    private readonly IWindowsStartupRegistration? _startupRegistration;

    public HoodConfigurationController(
        HoodSession session,
        IYamlConfigurationStore store,
        IWindowsStartupRegistration? startupRegistration = null)
    {
        _session = session;
        _store = store;
        _startupRegistration = startupRegistration;
    }

    public HoodConfiguration Current => _session.Configuration;

    public void SaveAndApply(string settingPath, HoodConfiguration candidate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingPath);
        HoodConfigurationValidator.Validate(candidate);
        _session.ValidatePetScale(candidate.PetScale);
        candidate = candidate with
        {
            Pet = PetCatalog.Canonicalize(candidate.Pet),
            UiStyle = UiStyleCatalog.Canonicalize(candidate.UiStyle)
        };
        var previous = Current;
        var startupChanged = candidate.Behavior.StartWithWindows != previous.Behavior.StartWithWindows;
        if (startupChanged && _startupRegistration is null)
        {
            throw new HoodConfigurationException(
                settingPath,
                "Windows startup integration is unavailable, so the setting was not changed.");
        }

        var startupApplied = false;
        try
        {
            if (startupChanged)
            {
                _startupRegistration!.SetEnabled(candidate.Behavior.StartWithWindows);
                startupApplied = true;
            }

            _store.Save(candidate);
        }
        catch (Exception exception) when (exception is HoodConfigurationException or IOException or UnauthorizedAccessException)
        {
            if (startupApplied)
            {
                try
                {
                    _startupRegistration!.SetEnabled(previous.Behavior.StartWithWindows);
                }
                catch (Exception rollbackException) when (
                    rollbackException is HoodConfigurationException or IOException or UnauthorizedAccessException)
                {
                    throw new HoodConfigurationException(
                        settingPath,
                        $"The configuration could not be saved, and Windows startup registration could not be restored. The startup registration may not match config.yaml; reopen Settings and try again. {exception.Message} Rollback error: {rollbackException.Message}",
                        new AggregateException(exception, rollbackException));
                }
            }

            throw new HoodConfigurationException(
                settingPath,
                $"The change to '{settingPath}' was not saved, so the previous settings remain in effect. {exception.Message}",
                exception);
        }

        _session.ApplyConfiguration(candidate);
    }
}
