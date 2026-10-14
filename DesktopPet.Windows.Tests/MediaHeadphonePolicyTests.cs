using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[TestClass]
public sealed class MediaHeadphonePolicyTests
{
    [TestMethod]
    public void AudibleSpotifyAndSupportedBrowserProcessesActivateHeadphones()
    {
        foreach (var process in new[] { "spotify", "Spotify", "chrome", "msedge", "firefox", "Chrome", "MSEDGE", "Firefox" })
        {
            var policy = new MediaHeadphonePolicy();
            var observation = Available(new MediaAudioSample("device:session", process, 0.02f, false, 1, false, 1));
            Assert.IsTrue(policy.Update(observation, TimeSpan.Zero, enabled: true), process);
        }

        var unrelated = new MediaHeadphonePolicy();
        Assert.IsFalse(unrelated.Update(Available(new MediaAudioSample("device:session", "spotify", 0, false, 1, false, 1)),
            TimeSpan.Zero, enabled: true));
        Assert.IsFalse(unrelated.Update(Available(new MediaAudioSample("device:session", "notepad", 0.8f, false, 1, false, 1)),
            TimeSpan.Zero, enabled: true));
        Assert.IsFalse(unrelated.Update(Available(new MediaAudioSample("device:session", "msedgewebview2", 0.8f, false, 1, false, 1)),
            TimeSpan.Zero, enabled: true));
    }

    [TestMethod]
    public void SoundThresholdAcceptsQuietSpotifyAndRejectsSilenceBelowThreshold()
    {
        var policy = new MediaHeadphonePolicy();
        Assert.IsTrue(policy.Update(Available(new MediaAudioSample("quiet", "spotify", 0.001f, false, 1, false, 1)),
            TimeSpan.Zero, enabled: true));

        policy.Reset();
        Assert.IsFalse(policy.Update(Available(new MediaAudioSample("silent", "spotify", 0.0009f, false, 1, false, 1)),
            TimeSpan.Zero, enabled: true));
    }

    [TestMethod]
    public void QuietGraceLastsThreeSecondsAndFreshSoundRenewsIt()
    {
        var policy = new MediaHeadphonePolicy();
        Assert.IsTrue(policy.Update(Sound("spotify", "session"), TimeSpan.Zero, enabled: true));
        Assert.IsTrue(policy.Update(Available(), TimeSpan.FromMilliseconds(2_999), enabled: true));
        Assert.IsFalse(policy.Update(Available(), TimeSpan.FromSeconds(3), enabled: true));

        policy.Reset();
        Assert.IsTrue(policy.Update(Sound("spotify", "session"), TimeSpan.Zero, enabled: true));
        Assert.IsTrue(policy.Update(Sound("spotify", "session"), TimeSpan.FromSeconds(2), enabled: true));
        Assert.IsTrue(policy.Update(Available(), TimeSpan.FromMilliseconds(4_999), enabled: true));
        Assert.IsFalse(policy.Update(Available(), TimeSpan.FromSeconds(5), enabled: true));
    }

    [TestMethod]
    public void MutingOneSessionClearsItsGraceWithoutClearingAnotherAudibleSession()
    {
        var policy = new MediaHeadphonePolicy();
        Assert.IsTrue(policy.Update(Available(
            new MediaAudioSample("device:spotify", "spotify", 0.1f, false, 1, false, 1),
            new MediaAudioSample("device:spotify-second-session", "spotify", 0.1f, false, 1, false, 1)),
            TimeSpan.Zero, enabled: true));

        Assert.IsTrue(policy.Update(Available(
            new MediaAudioSample("device:spotify", "spotify", 0, true, 1, false, 1),
            new MediaAudioSample("device:spotify-second-session", "spotify", 0, false, 1, false, 1)),
            TimeSpan.FromMilliseconds(200), enabled: true));
        Assert.IsFalse(policy.Update(Available(
            new MediaAudioSample("device:spotify", "spotify", 0, true, 1, false, 1),
            new MediaAudioSample("device:spotify-second-session", "spotify", 0, false, 1, false, 1)),
            TimeSpan.FromMilliseconds(3_201), enabled: true));
    }

    [TestMethod]
    public void MutedBrowserSourceClearsOnlyItsGraceWhileOtherBrowserAndSpotifyGraceRemains()
    {
        var policy = new MediaHeadphonePolicy();
        Assert.IsTrue(policy.Update(Available(
            new MediaAudioSample("device:spotify", "spotify", 0.02f, false, 1, false, 1),
            new MediaAudioSample("device:chrome", "chrome", 0.02f, false, 1, false, 1)),
            TimeSpan.Zero, enabled: true));

        Assert.IsTrue(policy.Update(Available(
            new MediaAudioSample("device:spotify", "spotify", 0, false, 1, false, 1),
            new MediaAudioSample("device:chrome", "chrome", 0.02f, true, 1, false, 1)),
            TimeSpan.FromMilliseconds(200), enabled: true));

        Assert.IsTrue(policy.Update(Available(), TimeSpan.FromMilliseconds(2_999), enabled: true));
        Assert.IsFalse(policy.Update(Available(), TimeSpan.FromSeconds(3), enabled: true));
    }

    [TestMethod]
    public void MutingZeroVolumeDisablingAndUnavailableObservationClearHeadphonesImmediately()
    {
        var policy = new MediaHeadphonePolicy();
        Assert.IsTrue(policy.Update(Sound("spotify", "session"), TimeSpan.Zero, enabled: true));
        Assert.IsFalse(policy.Update(Available(new MediaAudioSample("device:session", "spotify", 0.2f, true, 1, false, 1)),
            TimeSpan.FromMilliseconds(100), enabled: true));

        Assert.IsTrue(policy.Update(Sound("spotify", "session"), TimeSpan.FromSeconds(1), enabled: true));
        Assert.IsFalse(policy.Update(Available(new MediaAudioSample("device:session", "spotify", 0.2f, false, 0, false, 1)),
            TimeSpan.FromSeconds(1.1), enabled: true));

        Assert.IsTrue(policy.Update(Sound("spotify", "session"), TimeSpan.FromSeconds(2), enabled: true));
        Assert.IsFalse(policy.Update(MediaAudioObservation.Unavailable, TimeSpan.FromSeconds(2.1), enabled: true));
        Assert.IsTrue(policy.Update(Sound("spotify", "session"), TimeSpan.FromSeconds(3), enabled: true));
        Assert.IsFalse(policy.Update(Sound("spotify", "session"), TimeSpan.FromSeconds(3.1), enabled: false));
    }

    [TestMethod]
    public void OutputMuteAndVolumeZeroSuppressOnlyThatEndpoint()
    {
        var policy = new MediaHeadphonePolicy();
        Assert.IsFalse(policy.Update(Available(new MediaAudioSample("device:muted", "spotify", 0.5f, false, 1, true, 1)),
            TimeSpan.Zero, enabled: true));
        Assert.IsFalse(policy.Update(Available(new MediaAudioSample("device:zero", "spotify", 0.5f, false, 1, false, 0)),
            TimeSpan.Zero, enabled: true));
        Assert.IsTrue(policy.Update(Available(
            new MediaAudioSample("device:muted", "spotify", 0.5f, false, 1, true, 1),
            new MediaAudioSample("device:active", "spotify", 0.02f, false, 1, false, 0.5f)),
            TimeSpan.Zero, enabled: true));
    }

    [TestMethod]
    [DataRow(true, 1f)]
    [DataRow(false, 0f)]
    public void EndpointMuteOrZeroVolumeWithoutASessionSampleInvalidatesOnlyThatEndpointsGrace(bool muted, float volume)
    {
        var policy = new MediaHeadphonePolicy();
        Assert.IsTrue(policy.Update(Available(
            new MediaAudioSample("endpoint-a:session", "spotify", 0.02f, false, 1, false, 1, "endpoint-a"),
            new MediaAudioSample("endpoint-b:session", "chrome", 0.02f, false, 1, false, 1, "endpoint-b")),
            TimeSpan.Zero, enabled: true));

        var endpointState = new MediaAudioObservation(true, [],
            [new MediaAudioEndpoint("endpoint-a", muted, volume), new MediaAudioEndpoint("endpoint-b", false, 1)]);
        Assert.IsTrue(policy.Update(endpointState, TimeSpan.FromMilliseconds(100), enabled: true));
        Assert.IsTrue(policy.Update(Available(), TimeSpan.FromMilliseconds(2_999), enabled: true),
            "The unmuted endpoint retains its own grace after the other endpoint is muted.");
        Assert.IsFalse(policy.Update(Available(), TimeSpan.FromSeconds(3), enabled: true));
    }

    [TestMethod]
    public void ApplicationOutputHandoffKeepsHeadphonesOnUntilTheNewEndpointGraceExpires()
    {
        var policy = new MediaHeadphonePolicy();
        Assert.IsTrue(policy.Update(Available(
            new MediaAudioSample("endpoint-a:session", "spotify", 0.02f, false, 1, false, 1, "endpoint-a")),
            TimeSpan.Zero, enabled: true));
        Assert.IsTrue(policy.Update(Available(
            new MediaAudioSample("endpoint-b:session", "spotify", 0.02f, false, 1, false, 1, "endpoint-b")),
            TimeSpan.FromMilliseconds(250), enabled: true));
        Assert.IsTrue(policy.Update(Available(), TimeSpan.FromMilliseconds(3_000), enabled: true));
        Assert.IsFalse(policy.Update(Available(), TimeSpan.FromMilliseconds(3_250), enabled: true));
    }

    private static MediaAudioObservation Sound(string process, string session) =>
        Available(new MediaAudioSample($"device:{session}", process, 0.02f, false, 1, false, 1));

    private static MediaAudioObservation Available(params MediaAudioSample[] samples) => new(true, samples);
}
