using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.Windows;

internal sealed record LoadedHoodAssets(PetDefinition Definition, BitmapSource SpriteSheet);

internal static class HoodAssets
{
    public static LoadedHoodAssets Load(string directory)
    {
        var manifestPath = Path.Combine(directory, "hood.animations.json");
        if (!File.Exists(manifestPath))
        {
            throw new PetAssetException($"Hood's animation manifest is missing: {manifestPath}");
        }

        string json;
        try
        {
            json = File.ReadAllText(manifestPath);
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

        BitmapImage sheet;
        try
        {
            sheet = new BitmapImage();
            sheet.BeginInit();
            sheet.CacheOption = BitmapCacheOption.OnLoad;
            sheet.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            sheet.UriSource = new Uri(sheetPath, UriKind.Absolute);
            sheet.EndInit();
            sheet.Freeze();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or UriFormatException or InvalidOperationException)
        {
            throw new PetAssetException($"Hood's sprite sheet could not be decoded: {sheetPath}. Replace it with a valid PNG and relaunch. {exception.Message}", exception);
        }

        var definition = PetDefinition.Parse(json, sheet.PixelWidth, sheet.PixelHeight);
        if (!string.Equals(definition.SpriteSheet, "hood_spritesheet.png", StringComparison.OrdinalIgnoreCase))
        {
            throw new PetAssetException("The Hood animation manifest must refer to hood_spritesheet.png beside the manifest.");
        }

        return new LoadedHoodAssets(definition, sheet);
    }
}
