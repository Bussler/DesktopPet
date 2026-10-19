using DesktopPet.Core;

namespace DesktopPet.Windows;

public partial class HoodOverlayWindow
{
    private static readonly TimeSpan WritingTimeout = TimeSpan.FromMilliseconds(800);
    private readonly TimeProvider _writingClock;
    private object? _writingOwner;
    private long _writingStartedAt;
    private long _lastWritingInputAt;

    private bool HasRecentWriting => _writingOwner is not null &&
        _writingClock.GetElapsedTime(_lastWritingInputAt) < WritingTimeout;

    internal void ReportNoteWriting(object owner, bool active)
    {
        if (active && CanObserveWriting)
        {
            if (!ReferenceEquals(_writingOwner, owner) || !HasRecentWriting)
                _writingStartedAt = _writingClock.GetTimestamp();
            _writingOwner = owner;
            _lastWritingInputAt = _writingClock.GetTimestamp();
        }
        else if (ReferenceEquals(_writingOwner, owner))
            _writingOwner = null;

        RefreshWritingPresentation();
    }

    private bool CanObserveWriting => _session.Configuration.Behavior.WritingAnimations &&
        _session.Configuration.Behavior.ShowSprites && !_session.QuitRequested &&
        !_mediaObservationStopped && Volatile.Read(ref _suspended) == 0;

    private void UpdateWritingPresentation()
    {
        if (!CanObserveWriting || !HasRecentWriting) _writingOwner = null;
        _session.SetWritingPresentation(_writingOwner is not null);
    }

    private void ClearWritingActivity()
    {
        _writingOwner = null;
        _session.SetWritingPresentation(false);
    }

    internal void RefreshWritingPresentation()
    {
        UpdateWritingPresentation();
        RenderPresentation();
        UpdateTimerInterval();
    }

    private HoodPresentation? WritingPresentation
    {
        get
        {
            if (!_session.IsWriting || !HasRecentWriting) return null;
            var animation = _assetSource.Definition.GetAnimation(PetAction.Write);
            var action = Enum.Parse<PetAction>(animation.Action, ignoreCase: true);
            var frameTicks = TimeSpan.FromMilliseconds(animation.FrameDurationMilliseconds).Ticks;
            var index = (int)(_writingClock.GetElapsedTime(_writingStartedAt).Ticks / frameTicks % animation.Frames.Count);
            if (action == PetAction.Idle && !_session.Configuration.Behavior.IdleAnimations) index = 0;
            return new(action, index, _session.Position);
        }
    }
}
