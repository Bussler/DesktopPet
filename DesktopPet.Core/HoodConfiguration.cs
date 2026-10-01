using System.Text;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DesktopPet.Core;

public sealed record HoodConfiguration
{
    public int? SchemaVersion { get; init; } = 1;
    public BehaviorSettings Behavior { get; init; } = new();
    public NeedSettings Needs { get; init; } = new();
    public SleepSettings Sleep { get; init; } = new();
    public RandomIntervalSettings RandomInterval { get; init; } = new();
    public InitialAreaSettings InitialArea { get; init; } = new();

    public static HoodConfiguration Defaults => new();
}

public sealed record BehaviorSettings
{
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
    public double MinimumMinutes { get; init; } = 8;
    public double MaximumMinutes { get; init; } = 15;
}

public sealed record InitialAreaSettings
{
    public double Width { get; init; } = 480;
    public double Height { get; init; } = 180;
    public double BottomMargin { get; init; } = 16;
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
        var yaml = Serializer.Serialize(configuration);
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
            return configuration;
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
