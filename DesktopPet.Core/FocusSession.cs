namespace DesktopPet.Core;

public enum FocusPhase { Idle, Focus, FocusFinished, Break, BreakFinished }

/// <summary>A transient, manually advanced focus cycle. Never part of saved pet state.</summary>
public sealed class FocusSession
{
    public const int MaximumMinutes = 1440;
    public int FocusMinutes { get; private set; } = 25;
    public int BreakMinutes { get; private set; } = 5;
    public FocusPhase Phase { get; private set; }
    public TimeSpan Remaining { get; private set; }
    public bool IsPaused { get; private set; }
    public bool WavePending { get; private set; }
    public bool IsRunning => Phase is FocusPhase.Focus or FocusPhase.Break && !IsPaused;
    public bool CanStartFocus => Phase is FocusPhase.Idle or FocusPhase.BreakFinished;
    public PetAction? PetActionOverride => Phase is FocusPhase.Break or FocusPhase.BreakFinished
        ? PetAction.Sleep : WavePending ? PetAction.Wave : null;
    public event Action? Changed;
    public event Action? RestoreRequested;

    public void Configure(int focusMinutes, int breakMinutes)
    {
        if (focusMinutes is < 1 or > MaximumMinutes || breakMinutes is < 1 or > MaximumMinutes)
            throw new ArgumentOutOfRangeException(nameof(focusMinutes), $"Enter whole minutes from 1 to {MaximumMinutes}.");
        FocusMinutes = focusMinutes;
        BreakMinutes = breakMinutes;
        Changed?.Invoke();
    }

    public void StartFocus()
    {
        if (!CanStartFocus) return;
        Start(FocusPhase.Focus, FocusMinutes);
    }

    public void TakeBreak()
    {
        if (Phase != FocusPhase.FocusFinished) return;
        Start(FocusPhase.Break, BreakMinutes);
    }

    private void Start(FocusPhase phase, int minutes)
    {
        Phase = phase;
        Remaining = TimeSpan.FromMinutes(minutes);
        IsPaused = WavePending = false;
        Changed?.Invoke();
    }

    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (!IsRunning || elapsed == TimeSpan.Zero) return;
        var finished = elapsed >= Remaining;
        Remaining = finished ? TimeSpan.Zero : Remaining - elapsed;
        if (finished)
        {
            WavePending = Phase == FocusPhase.Focus;
            Phase = WavePending ? FocusPhase.FocusFinished : FocusPhase.BreakFinished;
        }
        Changed?.Invoke();
        if (finished && Phase == FocusPhase.BreakFinished) RestoreRequested?.Invoke();
    }

    public void TogglePause()
    {
        if (Phase is not (FocusPhase.Focus or FocusPhase.Break)) return;
        IsPaused = !IsPaused;
        Changed?.Invoke();
    }

    public void SkipBreak()
    {
        if (Phase != FocusPhase.Break) return;
        Phase = FocusPhase.BreakFinished;
        Remaining = TimeSpan.Zero;
        IsPaused = false;
        Changed?.Invoke();
    }

    public void Stop()
    {
        Phase = FocusPhase.Idle;
        Remaining = TimeSpan.Zero;
        IsPaused = WavePending = false;
        Changed?.Invoke();
    }

    public bool HandlePetClick()
    {
        if (PetActionOverride is null) return false;
        WavePending = false;
        Changed?.Invoke();
        RestoreRequested?.Invoke();
        return true;
    }
}
