using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.Windows;

internal static class MediaHeadphoneArtwork
{
    private static readonly Rect Canvas = new(0, 0, 192, 208);
    private readonly record struct Key(string Pet, PetAction Action, SpriteFrame Frame);
    private sealed record Pair(DrawingImage Behind, DrawingImage InFront);
    private sealed record Artwork(BitmapSource[] Parts, Dictionary<string, double[][]> Poses);
    private static readonly Dictionary<Key, Pair> Cache = [];
    private static readonly Lazy<Dictionary<string, Artwork>> Sprites = new(Load);
    private static readonly Pair Empty = Freeze(NewCanvas(), NewCanvas());

    public static (DrawingImage Behind, DrawingImage InFront) Get(string pet, PetAction action, SpriteFrame frame)
    {
        if (!PetCatalog.TryCanonicalize(pet, out var name)) return (Empty.Behind, Empty.InFront);
        if (!Sprites.Value.TryGetValue(name, out var art) ||
            !art.Poses.TryGetValue(action.ToString(), out var poses) || frame.Column < 0 || frame.Column >= poses.Length)
            return (Empty.Behind, Empty.InFront);

        var key = new Key(name, action, frame);
        if (!Cache.TryGetValue(key, out var pair))
        {
            pair = Create(art, poses[frame.Column], name, action);
            Cache.Add(key, pair);
        }
        return (pair.Behind, pair.InFront);
    }

    private static Pair Create(Artwork art, double[] pose, string pet, PetAction action)
    {
        var behind = NewCanvas();
        var front = NewCanvas();
        var (left, right, y, height, crown, facing, tilt) = (pose[0], pose[1], pose[2], pose[3], pose[4], pose[5], pose[6]);
        var bandInFront = pet is PetCatalog.Hood or PetCatalog.Puddle;
        var hoodProfile = pet == PetCatalog.Hood && facing != 0;
        var rotation = new RotateTransform(tilt, (left + right) / 2, y);
        using (var drawing = behind.Append())
        {
            drawing.PushTransform(rotation);
            // Two-part in-ear artwork has no band in any pose.
            if ((!bandInFront || hoodProfile) && art.Parts.Length == 3)
                DrawBand(drawing, art.Parts[2], left, right, y, height, crown, hoodProfile ? 0 : facing);
            drawing.Pop();
        }
        using (var drawing = front.Append())
        {
            drawing.PushTransform(rotation);
            if (bandInFront)
            {
                // Let the cap occlude the far stem while the arch remains over the hat.
                if (hoodProfile)
                    drawing.PushClip(new RectangleGeometry(new Rect(
                        facing > 0 ? left : left + (right - left) * 0.1, crown,
                        (right - left) * 0.9, Canvas.Height)));
                DrawBand(drawing, art.Parts[2], left, right, y, height, crown, pet == PetCatalog.Hood ? 0 : facing);
                if (hoodProfile) drawing.Pop();
            }
            if (facing == 0)
            {
                DrawCup(drawing, art.Parts[0], left, y, height, false);
                DrawCup(drawing, art.Parts[0], right, y, height, true);
            }
            else
            {
                // Crumb's walking tip points into the ear, with the gold shell outside.
                var flipEarbud = pet == PetCatalog.Crumb && action is PetAction.RunRight or PetAction.RunLeft;
                DrawCup(drawing, art.Parts[1], facing > 0 ? left : right, y, height, (facing < 0) != flipEarbud);
            }
            drawing.Pop();
        }
        return Freeze(behind, front);
    }

    private static void DrawBand(DrawingContext drawing, ImageSource sprite, double left, double right,
        double y, double height, double crown, double facing)
    {
        var bounds = new Rect(left, crown, right - left, Math.Max(5, y - height * 0.4 - crown));
        // The far cup and band disappear behind the head as it turns into profile.
        if (facing != 0)
            drawing.PushClip(new RectangleGeometry(new Rect(facing > 0 ? left : (left + right) / 2, crown, bounds.Width / 2, bounds.Height)));
        drawing.DrawImage(sprite, bounds);
        if (facing != 0) drawing.Pop();
    }

    private static void DrawCup(DrawingContext drawing, BitmapSource sprite, double x, double y, double height, bool mirror)
    {
        var width = height * sprite.PixelWidth / sprite.PixelHeight;
        if (mirror) drawing.PushTransform(new ScaleTransform(-1, 1, x, y));
        drawing.DrawImage(sprite, new Rect(x - width / 2, y - height * 0.65, width, height));
        if (mirror) drawing.Pop();
    }

    private static DrawingGroup NewCanvas()
    {
        var group = new DrawingGroup { ClipGeometry = new RectangleGeometry(Canvas) };
        using var drawing = group.Open();
        drawing.DrawRectangle(System.Windows.Media.Brushes.Transparent, null, Canvas);
        return group;
    }

    private static Pair Freeze(DrawingGroup behind, DrawingGroup front)
    {
        behind.Freeze(); front.Freeze();
        var behindImage = new DrawingImage(behind);
        var frontImage = new DrawingImage(front);
        behindImage.Freeze(); frontImage.Freeze();
        return new Pair(behindImage, frontImage);
    }

    private static Dictionary<string, Artwork> Load()
    {
        try
        {
            var assembly = typeof(MediaHeadphoneArtwork).Assembly;
            var sheet = LoadSheet("DesktopPet.Headphones.png");
            var earbuds = LoadSheet("DesktopPet.CrumbEarbuds.png");
            using var poseStream = assembly.GetManifestResourceStream("DesktopPet.HeadphonePoses.json")
                ?? throw new InvalidDataException("The headphone frame poses are missing.");
            using var document = JsonDocument.Parse(poseStream);
            var result = new Dictionary<string, Artwork>(StringComparer.OrdinalIgnoreCase);
            foreach (var pet in document.RootElement.GetProperty("poses").EnumerateObject())
            {
                var inEar = PetCatalog.Canonicalize(pet.Name) == PetCatalog.Crumb;
                var parts = document.RootElement.GetProperty("parts").GetProperty(pet.Name).EnumerateArray().Select(bounds =>
                {
                    var values = bounds.EnumerateArray().Select(value => value.GetInt32()).ToArray();
                    var crop = new CroppedBitmap(inEar ? earbuds : sheet, new Int32Rect(values[0], values[1], values[2], values[3]));
                    crop.Freeze();
                    return (BitmapSource)crop;
                }).ToArray();
                var poses = pet.Value.EnumerateObject().ToDictionary(action => action.Name,
                    action => action.Value.EnumerateArray().Select(frame => frame.EnumerateArray().Select(value => value.GetDouble()).ToArray()).ToArray(),
                    StringComparer.OrdinalIgnoreCase);
                if (parts.Length != (inEar ? 2 : 3) || poses.Values.SelectMany(frames => frames).Any(frame => frame.Length != 7 || frame.Any(value => !double.IsFinite(value)) || frame[1] <= frame[0] || frame[3] <= 0))
                    throw new InvalidDataException($"The headphone sprite data for {pet.Name} is invalid.");
                result.Add(pet.Name, new Artwork(parts, poses));
            }
            return result;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            MediaHeadphoneDiagnostics.Report($"Headphone artwork could not be loaded; ordinary pet sprites remain active. Restore the app files and relaunch. {exception.Message}");
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static BitmapSource LoadSheet(string resource)
    {
        using var stream = typeof(MediaHeadphoneArtwork).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidDataException($"The headphone sprite atlas {resource} is missing.");
        // Copy pixels before freezing so cached crops do not retain a dispatcher-bound decoder.
        var sheet = new WriteableBitmap(BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0]);
        sheet.Freeze();
        return sheet;
    }
}
