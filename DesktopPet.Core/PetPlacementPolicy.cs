namespace DesktopPet.Core;

public sealed record PetPlacement(PetPosition Position, PetBounds RoamingArea, DisplayInfo Display);

public static class PetPlacementPolicy
{
    public static PetPlacement Initial(PetDefinition definition, DisplayInfo primaryDisplay, InitialAreaSettings settings)
    {
        ValidateDisplay(primaryDisplay, definition);
        var minimum = definition.MinimumRoamingArea;
        var workArea = primaryDisplay.WorkArea;
        var width = Math.Min(workArea.Width, Math.Max(minimum.Width, settings.Width));
        var height = Math.Min(workArea.Height, Math.Max(minimum.Height, settings.Height));
        var position = new PetPosition(
            workArea.X + (workArea.Width - definition.DisplayWidth) / 2,
            workArea.Bottom - Math.Min(workArea.Height, definition.DisplayHeight + settings.BottomMargin));
        var area = new PetBounds(
            position.X + (definition.DisplayWidth - width) / 2,
            position.Y + definition.DisplayHeight - height,
            width,
            height);
        return Fit(definition, position, area, primaryDisplay);
    }

    public static PetPlacement Restore(
        PetDefinition definition,
        PetPosition position,
        PetBounds? roamingArea,
        IReadOnlyCollection<DisplayInfo> displays,
        InitialAreaSettings initialArea)
    {
        var primary = GetPrimaryDisplay(displays, definition);
        var display = FindDisplay(position, definition, displays) ?? primary;
        var area = roamingArea ?? new PetBounds(
            position.X + (definition.DisplayWidth - initialArea.Width) / 2,
            position.Y + definition.DisplayHeight - initialArea.Height,
            initialArea.Width,
            initialArea.Height);
        return Fit(definition, position, area, display);
    }

    public static PetPlacement Move(
        PetDefinition definition,
        PetPosition currentPosition,
        PetBounds currentArea,
        PetPosition requestedPosition,
        IReadOnlyCollection<DisplayInfo> displays)
    {
        var primary = GetPrimaryDisplay(displays, definition);
        var display = FindDisplay(requestedPosition, definition, displays) ?? FindNearestDisplay(requestedPosition, definition, displays) ?? primary;
        var translatedArea = currentArea with
        {
            X = currentArea.X + requestedPosition.X - currentPosition.X,
            Y = currentArea.Y + requestedPosition.Y - currentPosition.Y
        };
        return Fit(definition, requestedPosition, translatedArea, display);
    }

    public static PetPlacement Resize(
        PetDefinition definition,
        PetPosition position,
        PetBounds currentArea,
        double requestedWidth,
        double requestedHeight,
        IReadOnlyCollection<DisplayInfo> displays)
    {
        if (!double.IsFinite(requestedWidth) || !double.IsFinite(requestedHeight))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedWidth), "Roaming-area dimensions must be finite.");
        }

        var primary = GetPrimaryDisplay(displays, definition);
        var display = FindDisplay(position, definition, displays) ?? primary;
        var workArea = display.WorkArea;
        var minimum = definition.MinimumRoamingArea;
        var width = Math.Min(workArea.Width, Math.Max(minimum.Width, requestedWidth));
        var height = Math.Min(workArea.Height, Math.Max(minimum.Height, requestedHeight));
        var x = Math.Clamp(
            currentArea.X,
            Math.Max(workArea.X, position.X + definition.DisplayWidth - width),
            Math.Min(position.X, workArea.Right - width));
        var y = Math.Clamp(
            currentArea.Y,
            Math.Max(workArea.Y, position.Y + definition.DisplayHeight - height),
            Math.Min(position.Y, workArea.Bottom - height));
        return new PetPlacement(position, new PetBounds(x, y, width, height), display);
    }

    public static PetPlacement MoveArea(
        PetDefinition definition,
        PetPosition currentPosition,
        PetBounds currentArea,
        double requestedX,
        double requestedY,
        IReadOnlyCollection<DisplayInfo> displays)
    {
        if (!double.IsFinite(requestedX) || !double.IsFinite(requestedY))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedX), "Roaming-area placement must use finite coordinates.");
        }

        var primary = GetPrimaryDisplay(displays, definition);
        var target = new PetPosition(
            requestedX + (currentArea.Width - definition.DisplayWidth) / 2,
            requestedY + (currentArea.Height - definition.DisplayHeight) / 2);
        var display = FindDisplay(target, definition, displays) ?? FindNearestDisplay(target, definition, displays) ?? primary;
        var workArea = display.WorkArea;
        var minimum = definition.MinimumRoamingArea;
        var width = Math.Min(workArea.Width, Math.Max(minimum.Width, currentArea.Width));
        var height = Math.Min(workArea.Height, Math.Max(minimum.Height, currentArea.Height));
        var x = Math.Clamp(requestedX, workArea.X, workArea.Right - width);
        var y = Math.Clamp(requestedY, workArea.Y, workArea.Bottom - height);
        var position = new PetPosition(
            Math.Clamp(currentPosition.X, x, x + width - definition.DisplayWidth),
            Math.Clamp(currentPosition.Y, y, y + height - definition.DisplayHeight));
        return new PetPlacement(position, new PetBounds(x, y, width, height), display);
    }

    private static PetPlacement Fit(PetDefinition definition, PetPosition position, PetBounds area, DisplayInfo display)
    {
        ValidateDisplay(display, definition);
        var workArea = display.WorkArea;
        var minimum = definition.MinimumRoamingArea;
        var width = Math.Min(workArea.Width, Math.Max(minimum.Width, area.Width));
        var height = Math.Min(workArea.Height, Math.Max(minimum.Height, area.Height));
        var containsPet = area.X <= position.X && area.Y <= position.Y &&
                          area.Right >= position.X + definition.DisplayWidth &&
                          area.Bottom >= position.Y + definition.DisplayHeight;
        if (!containsPet || width != area.Width || height != area.Height)
        {
            area = new PetBounds(
                position.X + (definition.DisplayWidth - width) / 2,
                position.Y + (definition.DisplayHeight - height) / 2,
                width,
                height);
        }

        var offsetX = area.X < workArea.X
            ? workArea.X - area.X
            : area.Right > workArea.Right ? workArea.Right - area.Right : 0;
        var offsetY = area.Y < workArea.Y
            ? workArea.Y - area.Y
            : area.Bottom > workArea.Bottom ? workArea.Bottom - area.Bottom : 0;
        position = new PetPosition(position.X + offsetX, position.Y + offsetY);
        area = area with { X = area.X + offsetX, Y = area.Y + offsetY };
        return new PetPlacement(position, area, display);
    }

    private static DisplayInfo GetPrimaryDisplay(IReadOnlyCollection<DisplayInfo> displays, PetDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(displays);
        var primaryDisplays = displays.Where(display => display.IsPrimary).ToArray();
        if (primaryDisplays.Length != 1)
        {
            throw new InvalidOperationException("Hood needs exactly one primary display with a usable work area.");
        }

        ValidateDisplay(primaryDisplays[0], definition);
        return primaryDisplays[0];
    }

    private static DisplayInfo? FindDisplay(PetPosition position, PetDefinition definition, IEnumerable<DisplayInfo> displays)
    {
        var centerX = position.X + definition.DisplayWidth / 2;
        var centerY = position.Y + definition.DisplayHeight / 2;
        return displays
            .Where(display => IsUsable(display, definition) &&
                              centerX >= display.WorkArea.X && centerX <= display.WorkArea.Right &&
                              centerY >= display.WorkArea.Y && centerY <= display.WorkArea.Bottom)
            .OrderBy(display => DistanceSquared(centerX, centerY, display.WorkArea))
            .FirstOrDefault();
    }

    private static DisplayInfo? FindNearestDisplay(PetPosition position, PetDefinition definition, IEnumerable<DisplayInfo> displays)
    {
        var centerX = position.X + definition.DisplayWidth / 2;
        var centerY = position.Y + definition.DisplayHeight / 2;
        return displays
            .Where(display => IsUsable(display, definition))
            .OrderBy(display => DistanceSquared(centerX, centerY, display.WorkArea))
            .FirstOrDefault();
    }

    private static double DistanceSquared(double x, double y, PetBounds area)
    {
        var nearestX = Math.Clamp(x, area.X, area.Right);
        var nearestY = Math.Clamp(y, area.Y, area.Bottom);
        var dx = x - nearestX;
        var dy = y - nearestY;
        return dx * dx + dy * dy;
    }

    private static bool IsUsable(DisplayInfo display, PetDefinition definition) =>
        double.IsFinite(display.WorkArea.X) && double.IsFinite(display.WorkArea.Y) &&
        double.IsFinite(display.WorkArea.Width) && double.IsFinite(display.WorkArea.Height) &&
        display.WorkArea.Width >= definition.DisplayWidth && display.WorkArea.Height >= definition.DisplayHeight &&
        double.IsFinite(display.ScaleFactor) && display.ScaleFactor > 0;

    private static void ValidateDisplay(DisplayInfo display, PetDefinition definition)
    {
        if (!IsUsable(display, definition))
        {
            throw new InvalidOperationException($"Display '{display.Name}' does not have a usable work area and scale for Hood.");
        }
    }
}
