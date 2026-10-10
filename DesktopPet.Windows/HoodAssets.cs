using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.Windows;

internal sealed record HoodAssetSource(string Directory, PetDefinition Definition)
{
    public byte[]? CachedSpriteSheet { get; init; }
    public byte[]? ManifestHash { get; init; }
}

internal sealed class HoodAssetRollbackSnapshot
{
    private readonly byte[] _encodedSpriteSheet;
    private readonly byte[]? _manifestHash;
    private readonly byte[]? _spriteSheetHash;
    private readonly bool _sourceChangedBeforePreview;

    private HoodAssetRollbackSnapshot(HoodAssetSource source, byte[] encodedSpriteSheet,
        byte[]? manifestHash, byte[]? spriteSheetHash, bool sourceChangedBeforePreview)
    {
        Source = source;
        _encodedSpriteSheet = encodedSpriteSheet;
        _manifestHash = manifestHash;
        _spriteSheetHash = spriteSheetHash;
        _sourceChangedBeforePreview = sourceChangedBeforePreview;
    }

    public HoodAssetSource Source { get; }
    public int EncodedByteCount => _encodedSpriteSheet.Length;

    public static HoodAssetRollbackSnapshot Capture(HoodAssetSource source, LoadedHoodAssets assets)
    {
        var manifestPath = Path.Combine(source.Directory, "hood.animations.json");
        var spriteSheetPath = Path.Combine(source.Directory, source.Definition.SpriteSheet);
        var sourceChangedBeforePreview = source.CachedSpriteSheet is null &&
            (source.ManifestHash is null || !HashMatches(manifestPath, source.ManifestHash));
        var originalSpriteSheet = source.CachedSpriteSheet;
        if (originalSpriteSheet is null && !sourceChangedBeforePreview && assets.SpriteSheetHash is { } loadedHash)
        {
            try
            {
                var sourceBytes = File.ReadAllBytes(spriteSheetPath);
                if (CryptographicOperations.FixedTimeEquals(SHA256.HashData(sourceBytes), loadedHash))
                {
                    originalSpriteSheet = sourceBytes;
                }
                else
                {
                    sourceChangedBeforePreview = true;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // If the original file cannot be retained, preserve the displayed pixels below.
            }
        }

        if (originalSpriteSheet is null)
        {
            using var stream = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(assets.SpriteSheet));
            encoder.Save(stream);
            originalSpriteSheet = stream.ToArray();
        }

        var spriteSheetHash = SHA256.HashData(originalSpriteSheet);
        return new HoodAssetRollbackSnapshot(source, originalSpriteSheet, source.ManifestHash,
            spriteSheetHash, sourceChangedBeforePreview);
    }

    public (HoodAssetSource Source, LoadedHoodAssets Assets) LoadAtScale(double dpiScaleX, double dpiScaleY)
    {
        var source = GetRestoreSource();
        if (source.CachedSpriteSheet is null)
        {
            try
            {
                source = HoodAssets.ReadSourceForPet(source.Directory, source.Definition);
            }
            catch (PetAssetException)
            {
                source = Source with { CachedSpriteSheet = _encodedSpriteSheet };
            }
        }

        return (source, HoodAssets.LoadAtScale(source, dpiScaleX, dpiScaleY));
    }

    public bool SourceFilesChanged => Source.CachedSpriteSheet is null &&
        (_sourceChangedBeforePreview || !SourceFilesMatchSnapshot());

    public HoodAssetSource GetRestoreSource()
    {
        if (Source.CachedSpriteSheet is not null) return Source;
        if (!_sourceChangedBeforePreview && SourceFilesMatchSnapshot())
        {
            return Source;
        }

        return Source with { CachedSpriteSheet = _encodedSpriteSheet };
    }

    private bool SourceFilesMatchSnapshot() =>
        _manifestHash is not null && _spriteSheetHash is not null &&
        HashMatches(Path.Combine(Source.Directory, "hood.animations.json"), _manifestHash) &&
        HashMatches(Path.Combine(Source.Directory, Source.Definition.SpriteSheet), _spriteSheetHash);

    private static byte[]? TryHashFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return SHA256.HashData(stream);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static bool HashMatches(string path, byte[] expected) =>
        TryHashFile(path) is { } actual && CryptographicOperations.FixedTimeEquals(actual, expected);
}

internal sealed record LoadedHoodAssets(PetDefinition Definition, BitmapSource SpriteSheet)
{
    public string SourceDirectory { get; init; } = "";
    public HoodAssetSource? Source { get; init; }
    public byte[]? SpriteSheetHash { get; init; }
    public double DpiScaleX { get; init; } = 1;
    public double DpiScaleY { get; init; } = 1;

    public Int32Rect GetFrameRectangle(SpriteFrame frame)
    {
        var x = ScaleBoundary(frame.Column * Definition.FrameWidth, Definition.SheetWidth, SpriteSheet.PixelWidth);
        var y = ScaleBoundary(frame.Row * Definition.FrameHeight, Definition.SheetHeight, SpriteSheet.PixelHeight);
        var right = ScaleBoundary((frame.Column + 1) * Definition.FrameWidth, Definition.SheetWidth, SpriteSheet.PixelWidth);
        var bottom = ScaleBoundary((frame.Row + 1) * Definition.FrameHeight, Definition.SheetHeight, SpriteSheet.PixelHeight);
        return new Int32Rect(x, y, right - x, bottom - y);
    }

    private static int ScaleBoundary(int sourcePixel, int sourceSize, int decodedSize) =>
        (int)Math.Round(sourcePixel * decodedSize / (double)sourceSize, MidpointRounding.AwayFromZero);
}

internal static class HoodAssets
{
    public static LoadedHoodAssets LoadPet(string assetsDirectory, string pet, PetDefinition? currentDefinition = null)
        => LoadAtScale(LoadPetSource(assetsDirectory, pet, currentDefinition), 1, 1);

    public static LoadedHoodAssets LoadPetAtScale(
        string assetsDirectory,
        string pet,
        PetDefinition? currentDefinition,
        double dpiScaleX,
        double dpiScaleY)
        => LoadAtScale(LoadPetSource(assetsDirectory, pet, currentDefinition), dpiScaleX, dpiScaleY);

    public static HoodAssetSource LoadPetSource(string assetsDirectory, string pet, PetDefinition? currentDefinition = null)
    {
        var canonical = PetCatalog.Canonicalize(pet);
        var folder = char.ToUpperInvariant(canonical[0]) + canonical[1..];
        var source = ReadSource(Path.Combine(assetsDirectory, folder));
        if (currentDefinition is not null)
        {
            ValidateCompatibility(source.Definition, currentDefinition, folder);
        }

        return source;
    }

    public static HoodAssetSource ReadSourceForPet(string directory, PetDefinition currentDefinition)
    {
        var source = ReadSource(directory);
        ValidateCompatibility(source.Definition, currentDefinition, Path.GetFileName(source.Directory));
        return source;
    }

    public static LoadedHoodAssets Load(string directory)
        => LoadAtScale(ReadSource(directory), 1, 1);

    public static HoodAssetSource ReadSource(string directory)
    {
        directory = Path.GetFullPath(directory);
        var manifestPath = Path.Combine(directory, "hood.animations.json");
        if (!File.Exists(manifestPath))
        {
            throw new PetAssetException($"Hood's animation manifest is missing: {manifestPath}");
        }

        string json;
        byte[] manifestHash;
        try
        {
            using var stream = File.OpenRead(manifestPath);
            manifestHash = SHA256.HashData(stream);
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            json = reader.ReadToEnd();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new PetAssetException($"Hood's animation manifest could not be read: {manifestPath}. {exception.Message}", exception);
        }

        var sheetPath = Path.Combine(directory, "hood_spritesheet.png");
        if (!File.Exists(sheetPath))
        {
            throw new PetAssetException($"Hood's sprite sheet is missing: {sheetPath}. Restore the packaged Hood assets and relaunch.");
        }

        int sourceWidth;
        int sourceHeight;
        try
        {
            (sourceWidth, sourceHeight) = ReadPngDimensions(sheetPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or InvalidDataException)
        {
            throw new PetAssetException($"Hood's sprite sheet could not be decoded: {sheetPath}. Replace it with a valid PNG and relaunch. {exception.Message}", exception);
        }

        var definition = PetDefinition.Parse(json, sourceWidth, sourceHeight);
        if (!string.Equals(definition.SpriteSheet, "hood_spritesheet.png", StringComparison.OrdinalIgnoreCase))
        {
            throw new PetAssetException("The Hood animation manifest must refer to hood_spritesheet.png beside the manifest.");
        }

        return new HoodAssetSource(directory, definition)
        {
            ManifestHash = manifestHash
        };
    }

    public static LoadedHoodAssets LoadAtScale(HoodAssetSource source, double dpiScaleX, double dpiScaleY)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!double.IsFinite(dpiScaleX) || dpiScaleX <= 0 || !double.IsFinite(dpiScaleY) || dpiScaleY <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpiScaleX), "Display DPI scale must be finite and positive.");
        }

        var sheetPath = Path.Combine(source.Directory, source.Definition.SpriteSheet);
        try
        {
            var sheet = new BitmapImage();
            sheet.BeginInit();
            sheet.CacheOption = BitmapCacheOption.OnLoad;
            sheet.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            var width = ScaledPixels(source.Definition.SheetWidth, dpiScaleX, source.Definition.SpriteScale);
            var height = ScaledPixels(source.Definition.SheetHeight, dpiScaleY, source.Definition.SpriteScale);
            if (source.Definition.FrameWidth * width / (double)source.Definition.SheetWidth < 1 ||
                source.Definition.FrameHeight * height / (double)source.Definition.SheetHeight < 1)
            {
                width = source.Definition.SheetWidth;
                height = source.Definition.SheetHeight;
            }

            if (width != source.Definition.SheetWidth) sheet.DecodePixelWidth = width;
            if (height != source.Definition.SheetHeight) sheet.DecodePixelHeight = height;
            byte[] spriteSheetHash;
            if (source.CachedSpriteSheet is { } cachedSpriteSheet)
            {
                spriteSheetHash = SHA256.HashData(cachedSpriteSheet);
                using var stream = new MemoryStream(cachedSpriteSheet, writable: false);
                sheet.StreamSource = stream;
                sheet.EndInit();
            }
            else
            {
                // Hash and decode the same locked file so rollback identifies the displayed artwork.
                using var stream = File.OpenRead(sheetPath);
                spriteSheetHash = SHA256.HashData(stream);
                stream.Position = 0;
                sheet.StreamSource = stream;
                sheet.EndInit();
            }
            sheet.Freeze();
            return new LoadedHoodAssets(source.Definition, sheet)
            {
                SourceDirectory = source.Directory,
                Source = source,
                SpriteSheetHash = spriteSheetHash,
                DpiScaleX = dpiScaleX,
                DpiScaleY = dpiScaleY
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or UriFormatException or InvalidOperationException or FormatException)
        {
            throw new PetAssetException($"Hood's sprite sheet could not be decoded: {sheetPath}. Replace it with a valid PNG and relaunch. {exception.Message}", exception);
        }
    }

    private static int ScaledPixels(int sourcePixels, double dpiScale, double spriteScale) =>
        Math.Clamp((int)Math.Round(sourcePixels * Math.Min(1, dpiScale / spriteScale), MidpointRounding.AwayFromZero), 1, sourcePixels);

    private static void ValidateCompatibility(PetDefinition definition, PetDefinition currentDefinition, string folder)
    {
        // The session keeps its timers, motion bounds, and current frame during an artwork reload.
        if (definition.DisplayWidth != currentDefinition.DisplayWidth ||
            definition.DisplayHeight != currentDefinition.DisplayHeight ||
            Enum.GetValues<PetAction>().Any(action =>
            {
                var current = currentDefinition.GetAnimation(action);
                var next = definition.GetAnimation(action);
                return current.Frames.Count != next.Frames.Count ||
                    current.FrameDurationMilliseconds != next.FrameDurationMilliseconds ||
                    current.Loops != next.Loops || current.MotionBounds != next.MotionBounds;
            }))
        {
            throw new PetAssetException($"{folder}'s animations must match the current pet's frame counts, timing, motion bounds, and display size. Restore the packaged assets before switching pets.");
        }
    }

    private static (int Width, int Height) ReadPngDimensions(string path)
    {
        Span<byte> header = stackalloc byte[24];
        using var stream = File.OpenRead(path);
        var read = 0;
        while (read < header.Length)
        {
            var count = stream.Read(header[read..]);
            if (count == 0)
            {
                throw new InvalidDataException("The PNG header is incomplete.");
            }

            read += count;
        }

        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (!header[..8].SequenceEqual(signature) ||
            BinaryPrimitives.ReadUInt32BigEndian(header[8..12]) != 13 ||
            !header[12..16].SequenceEqual("IHDR"u8))
        {
            throw new InvalidDataException("The file does not begin with a PNG IHDR chunk.");
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(header[16..20]);
        var height = BinaryPrimitives.ReadUInt32BigEndian(header[20..24]);
        if (width is 0 or > int.MaxValue || height is 0 or > int.MaxValue)
        {
            throw new InvalidDataException("The PNG dimensions are outside the supported range.");
        }

        return ((int)width, (int)height);
    }
}
