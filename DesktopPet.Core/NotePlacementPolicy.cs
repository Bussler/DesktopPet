namespace DesktopPet.Core;

public static class NotePlacementPolicy
{
    public static NoteBounds Restore(NoteBounds bounds, IReadOnlyCollection<DisplayInfo> displays)
    {
        ArgumentNullException.ThrowIfNull(displays);
        NotesSnapshotValidator.ValidateBounds(bounds);

        var usableDisplays = displays
            .Where(IsUsable)
            .ToArray();
        if (usableDisplays.Length == 0)
        {
            throw new InvalidOperationException("No display work area can fit a note at its minimum usable size of 240×180 logical units.");
        }

        if (usableDisplays.Any(display => Contains(display.WorkArea, bounds)))
        {
            return bounds;
        }

        var display = usableDisplays
            .OrderByDescending(candidate => IntersectionArea(candidate.WorkArea, bounds))
            .ThenBy(candidate => DistanceFromCenter(candidate.WorkArea, bounds))
            .ThenByDescending(candidate => candidate.IsPrimary)
            .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
            .First();
        var area = display.WorkArea;
        var width = Math.Min(bounds.Width, area.Width);
        var height = Math.Min(bounds.Height, area.Height);
        var restored = new NoteBounds(
            Math.Clamp(bounds.Left, area.X, area.Right - width),
            Math.Clamp(bounds.Top, area.Y, area.Bottom - height),
            width,
            height);
        NotesSnapshotValidator.ValidateBounds(restored);
        return restored;
    }

    private static bool IsUsable(DisplayInfo? display)
    {
        if (display is null || !double.IsFinite(display.ScaleFactor) || display.ScaleFactor <= 0)
        {
            return false;
        }

        var area = display.WorkArea;
        return double.IsFinite(area.X) && double.IsFinite(area.Y) &&
               double.IsFinite(area.Width) && double.IsFinite(area.Height) &&
               double.IsFinite(area.Right) && double.IsFinite(area.Bottom) &&
               area.Width >= NotesSnapshotValidator.MinimumNoteWidth &&
               area.Height >= NotesSnapshotValidator.MinimumNoteHeight;
    }

    private static bool Contains(PetBounds area, NoteBounds bounds) =>
        bounds.Left >= area.X && bounds.Top >= area.Y &&
        bounds.Left + bounds.Width <= area.Right &&
        bounds.Top + bounds.Height <= area.Bottom;

    private static double IntersectionArea(PetBounds area, NoteBounds bounds)
    {
        var width = Math.Max(0, Math.Min(area.Right, bounds.Left + bounds.Width) - Math.Max(area.X, bounds.Left));
        var height = Math.Max(0, Math.Min(area.Bottom, bounds.Top + bounds.Height) - Math.Max(area.Y, bounds.Top));
        return width * height;
    }

    private static double DistanceFromCenter(PetBounds area, NoteBounds bounds)
    {
        var centerX = bounds.Left + bounds.Width / 2;
        var centerY = bounds.Top + bounds.Height / 2;
        var dx = Math.Max(area.X - centerX, Math.Max(0, centerX - area.Right));
        var dy = Math.Max(area.Y - centerY, Math.Max(0, centerY - area.Bottom));
        return dx * dx + dy * dy;
    }
}
