using System.Text.Json;

namespace DesktopPet.Core;

public enum PetAction
{
    Idle,
    RunLeft,
    RunRight,
    Wave,
    LookUp,
    LookAround,
    Jump,
    Feed,
    Sleep,
    Wake,
    Pet
}

public sealed class PetAssetException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class PetDefinition
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public int SchemaVersion { get; init; }
    public string Name { get; init; } = "";
    public string SpriteSheet { get; init; } = "";
    public int SheetWidth { get; init; }
    public int SheetHeight { get; init; }
    public int FrameWidth { get; init; }
    public int FrameHeight { get; init; }
    public double SpriteScale { get; init; } = 1;
    public double DisplayWidth => FrameWidth / SpriteScale;
    public double DisplayHeight => FrameHeight / SpriteScale;
    public List<PetAnimation> Animations { get; init; } = [];

    internal PetDefinition WithDisplayScale(double scale) => new()
    {
        SchemaVersion = SchemaVersion,
        Name = Name,
        SpriteSheet = SpriteSheet,
        SheetWidth = SheetWidth,
        SheetHeight = SheetHeight,
        FrameWidth = FrameWidth,
        FrameHeight = FrameHeight,
        SpriteScale = SpriteScale / scale,
        Animations = Animations.Select(animation => new PetAnimation
        {
            Action = animation.Action,
            Frames = animation.Frames,
            FrameDurationMilliseconds = animation.FrameDurationMilliseconds,
            Loops = animation.Loops,
            MotionBounds = new PetBounds(animation.MotionBounds.X * scale, animation.MotionBounds.Y * scale,
                animation.MotionBounds.Width * scale, animation.MotionBounds.Height * scale)
        }).ToList()
    };

    public static PetDefinition Parse(string json, int actualSheetWidth, int actualSheetHeight)
    {
        PetDefinition definition;
        try
        {
            definition = JsonSerializer.Deserialize<PetDefinition>(json, JsonOptions)
                ?? throw new PetAssetException("The Hood animation manifest is empty.");
        }
        catch (JsonException exception)
        {
            throw new PetAssetException(
                $"The Hood animation manifest is invalid: {exception.Message}",
                exception);
        }

        definition.Validate(actualSheetWidth, actualSheetHeight);
        return definition;
    }

    public PetAnimation GetAnimation(PetAction action) =>
        Animations.First(animation => Enum.Parse<PetAction>(animation.Action, ignoreCase: true) == action);

    public PetBounds MinimumRoamingArea => new(
        0,
        0,
        Math.Max(DisplayWidth, Animations.Max(animation => animation.MotionBounds.Width)),
        Math.Max(DisplayHeight, Animations.Max(animation => animation.MotionBounds.Height)));

    internal void Validate(int actualSheetWidth, int actualSheetHeight)
    {
        if (SchemaVersion != 1)
        {
            throw new PetAssetException($"The Hood animation manifest uses unsupported schema version {SchemaVersion}; expected version 1.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new PetAssetException("The Hood animation manifest must include a pet name.");
        }

        if (string.IsNullOrWhiteSpace(SpriteSheet) || Path.GetFileName(SpriteSheet) != SpriteSheet)
        {
            throw new PetAssetException("The Hood sprite sheet must be a file name beside its animation manifest.");
        }

        if (SheetWidth <= 0 || SheetHeight <= 0 || FrameWidth <= 0 || FrameHeight <= 0)
        {
            throw new PetAssetException("The Hood sprite sheet and frame dimensions must be positive.");
        }

        if (actualSheetWidth != SheetWidth || actualSheetHeight != SheetHeight)
        {
            throw new PetAssetException(
                $"The Hood sprite sheet is {actualSheetWidth}×{actualSheetHeight}px; the manifest expects {SheetWidth}×{SheetHeight}px.");
        }

        if (FrameWidth > SheetWidth || FrameHeight > SheetHeight)
        {
            throw new PetAssetException("The Hood animation frame size is larger than its sprite sheet.");
        }

        if (!double.IsFinite(SpriteScale) || SpriteScale <= 0 ||
            !double.IsFinite(DisplayWidth) || !double.IsFinite(DisplayHeight))
        {
            throw new PetAssetException("The Hood sprite scale must be a finite positive number of pixels per logical unit.");
        }

        var actions = new HashSet<PetAction>();
        foreach (var animation in Animations)
        {
            if (!Enum.TryParse(animation.Action, ignoreCase: true, out PetAction action) || !Enum.IsDefined(action))
            {
                throw new PetAssetException($"The Hood animation manifest names unknown action '{animation.Action}'.");
            }

            if (!actions.Add(action))
            {
                throw new PetAssetException($"The Hood animation manifest defines '{animation.Action}' more than once.");
            }

            if (animation.Frames is null || animation.Frames.Count == 0)
            {
                throw new PetAssetException($"The Hood '{animation.Action}' animation has no frames.");
            }

            if (animation.FrameDurationMilliseconds <= 0)
            {
                throw new PetAssetException($"The Hood '{animation.Action}' animation needs a positive frame duration in milliseconds.");
            }

            var bounds = animation.MotionBounds;
            if (bounds.X < 0 || bounds.Y < 0 || bounds.Width <= 0 || bounds.Height <= 0 ||
                bounds.X + bounds.Width > DisplayWidth || bounds.Y + bounds.Height > DisplayHeight)
            {
                throw new PetAssetException($"The Hood '{animation.Action}' motion bounds must fit inside one animation frame.");
            }

            for (var index = 0; index < animation.Frames.Count; index++)
            {
                var frame = animation.Frames[index];
                if (frame.Column < 0 || frame.Row < 0 ||
                    ((long)frame.Column + 1) * FrameWidth > SheetWidth ||
                    ((long)frame.Row + 1) * FrameHeight > SheetHeight)
                {
                    throw new PetAssetException(
                        $"The Hood '{animation.Action}' frame {index + 1} points outside the sprite sheet.");
                }
            }
        }

        foreach (var requiredAction in Enum.GetValues<PetAction>())
        {
            // Older custom artwork can use its friendly wave for the new interaction.
            if (requiredAction != PetAction.Pet && !actions.Contains(requiredAction))
            {
                throw new PetAssetException($"The Hood animation manifest is missing the required '{requiredAction}' animation.");
            }
        }

        if (!actions.Contains(PetAction.Pet))
        {
            var wave = GetAnimation(PetAction.Wave);
            Animations.Add(new PetAnimation
            {
                Action = "pet", Frames = wave.Frames, FrameDurationMilliseconds = wave.FrameDurationMilliseconds,
                Loops = false, MotionBounds = wave.MotionBounds
            });
        }

        if (!GetAnimation(PetAction.Idle).Loops)
        {
            throw new PetAssetException("The Hood 'Idle' animation must loop.");
        }

        if (GetAnimation(PetAction.Jump).Loops)
        {
            throw new PetAssetException("The Hood 'Jump' reaction must finish instead of looping.");
        }

        if (GetAnimation(PetAction.Pet).Loops)
        {
            throw new PetAssetException("The 'Pet' reaction must finish instead of looping.");
        }
    }
}

public sealed class PetAnimation
{
    public string Action { get; init; } = "";
    public List<SpriteFrame> Frames { get; init; } = [];
    public int FrameDurationMilliseconds { get; init; }
    public bool Loops { get; init; }
    public PetBounds MotionBounds { get; init; }
}

public sealed record SpriteFrame(int Column, int Row);
