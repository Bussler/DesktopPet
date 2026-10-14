namespace DesktopPet.Windows;

internal readonly record struct MediaAudioSample(
    string SessionId,
    string ProcessName,
    float Peak,
    bool IsMuted,
    float SessionVolume,
    bool EndpointMuted,
    float EndpointVolume,
    string EndpointId = "");

internal readonly record struct MediaAudioEndpoint(string EndpointId, bool IsMuted, float Volume);

internal sealed record MediaAudioObservation(bool IsAvailable, IReadOnlyList<MediaAudioSample> Samples,
    IReadOnlyList<MediaAudioEndpoint>? Endpoints = null)
{
    public static MediaAudioObservation Unavailable { get; } = new(false, [], []);
}

internal sealed class MediaHeadphonePolicy
{
    private static readonly TimeSpan SilenceGrace = TimeSpan.FromSeconds(3);
    private static readonly HashSet<string> EligibleProcesses = new(StringComparer.OrdinalIgnoreCase)
        { "spotify", "chrome", "msedge", "firefox" };
    private const float AudibleLevel = 0.001f;
    private readonly Dictionary<string, HeardSource> _heardUntil = new(StringComparer.Ordinal);

    private readonly record struct HeardSource(TimeSpan Until, string EndpointId);

    public void Reset() => _heardUntil.Clear();

    public bool Update(MediaAudioObservation observation, TimeSpan elapsed, bool enabled)
    {
        if (!enabled || !observation.IsAvailable)
        {
            _heardUntil.Clear();
            return false;
        }

        var mutedEndpointIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var endpoint in observation.Endpoints ?? [])
        {
            if (!endpoint.IsMuted && endpoint.Volume > 0) continue;
            mutedEndpointIds.Add(endpoint.EndpointId);
            foreach (var sessionId in _heardUntil
                .Where(pair => pair.Value.EndpointId == endpoint.EndpointId)
                .Select(pair => pair.Key).ToArray())
                _heardUntil.Remove(sessionId);
        }

        foreach (var sample in observation.Samples)
        {
            if (!IsEligible(sample.ProcessName)) continue;
            if (sample.IsMuted || sample.SessionVolume <= 0 || sample.EndpointMuted || sample.EndpointVolume <= 0 ||
                (sample.EndpointId.Length > 0 && mutedEndpointIds.Contains(sample.EndpointId)))
            {
                _heardUntil.Remove(sample.SessionId);
                continue;
            }

            if (sample.Peak * sample.SessionVolume * sample.EndpointVolume >= AudibleLevel)
            {
                _heardUntil[sample.SessionId] = new HeardSource(elapsed + SilenceGrace, sample.EndpointId);
            }
        }

        foreach (var sessionId in _heardUntil.Where(pair => pair.Value.Until <= elapsed).Select(pair => pair.Key).ToArray())
            _heardUntil.Remove(sessionId);

        return _heardUntil.Count > 0;
    }

    internal static bool IsEligible(string processName) =>
        EligibleProcesses.Contains(processName);
}
