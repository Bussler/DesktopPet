namespace DesktopPet.Core;

public readonly record struct PetPosition(double X, double Y);

public readonly record struct PetBounds(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

public sealed record DisplayInfo(string Name, PetBounds WorkArea, bool IsPrimary, double ScaleFactor = 1.0);
