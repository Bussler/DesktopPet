namespace DesktopPet.Core;

public interface IPetRandomSource
{
    int NextIndex(int exclusiveUpperBound);
}

public sealed class SystemRandomSource : IPetRandomSource
{
    public int NextIndex(int exclusiveUpperBound) => Random.Shared.Next(exclusiveUpperBound);
}

public interface ISessionStore
{
    HoodSessionSnapshot? Load();
    void Save(HoodSessionSnapshot snapshot);
    string? ResetAfterAcknowledgement();
}

public sealed record NeedLevels(double Hunger, double Boredom, double Sleep)
{
    public static NeedLevels Initial => new(0, 0, 0);
}

public enum HoodSessionMode
{
    Awake,
    Sleeping
}

public sealed record HoodSessionSnapshot
{
    public int SchemaVersion { get; init; } = 2;
    public PetPosition Position { get; init; }
    public PetAction Action { get; init; } = PetAction.Idle;
    public int FrameIndex { get; init; }
    public NeedLevels Needs { get; init; } = NeedLevels.Initial;
    public HoodSessionMode Mode { get; init; } = HoodSessionMode.Awake;
    public TimeSpan RemainingNap { get; init; }
    public double NapRecoveryPointsPerSecond { get; init; }
    public TimeSpan RemainingRandomInterval { get; init; }
    public TimeSpan RemainingEarlyWakeGrace { get; init; }
    public PetBounds? RoamingArea { get; init; }
}

public sealed class SessionStateException : Exception
{
    public SessionStateException(string statePath, string message, Exception? innerException = null)
        : base($"{message} Saved state: {statePath}", innerException)
    {
        StatePath = statePath;
    }

    public string StatePath { get; }
}

public static class HoodSessionSnapshotValidator
{
    public static void Validate(HoodSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SchemaVersion != 2)
        {
            throw new InvalidDataException("The session state schema version is not supported.");
        }

        if (!double.IsFinite(snapshot.Position.X) || !double.IsFinite(snapshot.Position.Y))
        {
            throw new InvalidDataException("The saved pet position must contain finite coordinates.");
        }

        if (!Enum.IsDefined(snapshot.Action) || !Enum.IsDefined(snapshot.Mode) || snapshot.FrameIndex < 0)
        {
            throw new InvalidDataException("The saved pet action, mode, or animation frame is invalid.");
        }

        if (snapshot.Needs is null ||
            !IsNeed(snapshot.Needs.Hunger) || !IsNeed(snapshot.Needs.Boredom) || !IsNeed(snapshot.Needs.Sleep))
        {
            throw new InvalidDataException("Saved needs must be finite values from 0 through 100.");
        }

        if (snapshot.RemainingNap < TimeSpan.Zero ||
            snapshot.RemainingRandomInterval < TimeSpan.Zero ||
            snapshot.RemainingEarlyWakeGrace < TimeSpan.Zero ||
            !double.IsFinite(snapshot.NapRecoveryPointsPerSecond) || snapshot.NapRecoveryPointsPerSecond < 0)
        {
            throw new InvalidDataException("Saved timer and nap recovery values cannot be negative or non-finite.");
        }

        if (snapshot.Mode == HoodSessionMode.Sleeping &&
            (snapshot.RemainingNap <= TimeSpan.Zero || snapshot.Action != PetAction.Sleep))
        {
            throw new InvalidDataException("A sleeping session must include a sleep action and positive remaining nap time.");
        }

        if (snapshot.Mode == HoodSessionMode.Sleeping &&
            (snapshot.RemainingEarlyWakeGrace > TimeSpan.Zero ||
             (snapshot.Needs.Sleep > 0 && snapshot.NapRecoveryPointsPerSecond <= 0)))
        {
            throw new InvalidDataException("A sleeping session cannot have wake grace and must preserve its nap recovery rate.");
        }

        if (snapshot.Mode == HoodSessionMode.Awake && snapshot.Action == PetAction.Sleep)
        {
            throw new InvalidDataException("An awake session cannot be saved in the sleep action.");
        }

        if (snapshot.Mode == HoodSessionMode.Awake &&
            (snapshot.RemainingNap > TimeSpan.Zero || snapshot.NapRecoveryPointsPerSecond > 0))
        {
            throw new InvalidDataException("An awake session cannot retain an active nap timer or recovery rate.");
        }

        if (snapshot.RoamingArea is { } area &&
            (!double.IsFinite(area.X) || !double.IsFinite(area.Y) ||
             !double.IsFinite(area.Width) || !double.IsFinite(area.Height) ||
             area.Width <= 0 || area.Height <= 0))
        {
            throw new InvalidDataException("The saved roaming area must have finite coordinates and positive dimensions.");
        }
    }

    private static bool IsNeed(double value) => double.IsFinite(value) && value is >= 0 and <= 100;
}

public sealed record HoodPresentation(
    PetAction Action,
    int FrameIndex,
    PetPosition Position,
    bool QuitRequested);

public sealed class HoodSession
{
    private const double RoamingSpeedUnitsPerSecond = 18;
    private const int RandomSampleResolution = 1_000_000;
    private static readonly TimeSpan RoamingBoutDuration = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan RoamingRestDuration = TimeSpan.FromSeconds(12);
    private readonly PetDefinition _definition;
    private readonly IPetRandomSource _random;
    private readonly ISessionStore _store;
    private IReadOnlyCollection<DisplayInfo> _displays;
    private HoodConfiguration _configuration;
    private PetAction _action = PetAction.Idle;
    private int _frameIndex;
    private TimeSpan _frameElapsed;
    private TimeSpan _remainingRoamingBout = RoamingBoutDuration;
    private TimeSpan _remainingRoamingRest;
    private bool _quitRequested;
    private bool _editingArea;
    private bool _dragging;

    private HoodSession(
        PetDefinition definition,
        IPetRandomSource random,
        ISessionStore store,
        IReadOnlyCollection<DisplayInfo> displays,
        PetPosition position,
        PetBounds roamingArea,
        HoodConfiguration configuration,
        HoodSessionSnapshot? restoredSession)
    {
        _definition = definition;
        _random = random;
        _store = store;
        _displays = displays;
        _configuration = configuration;
        Position = position;
        RoamingArea = roamingArea;
        if (restoredSession is null)
        {
            _frameIndex = NextIdleFrame();
            Needs = NeedLevels.Initial;
            Mode = HoodSessionMode.Awake;
        }
        else
        {
            HoodSessionSnapshotValidator.Validate(restoredSession);
            _action = restoredSession.Action;
            _frameIndex = restoredSession.FrameIndex;
            if (_frameIndex >= _definition.GetAnimation(_action).Frames.Count)
            {
                throw new InvalidDataException($"The saved {_action} frame does not exist in the packaged Hood animation.");
            }

            Needs = restoredSession.Needs;
            Mode = restoredSession.Mode;
            RemainingNap = restoredSession.RemainingNap;
            NapRecoveryPointsPerSecond = restoredSession.NapRecoveryPointsPerSecond;
            RemainingRandomInterval = restoredSession.RemainingRandomInterval;
            RemainingEarlyWakeGrace = restoredSession.RemainingEarlyWakeGrace;
            if (_action is PetAction.RunLeft or PetAction.RunRight)
            {
                _remainingRoamingBout = RoamingBoutDuration;
            }
        }

        if (!_configuration.Behavior.Roaming && _action is (PetAction.RunLeft or PetAction.RunRight))
        {
            _action = PetAction.Idle;
            _frameIndex = NextIdleFrame();
            _frameElapsed = TimeSpan.Zero;
        }
    }

    public PetPosition Position { get; private set; }
    public PetBounds RoamingArea { get; private set; }
    public HoodConfiguration Configuration => _configuration;
    public PetBounds MinimumRoamingArea => _definition.MinimumRoamingArea;
    public NeedLevels Needs { get; private set; }
    public HoodSessionMode Mode { get; private set; }
    public TimeSpan RemainingNap { get; private set; }
    public double NapRecoveryPointsPerSecond { get; private set; }
    public TimeSpan RemainingRandomInterval { get; private set; }
    public TimeSpan RemainingEarlyWakeGrace { get; private set; }
    public bool IsEditingArea => _editingArea;
    public bool IsDragging => _dragging;
    public bool RequiresAnimationFrames =>
        !((_action is PetAction.RunLeft or PetAction.RunRight) && IsRoamingPaused) &&
        (_action != PetAction.Idle || _configuration.Behavior.IdleAnimations);
    public bool QuitRequested => _quitRequested;

    public HoodPresentation Presentation => new(_action, _frameIndex, Position, _quitRequested);

    public static HoodSession Start(
        PetDefinition definition,
        IEnumerable<DisplayInfo> displays,
        IPetRandomSource random,
        ISessionStore store,
        HoodConfiguration? configuration = null,
        HoodSessionSnapshot? restoredSession = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(displays);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(store);
        definition.Validate(definition.SheetWidth, definition.SheetHeight);
        configuration ??= HoodConfiguration.Defaults;
        HoodConfigurationValidator.Validate(configuration);

        var displayList = displays.ToArray();
        var primary = displayList.SingleOrDefault(display => display.IsPrimary)
            ?? throw new InvalidOperationException("Hood needs exactly one primary display with a usable work area.");
        if (displayList.Count(display => display.IsPrimary) != 1)
        {
            throw new InvalidOperationException("Hood needs exactly one primary display with a usable work area.");
        }

        var placement = restoredSession is null
            ? PetPlacementPolicy.Initial(definition, primary, configuration.InitialArea)
            : PetPlacementPolicy.Restore(
                definition,
                restoredSession.Position,
                restoredSession.RoamingArea,
                displayList,
                configuration.InitialArea);
        return new HoodSession(
            definition,
            random,
            store,
            displayList,
            placement.Position,
            placement.RoamingArea,
            configuration,
            restoredSession);
    }

    public void MoveTo(PetPosition requestedPosition, bool persist = true)
    {
        if (_quitRequested)
        {
            return;
        }

        if (!double.IsFinite(requestedPosition.X) || !double.IsFinite(requestedPosition.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedPosition), "Hood placement must use finite logical coordinates.");
        }

        var placement = PetPlacementPolicy.Move(_definition, Position, RoamingArea, requestedPosition, _displays);
        Position = placement.Position;
        RoamingArea = placement.RoamingArea;
        if (persist)
        {
            SaveSnapshot();
        }
    }

    public void MoveRoamingAreaBy(double offsetX, double offsetY, bool persist = true)
    {
        if (!double.IsFinite(offsetX) || !double.IsFinite(offsetY))
        {
            throw new ArgumentOutOfRangeException(nameof(offsetX), "Roaming-area movement must use finite coordinates.");
        }

        if (_quitRequested)
        {
            return;
        }

        var placement = PetPlacementPolicy.MoveArea(
            _definition,
            Position,
            RoamingArea,
            RoamingArea.X + offsetX,
            RoamingArea.Y + offsetY,
            _displays);
        Position = placement.Position;
        RoamingArea = placement.RoamingArea;
        if (persist)
        {
            SaveSnapshot();
        }
    }

    public void ResizeRoamingArea(double width, double height, bool persist = true)
    {
        if (_quitRequested)
        {
            return;
        }

        var placement = PetPlacementPolicy.Resize(_definition, Position, RoamingArea, width, height, _displays);
        Position = placement.Position;
        RoamingArea = placement.RoamingArea;
        if (persist)
        {
            SaveSnapshot();
        }
    }

    public void BeginAreaEditing() => _editingArea = true;

    public void EndAreaEditing() => _editingArea = false;

    public void UpdateDisplays(IEnumerable<DisplayInfo> displays)
    {
        ArgumentNullException.ThrowIfNull(displays);
        var displayList = displays.ToArray();
        var placement = PetPlacementPolicy.Restore(
            _definition,
            Position,
            RoamingArea,
            displayList,
            _configuration.InitialArea);
        _displays = displayList;
        if (Position == placement.Position && RoamingArea == placement.RoamingArea)
        {
            return;
        }

        Position = placement.Position;
        RoamingArea = placement.RoamingArea;
        SaveSnapshot();
    }

    public void SaveCurrentState()
    {
        if (!_quitRequested)
        {
            SaveSnapshot();
        }
    }

    internal void ApplyConfiguration(HoodConfiguration configuration)
    {
        HoodConfigurationValidator.Validate(configuration);
        _configuration = configuration;
        if (!configuration.Behavior.Roaming && _action is (PetAction.RunLeft or PetAction.RunRight))
        {
            _action = PetAction.Idle;
            _frameIndex = NextIdleFrame();
            _frameElapsed = TimeSpan.Zero;
        }
    }

    public void BeginDragging() => _dragging = true;

    public void EndDragging() => _dragging = false;

    public void React()
    {
        if (_quitRequested)
        {
            return;
        }

        if (Mode == HoodSessionMode.Sleeping)
        {
            LeaveNap(PetAction.Wake);
            SaveSnapshot();
            return;
        }

        StartDirectAction(PetAction.Jump);
        if (!_configuration.Behavior.Roaming)
        {
            Needs = Needs with
            {
                Boredom = Math.Max(0, Needs.Boredom - _configuration.Needs.StationaryClickRelief)
            };
            SaveSnapshot();
        }
    }

    public void Feed()
    {
        if (_quitRequested)
        {
            return;
        }

        Needs = Needs with
        {
            Hunger = Math.Max(0, Needs.Hunger - _configuration.Needs.FeedGain)
        };
        if (Mode == HoodSessionMode.Sleeping)
        {
            LeaveNap(PetAction.Feed);
            SaveSnapshot();
            return;
        }

        StartDirectAction(PetAction.Feed);
        SaveSnapshot();
    }

    public void Sleep()
    {
        if (_quitRequested || Mode == HoodSessionMode.Sleeping)
        {
            return;
        }

        BeginNap();
        SaveSnapshot();
    }

    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Elapsed active-session time cannot be negative.");
        }

        if (_quitRequested)
        {
            return;
        }

        while (true)
        {
            if (Mode == HoodSessionMode.Awake &&
                !IsForegroundAction(_action) &&
                RemainingEarlyWakeGrace == TimeSpan.Zero &&
                Needs.Sleep >= _configuration.Sleep.AutomaticNapThreshold)
            {
                BeginNap();
                SaveSnapshot();
                continue;
            }

            if (elapsed == TimeSpan.Zero)
            {
                return;
            }

            if (CanAdvanceRandomInterval && RemainingRandomInterval == TimeSpan.Zero)
            {
                RemainingRandomInterval = SampleRandomInterval();
            }

            if (_action == PetAction.Idle && CanRoam && _remainingRoamingRest == TimeSpan.Zero && TryStartRoaming())
            {
                continue;
            }

            NormalizeRoamingDirection();

            var step = elapsed;
            var advanceRoamingBout = IsMoving;
            var advanceRoamingRest = _action == PetAction.Idle && CanRoam && _remainingRoamingRest > TimeSpan.Zero;
            var advanceRandomInterval = CanAdvanceRandomInterval;
            if (advanceRandomInterval)
            {
                step = Min(step, RemainingRandomInterval);
            }

            if (Mode == HoodSessionMode.Sleeping)
            {
                step = Min(step, RemainingNap);
            }
            else
            {
                if (RemainingEarlyWakeGrace > TimeSpan.Zero)
                {
                    step = Min(step, RemainingEarlyWakeGrace);
                }
                else if (!IsForegroundAction(_action) && Needs.Sleep < _configuration.Sleep.AutomaticNapThreshold)
                {
                    var timeToThreshold = TimeUntilSleepThreshold();
                    if (timeToThreshold > TimeSpan.Zero)
                    {
                        step = Min(step, timeToThreshold);
                    }
                }
            }

            var animation = _definition.GetAnimation(_action);
            if (!animation.Loops)
            {
                var frameDuration = TimeSpan.FromMilliseconds(animation.FrameDurationMilliseconds);
                var ticksToComplete = frameDuration.Ticks * (animation.Frames.Count - _frameIndex) - _frameElapsed.Ticks;
                step = Min(step, TimeSpan.FromTicks(ticksToComplete));
            }

            if (IsMoving)
            {
                step = Min(step, TimeToRoamingEdge());
                step = Min(step, _remainingRoamingBout);
            }
            else if (advanceRoamingRest)
            {
                step = Min(step, _remainingRoamingRest);
            }

            AdvanceNeeds(step);
            AdvanceNapTimers(step);
            AdvanceAnimation(step);
            AdvanceMovement(step);
            if (advanceRandomInterval)
            {
                RemainingRandomInterval = step >= RemainingRandomInterval
                    ? TimeSpan.Zero
                    : RemainingRandomInterval - step;
                if (RemainingRandomInterval == TimeSpan.Zero)
                {
                    StartScheduledReaction();
                    RemainingRandomInterval = SampleRandomInterval();
                    SaveSnapshot();
                }
            }

            if (advanceRoamingRest)
            {
                _remainingRoamingRest = step >= _remainingRoamingRest
                    ? TimeSpan.Zero
                    : _remainingRoamingRest - step;
            }

            if (advanceRoamingBout)
            {
                _remainingRoamingBout = step >= _remainingRoamingBout
                    ? TimeSpan.Zero
                    : _remainingRoamingBout - step;
                if (_remainingRoamingBout == TimeSpan.Zero)
                {
                    if (_action is PetAction.RunLeft or PetAction.RunRight)
                    {
                        _action = PetAction.Idle;
                        _frameIndex = NextIdleFrame();
                        _frameElapsed = TimeSpan.Zero;
                    }

                    _remainingRoamingRest = RoamingRestDuration;
                }
            }

            elapsed -= step;

            if (Mode == HoodSessionMode.Sleeping && RemainingNap == TimeSpan.Zero)
            {
                Needs = Needs with { Sleep = 0 };
                NapRecoveryPointsPerSecond = 0;
                Mode = HoodSessionMode.Awake;
                RemainingEarlyWakeGrace = TimeSpan.Zero;
                StartDirectAction(PetAction.Wake);
                SaveSnapshot();
            }
        }
    }

    public void Quit()
    {
        if (_quitRequested)
        {
            return;
        }

        _quitRequested = true;
        SaveSnapshot();
    }

    private int NextIdleFrame()
    {
        var frame = _random.NextIndex(_definition.GetAnimation(PetAction.Idle).Frames.Count);
        if (frame < 0 || frame >= _definition.GetAnimation(PetAction.Idle).Frames.Count)
        {
            throw new InvalidOperationException("The random source returned an invalid Hood idle frame index.");
        }

        return frame;
    }

    private void StartDirectAction(PetAction action)
    {
        _action = action;
        _frameIndex = 0;
        _frameElapsed = TimeSpan.Zero;
    }

    private static double AddNeed(double current, double increase) => Math.Min(100, current + increase);

    private void AdvanceNeeds(TimeSpan elapsed)
    {
        if (Mode == HoodSessionMode.Sleeping)
        {
            var recovery = NapRecoveryPointsPerSecond * elapsed.TotalSeconds;
            Needs = Needs with { Sleep = Math.Max(0, Needs.Sleep - recovery) };
            return;
        }

        var hours = elapsed.TotalHours;
        Needs = new NeedLevels(
            AddNeed(Needs.Hunger, _configuration.Needs.HungerPointsPerHour * hours),
            AddNeed(Needs.Boredom, _configuration.Needs.BoredomPointsPerHour * hours),
            AddNeed(Needs.Sleep, _configuration.Needs.SleepPointsPerHour * hours));
    }

    private void AdvanceNapTimers(TimeSpan elapsed)
    {
        if (Mode == HoodSessionMode.Sleeping)
        {
            RemainingNap = elapsed >= RemainingNap ? TimeSpan.Zero : RemainingNap - elapsed;
        }
        else if (RemainingEarlyWakeGrace > TimeSpan.Zero)
        {
            RemainingEarlyWakeGrace = elapsed >= RemainingEarlyWakeGrace
                ? TimeSpan.Zero
                : RemainingEarlyWakeGrace - elapsed;
        }
    }

    private void AdvanceAnimation(TimeSpan elapsed)
    {
        if ((_action is PetAction.RunLeft or PetAction.RunRight) && IsRoamingPaused)
        {
            return;
        }

        if (_action == PetAction.Idle && !_configuration.Behavior.IdleAnimations)
        {
            return;
        }

        var animation = _definition.GetAnimation(_action);
        var frameDuration = TimeSpan.FromMilliseconds(animation.FrameDurationMilliseconds);
        if (animation.Loops)
        {
            var advancedFrames = elapsed.Ticks / frameDuration.Ticks;
            var partialTicks = _frameElapsed.Ticks + elapsed.Ticks % frameDuration.Ticks;
            advancedFrames += partialTicks / frameDuration.Ticks;
            _frameIndex = (int)((_frameIndex + advancedFrames % animation.Frames.Count) % animation.Frames.Count);
            _frameElapsed = TimeSpan.FromTicks(partialTicks % frameDuration.Ticks);
            return;
        }

        var totalTicks = frameDuration.Ticks * animation.Frames.Count;
        var currentTicks = frameDuration.Ticks * _frameIndex + _frameElapsed.Ticks;
        var ticksToComplete = totalTicks - currentTicks;
        if (elapsed.Ticks >= ticksToComplete)
        {
            _action = PetAction.Idle;
            _frameIndex = NextIdleFrame();
            _frameElapsed = TimeSpan.Zero;
            return;
        }

        var elapsedTicks = _frameElapsed.Ticks + elapsed.Ticks;
        _frameIndex += (int)(elapsedTicks / frameDuration.Ticks);
        _frameElapsed = TimeSpan.FromTicks(elapsedTicks % frameDuration.Ticks);
    }

    private void BeginNap()
    {
        var duration = DurationFromMinutes(_configuration.Sleep.DurationMinutes);
        RemainingNap = duration;
        NapRecoveryPointsPerSecond = Needs.Sleep / duration.TotalSeconds;
        RemainingEarlyWakeGrace = TimeSpan.Zero;
        Mode = HoodSessionMode.Sleeping;
        StartDirectAction(PetAction.Sleep);
    }

    private void LeaveNap(PetAction nextAction)
    {
        var wokeEarly = RemainingNap > TimeSpan.Zero;
        RemainingNap = TimeSpan.Zero;
        NapRecoveryPointsPerSecond = 0;
        RemainingEarlyWakeGrace = wokeEarly
            ? DurationFromMinutes(_configuration.Sleep.EarlyWakeGraceMinutes)
            : TimeSpan.Zero;
        Mode = HoodSessionMode.Awake;
        StartDirectAction(nextAction);
    }

    private TimeSpan TimeUntilSleepThreshold()
    {
        var pointsPerHour = _configuration.Needs.SleepPointsPerHour;
        if (pointsPerHour <= 0)
        {
            return TimeSpan.MaxValue;
        }

        var ticks = (_configuration.Sleep.AutomaticNapThreshold - Needs.Sleep) / pointsPerHour * TimeSpan.TicksPerHour;
        if (!double.IsFinite(ticks) || ticks >= TimeSpan.MaxValue.Ticks)
        {
            return TimeSpan.MaxValue;
        }

        return TimeSpan.FromTicks(Math.Max(1, (long)Math.Ceiling(ticks)));
    }

    private static TimeSpan DurationFromMinutes(double minutes)
    {
        var ticks = minutes * TimeSpan.TicksPerMinute;
        if (!double.IsFinite(ticks) || ticks >= TimeSpan.MaxValue.Ticks)
        {
            return TimeSpan.MaxValue;
        }

        return TimeSpan.FromTicks(Math.Max(1, (long)Math.Round(ticks)));
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;

    private bool CanRoam =>
        Mode == HoodSessionMode.Awake &&
        _configuration.Behavior.Roaming &&
        !_dragging &&
        !_editingArea &&
        RoamingArea.Width > _definition.FrameWidth;

    private bool IsMoving => CanRoam && _action is PetAction.RunLeft or PetAction.RunRight;

    private bool CanAdvanceRandomInterval =>
        _configuration.Behavior.RandomEvents &&
        Mode == HoodSessionMode.Awake &&
        !_dragging &&
        !_editingArea &&
        _action is PetAction.Idle or PetAction.RunLeft or PetAction.RunRight;

    private bool IsRoamingPaused =>
        Mode != HoodSessionMode.Awake ||
        !_configuration.Behavior.Roaming ||
        _dragging ||
        _editingArea;

    private static bool IsForegroundAction(PetAction action) =>
        action is PetAction.Jump or PetAction.Feed or PetAction.Wake;

    private bool TryStartRoaming()
    {
        if (!CanRoam)
        {
            return false;
        }

        var leftEdge = RoamingArea.X;
        var rightEdge = RoamingArea.Right - _definition.FrameWidth;
        var direction = Position.X <= leftEdge
            ? PetAction.RunRight
            : Position.X >= rightEdge
                ? PetAction.RunLeft
                : _random.NextIndex(2) == 0 ? PetAction.RunLeft : PetAction.RunRight;
        StartDirectAction(direction);
        _remainingRoamingBout = RoamingBoutDuration;
        return true;
    }

    private void NormalizeRoamingDirection()
    {
        if (!CanRoam || _action is not (PetAction.RunLeft or PetAction.RunRight))
        {
            return;
        }

        var leftEdge = RoamingArea.X;
        var rightEdge = RoamingArea.Right - _definition.FrameWidth;
        if (_action == PetAction.RunLeft && Position.X <= leftEdge)
        {
            StartDirectAction(PetAction.RunRight);
        }
        else if (_action == PetAction.RunRight && Position.X >= rightEdge)
        {
            StartDirectAction(PetAction.RunLeft);
        }
    }

    private TimeSpan TimeToRoamingEdge()
    {
        var distance = _action == PetAction.RunLeft
            ? Position.X - RoamingArea.X
            : RoamingArea.Right - _definition.FrameWidth - Position.X;
        var ticks = Math.Max(0, distance) / RoamingSpeedUnitsPerSecond * TimeSpan.TicksPerSecond;
        if (!double.IsFinite(ticks) || ticks >= TimeSpan.MaxValue.Ticks)
        {
            return TimeSpan.MaxValue;
        }

        return TimeSpan.FromTicks(Math.Max(1, (long)Math.Ceiling(ticks)));
    }

    private void AdvanceMovement(TimeSpan elapsed)
    {
        if (!IsMoving)
        {
            return;
        }

        var direction = _action == PetAction.RunLeft ? -1 : 1;
        var leftEdge = RoamingArea.X;
        var rightEdge = RoamingArea.Right - _definition.FrameWidth;
        var nextX = Math.Clamp(
            Position.X + direction * RoamingSpeedUnitsPerSecond * elapsed.TotalSeconds,
            leftEdge,
            rightEdge);
        var distance = Math.Abs(nextX - Position.X);
        if (distance <= 0)
        {
            return;
        }

        Position = new PetPosition(nextX, Position.Y);
        Needs = Needs with
        {
            Boredom = Math.Max(0, Needs.Boredom -
                _configuration.Needs.MovementReliefPerSecond * distance / RoamingSpeedUnitsPerSecond)
        };

        if (nextX <= leftEdge && direction < 0)
        {
            StartDirectAction(PetAction.RunRight);
        }
        else if (nextX >= rightEdge && direction > 0)
        {
            StartDirectAction(PetAction.RunLeft);
        }
    }

    private TimeSpan SampleRandomInterval()
    {
        var minimum = DurationFromMinutes(_configuration.RandomInterval.MinimumMinutes).Ticks;
        var maximum = DurationFromMinutes(_configuration.RandomInterval.MaximumMinutes).Ticks;
        if (maximum <= minimum)
        {
            return TimeSpan.FromTicks(minimum);
        }

        var sample = NextRandomIndex(RandomSampleResolution + 1);
        var fraction = sample / (double)RandomSampleResolution;
        var ticks = sample == RandomSampleResolution
            ? maximum
            : (long)Math.Round(minimum + ((double)maximum - minimum) * fraction);
        return TimeSpan.FromTicks(Math.Clamp(ticks, minimum, maximum));
    }

    private void StartScheduledReaction()
    {
        PetAction[] candidates = [PetAction.Wave, PetAction.LookAround, PetAction.Jump];
        var fitting = candidates
            .Where(action =>
            {
                var footprint = _definition.GetAnimation(action).MotionBounds;
                return footprint.Width <= RoamingArea.Width && footprint.Height <= RoamingArea.Height;
            })
            .ToArray();
        if (fitting.Length == 0)
        {
            return;
        }

        StartDirectAction(fitting[NextRandomIndex(fitting.Length)]);
    }

    private int NextRandomIndex(int exclusiveUpperBound)
    {
        var index = _random.NextIndex(exclusiveUpperBound);
        if (index < 0 || index >= exclusiveUpperBound)
        {
            throw new InvalidOperationException("The random source returned a value outside the requested range.");
        }

        return index;
    }

    private void SaveSnapshot() => _store.Save(new HoodSessionSnapshot
    {
        Position = Position,
        Action = _action,
        FrameIndex = _frameIndex,
        Needs = Needs,
        Mode = Mode,
        RemainingNap = RemainingNap,
        NapRecoveryPointsPerSecond = NapRecoveryPointsPerSecond,
        RemainingRandomInterval = RemainingRandomInterval,
        RemainingEarlyWakeGrace = RemainingEarlyWakeGrace,
        RoamingArea = RoamingArea
    });
}
