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
    private const double BaselineRoamingCycleSeconds = 40;
    private readonly PetDefinition _baseDefinition;
    private PetDefinition _definition;
    private readonly IPetRandomSource _random;
    private readonly ISessionStore _store;
    private readonly int _idleSkippedStillFrames;
    private IReadOnlyCollection<DisplayInfo> _displays;
    private HoodConfiguration _configuration;
    private PetAction _action = PetAction.Idle;
    private int _frameIndex;
    private TimeSpan _frameElapsed;
    private TimeSpan _remainingRoamingBout = RoamingBoutDuration;
    private TimeSpan? _elapsedRoamingRest;
    private bool _quitRequested;
    private bool _editingArea;
    private bool _dragging;
    private PetAction? _dragAction;
    private TimeSpan _dragAnimationElapsed;
    private PetPosition _roamingDirection;

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
        _baseDefinition = definition;
        _definition = definition.WithDisplayScale(configuration.PetScale);
        PetScale = configuration.PetScale;
        var idle = definition.GetAnimation(PetAction.Idle);
        // Removing one sixth of the cycle makes idle repeat about 20% more often.
        // Skip only the still pose and retain manifest indices for saved sessions.
        _idleSkippedStillFrames = Math.Min((int)Math.Round(idle.Frames.Count / 6d),
            idle.Frames.TakeWhile(frame => frame == idle.Frames[0]).Count() - 1);
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
            RemainingRandomInterval = Min(restoredSession.RemainingRandomInterval,
                DurationFromMinutes(_configuration.RandomInterval.MaximumMinutes));
            RemainingEarlyWakeGrace = restoredSession.RemainingEarlyWakeGrace;
            if (_action is PetAction.RunLeft or PetAction.RunRight)
            {
                _remainingRoamingBout = RoamingBoutDuration;
                _roamingDirection = new PetPosition(_action == PetAction.RunLeft ? -1 : 1, 0);
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
    public double PetScale { get; private set; }
    public double PetWidth => _definition.DisplayWidth;
    public double PetHeight => _definition.DisplayHeight;
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
        _configuration.Behavior.ShowSprites &&
        (_dragAction is not null ||
         (!((_action is PetAction.RunLeft or PetAction.RunRight) && IsRoamingPaused) &&
          (_action != PetAction.Idle || _configuration.Behavior.IdleAnimations)));
    public bool QuitRequested => _quitRequested;

    public HoodPresentation Presentation
    {
        get
        {
            if (_dragAction is { } action)
            {
                var frameDuration = TimeSpan.FromMilliseconds(_definition.GetAnimation(action).FrameDurationMilliseconds);
                return new(action, (int)(_dragAnimationElapsed.Ticks / frameDuration.Ticks), Position, _quitRequested);
            }

            return new(_action, _frameIndex, PresentationPosition(), _quitRequested);
        }
    }

    private PetPosition PresentationPosition()
    {
        if (_action != PetAction.Jump)
        {
            return Position;
        }

        var animation = _definition.GetAnimation(PetAction.Jump);
        var progress = (_frameIndex + _frameElapsed.TotalMilliseconds / animation.FrameDurationMilliseconds)
            / animation.Frames.Count;
        // The first four poses crouch; the last three absorb the landing.
        var flight = Math.Clamp((progress - 4d / 15) / (8d / 15), 0, 1);
        var display = PetPlacementPolicy.Restore(
            _definition, Position, RoamingArea, _displays, _configuration.InitialArea).Display;
        var height = Math.Min(90 * PetScale, Math.Max(0, Position.Y - display.WorkArea.Y));
        return Position with { Y = Position.Y - height * 4 * flight * (1 - flight) };
    }

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

        var scaledDefinition = definition.WithDisplayScale(configuration.PetScale);
        var placement = restoredSession is null
            ? PetPlacementPolicy.Initial(scaledDefinition, primary, configuration.InitialArea)
            : PetPlacementPolicy.Restore(
                scaledDefinition,
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
        if (_dragging && placement.Position.X != Position.X)
        {
            var action = placement.Position.X < Position.X ? PetAction.RunLeft : PetAction.RunRight;
            if (_dragAction != action)
            {
                _dragAction = action;
                _dragAnimationElapsed = TimeSpan.Zero;
            }
        }

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
        NormalizeRoamingDirection();
        if (persist)
        {
            SaveSnapshot();
        }
    }

    public void ResizeRoamingAreaBy(double horizontalChange, double verticalChange,
        bool fromLeft = false, bool fromTop = false, bool persist = true)
    {
        if (_quitRequested)
        {
            return;
        }

        var placement = PetPlacementPolicy.ResizeFromEdge(
            _definition, Position, RoamingArea, horizontalChange, verticalChange, fromLeft, fromTop, _displays);
        Position = placement.Position;
        RoamingArea = placement.RoamingArea;
        NormalizeRoamingDirection();
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
        NormalizeRoamingDirection();
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
        if (configuration.PetScale != _configuration.PetScale) SetPetScale(configuration.PetScale);
        if (configuration.RandomInterval != _configuration.RandomInterval)
        {
            RemainingRandomInterval = TimeSpan.Zero;
        }

        _configuration = configuration;
        if (!configuration.Behavior.Roaming && _action is (PetAction.RunLeft or PetAction.RunRight))
        {
            _action = PetAction.Idle;
            _frameIndex = NextIdleFrame();
            _frameElapsed = TimeSpan.Zero;
        }
    }

    internal void ValidatePetScale(double scale) => PlacementForPetScale(scale, null, null);

    public void SetPetScale(double scale, PetPosition? position = null, PetBounds? roamingArea = null)
    {
        if (scale == PetScale && position is null && roamingArea is null) return;
        var placement = PlacementForPetScale(scale, position, roamingArea);
        _definition = _baseDefinition.WithDisplayScale(scale);
        PetScale = scale;
        Position = placement.Position;
        RoamingArea = placement.RoamingArea;
        NormalizeRoamingDirection();
    }

    private PetPlacement PlacementForPetScale(double scale, PetPosition? position, PetBounds? roamingArea)
    {
        HoodConfigurationValidator.ValidatePetScale(scale);
        var definition = _baseDefinition.WithDisplayScale(scale);
        // Keep the pet's feet and horizontal center fixed until display bounds require a move.
        position ??= new PetPosition(Position.X + (PetWidth - definition.DisplayWidth) / 2,
            Position.Y + PetHeight - definition.DisplayHeight);
        return PetPlacementPolicy.Restore(definition, position.Value, roamingArea ?? RoamingArea,
            _displays, _configuration.InitialArea);
    }

    public void BeginDragging() => _dragging = true;

    public void EndDragging()
    {
        _dragging = false;
        _dragAction = null;
        _dragAnimationElapsed = TimeSpan.Zero;
    }

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

        if (_quitRequested || !_configuration.Behavior.ShowSprites)
        {
            return;
        }

        if (_dragAction is { } dragAction)
        {
            var animation = _definition.GetAnimation(dragAction);
            var cycleTicks = TimeSpan.FromMilliseconds(animation.FrameDurationMilliseconds).Ticks * animation.Frames.Count;
            _dragAnimationElapsed = TimeSpan.FromTicks((_dragAnimationElapsed.Ticks + elapsed.Ticks % cycleTicks) % cycleTicks);
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

            if (_action == PetAction.Idle && CanRoam && TimeUntilRoaming() == TimeSpan.Zero && TryStartRoaming())
            {
                continue;
            }

            NormalizeRoamingDirection();

            var step = elapsed;
            var advanceRoamingBout = IsMoving;
            var advanceRoamingRest = _action == PetAction.Idle && CanRoam && _elapsedRoamingRest is not null;
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
                step = Min(step, TimeUntilRoaming());
            }

            AdvanceNapTimers(step);
            AdvanceAnimation(step);
            var movementSeconds = AdvanceMovement(step);
            AdvanceNeeds(step, movementSeconds);
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
                _elapsedRoamingRest += step;
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

                    _elapsedRoamingRest = TimeSpan.Zero;
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

    private void AdvanceNeeds(TimeSpan elapsed, double movementSeconds)
    {
        if (Mode == HoodSessionMode.Sleeping)
        {
            var recovery = NapRecoveryPointsPerSecond * elapsed.TotalSeconds;
            Needs = Needs with { Sleep = Math.Max(0, Needs.Sleep - recovery) };
            return;
        }

        var hours = elapsed.TotalHours;
        // Clamp the net change once so saturation does not depend on update size.
        var boredomChange = _configuration.Needs.BoredomPointsPerHour * hours -
            _configuration.Needs.MovementReliefPerSecond * movementSeconds;
        Needs = new NeedLevels(
            AddNeed(Needs.Hunger, _configuration.Needs.HungerPointsPerHour * hours),
            Math.Clamp(Needs.Boredom + boredomChange, 0, 100),
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
            var skippedFrames = _action == PetAction.Idle ? _idleSkippedStillFrames : 0;
            var frameCount = animation.Frames.Count - skippedFrames;
            var advancedFrames = elapsed.Ticks / frameDuration.Ticks;
            var partialTicks = _frameElapsed.Ticks + elapsed.Ticks % frameDuration.Ticks;
            advancedFrames += partialTicks / frameDuration.Ticks;
            _frameIndex = skippedFrames + (int)((Math.Max(0, _frameIndex - skippedFrames) + advancedFrames % frameCount) % frameCount);
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
        (CanRoamHorizontally || CanRoamVertically);

    // Avoid pacing, including microscopic rounding gaps, with less than one second of travel space.
    private bool CanRoamHorizontally => RoamingArea.Width - _definition.DisplayWidth >= RoamingSpeedUnitsPerSecond;
    private bool CanRoamVertically => RoamingArea.Height - _definition.DisplayHeight >= RoamingSpeedUnitsPerSecond;
    private double RoamingEndX => RoamingArea.X + (RoamingArea.Width - _definition.DisplayWidth);
    private double RoamingEndY => RoamingArea.Y + (RoamingArea.Height - _definition.DisplayHeight);

    private double RoamingAxisSpeed => RoamingSpeedUnitsPerSecond /
        Math.Sqrt(Math.Max(1, Math.Abs(_roamingDirection.X) + Math.Abs(_roamingDirection.Y)));
    private PetAction RoamingAction => (_roamingDirection.X != 0 ? _roamingDirection.X : _roamingDirection.Y) < 0
        ? PetAction.RunLeft : PetAction.RunRight;
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

    private TimeSpan TimeUntilRoaming()
    {
        if (_elapsedRoamingRest is not { } rest)
        {
            return TimeSpan.Zero;
        }

        var cycleElapsed = RoamingBoutDuration.TotalSeconds + rest.TotalSeconds;
        var frequency = 0.1 + 1.1 * Needs.Boredom / 100;
        var remaining = BaselineRoamingCycleSeconds - cycleElapsed * frequency;
        if (remaining <= 0)
        {
            return TimeSpan.Zero;
        }

        // Solve (cycleElapsed + t) * (frequency + growth * t) = 40 so rising
        // boredom shortens the current rest equally for large and small time steps.
        var growth = _configuration.Needs.BoredomPointsPerHour / 3600 * 0.011;
        var linear = frequency + cycleElapsed * growth;
        var seconds = 2 * remaining / (linear + Math.Sqrt(linear * linear + 4 * growth * remaining));
        seconds = Math.Max(seconds, BaselineRoamingCycleSeconds / 1.2 - cycleElapsed);
        return TimeSpan.FromTicks(Math.Max(1, (long)Math.Ceiling(seconds * TimeSpan.TicksPerSecond)));
    }

    private bool TryStartRoaming()
    {
        if (!CanRoam)
        {
            return false;
        }

        // Choose horizontal, diagonal, or vertical travel when both axes have room.
        var travel = CanRoamVertically
            ? CanRoamHorizontally ? NextRandomIndex(3) : 2
            : 0;
        _roamingDirection = new PetPosition(
            travel != 2 ? ChooseDirection(Position.X, RoamingArea.X, RoamingEndX) : 0,
            travel != 0 ? ChooseDirection(Position.Y, RoamingArea.Y, RoamingEndY) : 0);
        StartDirectAction(RoamingAction);
        _remainingRoamingBout = RoamingBoutDuration;
        _elapsedRoamingRest = null;
        return true;
    }

    private double ChooseDirection(double coordinate, double start, double end) =>
        coordinate <= start ? 1 : coordinate >= end ? -1 : NextRandomIndex(2) == 0 ? -1 : 1;

    private static double ReflectDirection(double coordinate, double start, double end, double direction) =>
        direction < 0 && coordinate <= start ? 1 : direction > 0 && coordinate >= end ? -1 : direction;

    private void NormalizeRoamingDirection()
    {
        if (_action is not (PetAction.RunLeft or PetAction.RunRight))
        {
            return;
        }

        if (!CanRoamHorizontally && !CanRoamVertically)
        {
            _action = PetAction.Idle;
            _frameIndex = NextIdleFrame();
            _frameElapsed = TimeSpan.Zero;
            _remainingRoamingBout = TimeSpan.Zero;
            _elapsedRoamingRest = null;
            return;
        }
        if (!CanRoam) return;

        var dx = CanRoamHorizontally ? _roamingDirection.X : 0;
        var dy = CanRoamVertically ? _roamingDirection.Y : 0;
        if (dx == 0 && dy == 0)
        {
            var direction = _action == PetAction.RunLeft ? -1 : 1;
            if (CanRoamHorizontally)
            {
                dx = direction;
            }
            else
            {
                dy = direction;
            }
        }

        _roamingDirection = new PetPosition(
            ReflectDirection(Position.X, RoamingArea.X, RoamingEndX, dx),
            ReflectDirection(Position.Y, RoamingArea.Y, RoamingEndY, dy));
        if (_action != RoamingAction)
        {
            StartDirectAction(RoamingAction);
        }
    }

    private TimeSpan TimeToRoamingEdge() => Min(
        TimeToEdge(Position.X, RoamingArea.X, RoamingEndX, _roamingDirection.X),
        TimeToEdge(Position.Y, RoamingArea.Y, RoamingEndY, _roamingDirection.Y));

    private TimeSpan TimeToEdge(double coordinate, double start, double end, double direction)
    {
        if (direction == 0)
        {
            return TimeSpan.MaxValue;
        }

        var distance = direction < 0 ? coordinate - start : end - coordinate;
        var ticks = Math.Max(0, distance) / RoamingAxisSpeed * TimeSpan.TicksPerSecond;
        if (!double.IsFinite(ticks) || ticks >= TimeSpan.MaxValue.Ticks)
        {
            return TimeSpan.MaxValue;
        }

        return TimeSpan.FromTicks(Math.Max(1, (long)Math.Ceiling(ticks)));
    }

    private double AdvanceMovement(TimeSpan elapsed)
    {
        if (!IsMoving)
        {
            return 0;
        }

        var travel = RoamingAxisSpeed * elapsed.TotalSeconds;
        // Subtract dimensions first so a zero travel range stays exactly at its fractional origin.
        var next = new PetPosition(
            Math.Clamp(Position.X + _roamingDirection.X * travel, RoamingArea.X, RoamingEndX),
            Math.Clamp(Position.Y + _roamingDirection.Y * travel, RoamingArea.Y, RoamingEndY));
        var dx = next.X - Position.X;
        var dy = next.Y - Position.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        Position = next;
        NormalizeRoamingDirection();
        return distance / RoamingSpeedUnitsPerSecond;
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
        PetAction[] candidates = [PetAction.Wave, PetAction.LookAround, PetAction.Jump, PetAction.LookUp];
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
