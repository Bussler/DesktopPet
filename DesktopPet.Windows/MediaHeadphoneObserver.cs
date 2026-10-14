using System.Diagnostics;
using System.Windows.Threading;

namespace DesktopPet.Windows;

internal sealed class MediaHeadphoneObserver : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private readonly Dispatcher _dispatcher;
    private readonly IMediaAudioProbe _probe;
    private readonly Action<MediaAudioObservation, TimeSpan> _onObservation;
    private readonly long _clockOrigin = Stopwatch.GetTimestamp();
    private CancellationTokenSource? _cancellation;
    private bool _unavailableReported;

    public MediaHeadphoneObserver(Dispatcher dispatcher, IMediaAudioProbe probe,
        Action<MediaAudioObservation, TimeSpan> onObservation)
    {
        _dispatcher = dispatcher;
        _probe = probe;
        _onObservation = onObservation;
    }

    public void Start()
    {
        if (_cancellation is not null) return;
        var cancellation = _cancellation = new CancellationTokenSource();
        _ = PollAsync(cancellation);
    }

    public void Stop()
    {
        var cancellation = _cancellation;
        _cancellation = null;
        cancellation?.Cancel();
    }

    private async Task PollAsync(CancellationTokenSource cancellation)
    {
        try
        {
            using var timer = new PeriodicTimer(PollInterval);
            while (await timer.WaitForNextTickAsync(cancellation.Token).ConfigureAwait(false))
            {
                MediaAudioObservation observation;
                try { observation = await Task.Run(_probe.Read, cancellation.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { break; }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    observation = MediaAudioObservation.Unavailable;
                }

                if (!observation.IsAvailable)
                {
                    if (!_unavailableReported)
                        MediaHeadphoneDiagnostics.Report("Media headphone audio observation is unavailable; headphones are off. Check or reconnect your Windows output device. Observation will retry automatically.");
                    _unavailableReported = true;
                }
                else if (_unavailableReported)
                {
                    MediaHeadphoneDiagnostics.Report("Media headphone audio observation recovered.");
                    _unavailableReported = false;
                }

                if (cancellation.IsCancellationRequested || _dispatcher.HasShutdownStarted) break;
                var elapsed = Stopwatch.GetElapsedTime(_clockOrigin, Stopwatch.GetTimestamp());
                var captured = observation;
                _ = _dispatcher.BeginInvoke(new Action(() => _onObservation(captured, elapsed)), DispatcherPriority.Background);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(_cancellation, cancellation)) _cancellation = null;
            cancellation.Dispose();
        }
    }

    public void Dispose() => Stop();
}
