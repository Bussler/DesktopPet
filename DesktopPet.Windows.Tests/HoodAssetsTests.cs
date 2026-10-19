using System.IO;
using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class HoodAssetsTests
{
    [TestMethod]
    [DataRow("hood")]
    [DataRow("pip")]
    [DataRow("crumb")]
    [DataRow("puddle")]
    public void WritingHasEightDistinctUnclippedLoopingPoses(string pet)
    {
        var assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), pet);
        var animation = assets.Definition.GetAnimation(PetAction.Write);
        Assert.AreEqual("write", animation.Action);
        Assert.IsTrue(animation.Loops);
        Assert.AreEqual(120, animation.FrameDurationMilliseconds);
        Assert.AreEqual(8, animation.Frames.Distinct().Count());
        var hashes = new HashSet<string>();
        foreach (var frame in animation.Frames)
        {
            var crop = new CroppedBitmap(assets.SpriteSheet, assets.GetFrameRectangle(frame));
            var pixels = new byte[crop.PixelWidth * crop.PixelHeight * 4];
            crop.CopyPixels(pixels, crop.PixelWidth * 4, 0);
            Assert.IsTrue(Enumerable.Range(0, pixels.Length / 4).Count(i => pixels[i * 4 + 3] > 32) > 1000);
            for (var y = 0; y < crop.PixelHeight; y++)
            for (var x = 0; x < crop.PixelWidth; x++)
                if (x == 0 || y == 0 || x == crop.PixelWidth - 1 || y == crop.PixelHeight - 1)
                    Assert.AreEqual(0, (int)pixels[(y * crop.PixelWidth + x) * 4 + 3], $"{pet} writing frame {frame.Column} is clipped.");
            hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels)));
        }
        Assert.AreEqual(8, hashes.Count, "The writing poses must contain different artwork.");
    }

    [TestMethod]
    public void OlderArtworkWithoutWritingStillLoadsAndSwitchesToPackagedPets()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"HoodLegacyWriting-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var root = Path.Combine(AppContext.BaseDirectory, "Assets");
            var original = Path.Combine(root, "Hood");
            var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(original, "hood.animations.json")))!;
            var animations = manifest["animations"]!.AsArray();
            animations.Remove(animations.Single(animation => animation!["action"]!.GetValue<string>() == "write"));
            File.WriteAllText(Path.Combine(directory, "hood.animations.json"), manifest.ToJsonString());
            File.Copy(Path.Combine(original, "hood_spritesheet.png"), Path.Combine(directory, "hood_spritesheet.png"));

            var legacy = HoodAssets.ReadSource(directory);
            Assert.AreSame(legacy.Definition.GetAnimation(PetAction.Idle), legacy.Definition.GetAnimation(PetAction.Write));
            var packaged = HoodAssets.LoadPetSource(root, "pip", legacy.Definition);
            var restored = HoodAssets.ReadSourceForPet(directory, packaged.Definition);
            Assert.AreSame(restored.Definition.GetAnimation(PetAction.Idle), restored.Definition.GetAnimation(PetAction.Write));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    [DataRow("hood")]
    [DataRow("pip")]
    [DataRow("crumb")]
    [DataRow("puddle")]
    public void WaveHasSixteenDistinctPosesAtOneHundredTwentyMilliseconds(string pet)
    {
        var assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), pet);
        var animation = assets.Definition.GetAnimation(PetAction.Wave);
        Assert.AreEqual(16, animation.Frames.Count);
        Assert.AreEqual(16, animation.Frames.Distinct().Count());
        Assert.AreEqual(120, animation.FrameDurationMilliseconds);
        Assert.IsFalse(animation.Loops);
        foreach (var frame in animation.Frames)
        {
            var crop = new CroppedBitmap(assets.SpriteSheet, assets.GetFrameRectangle(frame));
            var pixels = new byte[crop.PixelWidth * crop.PixelHeight * 4];
            crop.CopyPixels(pixels, crop.PixelWidth * 4, 0);
            Assert.IsTrue(Enumerable.Range(0, pixels.Length / 4).Any(index => pixels[index * 4 + 3] != 0),
                $"{pet} wave pose {frame.Column} must contain artwork.");
        }
    }

    [TestMethod]
    [DataRow(0.5, 1.0, 1536, 1248)]
    [DataRow(1.5, 1.25, 5760, 4680)]
    [DataRow(2.0, 1.5, 6144, 4992)]
    public void PetSizeAndDisplayDpiDetermineDecodedResolution(double petScale, double dpi, int width, int height)
    {
        var source = HoodAssets.LoadPetSource(Path.Combine(AppContext.BaseDirectory, "Assets"), PetCatalog.Hood, null);
        var assets = HoodAssets.LoadForDisplay(source, dpi, dpi, petScale);
        var sheet = assets.SpriteSheet;
        Assert.AreEqual(width, sheet.PixelWidth);
        Assert.AreEqual(height, sheet.PixelHeight);
        Assert.AreEqual(dpi, assets.DpiScaleX);
        var definition = assets.Definition;
        Assert.AreEqual(192d, definition.DisplayWidth, "Source manifest dimensions must remain unscaled.");
        foreach (var frame in definition.Animations.SelectMany(animation => animation.Frames))
        {
            var rect = assets.GetFrameRectangle(frame);
            Assert.IsTrue(rect.X >= 0 && rect.Y >= 0 && rect.Width > 0 && rect.Height > 0);
            Assert.IsTrue(rect.X + rect.Width <= width && rect.Y + rect.Height <= height);
        }
    }

    [TestMethod]
    public void PackagedSpriteAtlasDecodesToThe100PercentDisplaySize()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Assets");

        foreach (var pet in new[] { "hood", "pip", "crumb", "puddle" })
        {
            var loaded = HoodAssets.LoadPet(root, pet, null);
            var definition = loaded.Definition;
            var sheet = loaded.SpriteSheet;

            Assert.AreEqual(definition.SheetWidth / definition.SpriteScale, sheet.PixelWidth,
                $"{pet} should decode only the pixels needed for its 100% logical display size.");
            Assert.AreEqual(definition.SheetHeight / definition.SpriteScale, sheet.PixelHeight,
                $"{pet} should preserve its source aspect ratio at 100% DPI.");
            Assert.AreEqual(192, definition.DisplayWidth);
            Assert.AreEqual(208, definition.DisplayHeight);
        }
    }

    [TestMethod]
    public void PackagedAssetsKeepOriginalManifestsAndCropEveryFrameAtSupportedDpi()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Assets");

        foreach (var pet in new[] { "hood", "pip", "crumb", "puddle" })
        {
            var source = HoodAssets.ReadSource(Path.Combine(root, char.ToUpperInvariant(pet[0]) + pet[1..]));
            AssertScaledFrames(source, HoodAssets.LoadAtScale(source, 1d, 1d), 1d, pet);
        }

        var hoodSource = HoodAssets.ReadSource(Path.Combine(root, "Hood"));
        foreach (var scale in new[] { 1.25, 1.5, 2d })
        {
            AssertScaledFrames(hoodSource, HoodAssets.LoadAtScale(hoodSource, scale, scale), scale, $"Hood at {scale:P0}");
        }
    }

    [TestMethod]
    public void CustomScaledCropsDoNotSamplePixelsFromNeighboringFrames()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"HoodCustomSprites-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            WriteCustomAsset(directory);
            var source = HoodAssets.ReadSource(directory);
            var loaded = HoodAssets.LoadAtScale(source, 1.25, 1.25);
            var sheet = loaded.SpriteSheet;
            var definition = loaded.Definition;
            Assert.AreEqual(28, sheet.PixelWidth);
            Assert.AreEqual(41, sheet.PixelHeight);

            foreach (var frame in new[] { new SpriteFrame(0, 0), new SpriteFrame(1, 0), new SpriteFrame(3, 4) })
            {
                var rectangle = loaded.GetFrameRectangle(frame);
                Assert.IsTrue(rectangle.Width > 0 && rectangle.Height > 0);
                Assert.IsTrue(rectangle.X >= 0 && rectangle.Y >= 0);
                Assert.IsTrue(rectangle.X + rectangle.Width <= sheet.PixelWidth && rectangle.Y + rectangle.Height <= sheet.PixelHeight);
                AssertSolidFrame(sheet, rectangle, CustomCellColor(frame.Column, frame.Row), $"cell {frame.Column},{frame.Row}");
            }

            Assert.AreEqual(5.5, definition.DisplayWidth);
            Assert.AreEqual(6.5, definition.DisplayHeight);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void TinyValidCustomFramesRetainPixelsNeededForNonemptyCrops()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"HoodTinySprites-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            WriteCustomAsset(directory, spriteScale: 20);
            var source = HoodAssets.ReadSource(directory);
            var loaded = HoodAssets.LoadAtScale(source, 1d, 1d);
            var sheet = loaded.SpriteSheet;
            var definition = loaded.Definition;

            Assert.AreEqual(definition.SheetWidth, sheet.PixelWidth);
            Assert.AreEqual(definition.SheetHeight, sheet.PixelHeight);
            foreach (var frame in definition.Animations.SelectMany(animation => animation.Frames).Distinct())
            {
                var rectangle = loaded.GetFrameRectangle(frame);
                Assert.AreEqual(definition.FrameWidth, rectangle.Width);
                Assert.AreEqual(definition.FrameHeight, rectangle.Height);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void EffectiveDisplayScaleChangeReloadsPixelsWithoutChangingPetStateOrGeometry()
    {
        var assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), "hood", null);
        var definition = assets.Definition;
        var session = HoodSession.Start(definition,
            [new DisplayInfo("Primary", new PetBounds(0, 0, 1920, 1080), true)],
            new ZeroRandom(), new NoopSessionStore());
        Action noop = () => { };
        var pet = new HoodOverlayWindow(session, assets, noop, noop, noop, noop, noop, noop, UiStyleCatalog.StickyNotes);
        try
        {
            var size = (pet.Width, pet.Height);
            var position = session.Position;
            var presentation = session.Presentation;
            var dpiChanged = typeof(HoodOverlayWindow).GetMethod("OnDpiChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
            dpiChanged.Invoke(pet, [new DpiScale(1, 1), new DpiScale(1.5, 1.5)]);

            var currentAssets = pet.CurrentAssets!;
            var sheet = currentAssets.SpriteSheet;
            var image = (Image)pet.FindName("PetImage");
            var crop = (CroppedBitmap)image.Source;
            Assert.AreEqual(4608, sheet.PixelWidth);
            Assert.AreEqual(3744, sheet.PixelHeight);
            Assert.AreSame(sheet, crop.Source);
            Assert.AreEqual(size, (pet.Width, pet.Height));
            Assert.AreEqual(192d, image.Width);
            Assert.AreEqual(208d, image.Height);
            Assert.AreEqual(Stretch.Fill, image.Stretch);
            Assert.AreEqual(position, session.Position);
            Assert.AreEqual(presentation, session.Presentation);
            Assert.AreEqual(192d, pet.Width);
            Assert.AreEqual(208d, pet.Height);
        }
        finally
        {
            pet.Close();
        }
    }

    [TestMethod]
    public void FailedDisplayScaleReloadKeepsTheLastValidAppearanceAndPetState()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"HoodReloadFailure-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            WriteCustomAsset(directory);
            var source = HoodAssets.ReadSource(directory);
            var assets = HoodAssets.LoadAtScale(source, 1d, 1d);
            var definition = assets.Definition;
            var session = HoodSession.Start(definition,
                [new DisplayInfo("Primary", new PetBounds(0, 0, 1920, 1080), true)],
                new ZeroRandom(), new NoopSessionStore());
            Action noop = () => { };
            var pet = new HoodOverlayWindow(session, assets, noop, noop, noop, noop, noop, noop, UiStyleCatalog.StickyNotes);
            try
            {
                var image = (Image)pet.FindName("PetImage");
                var originalImage = image.Source;
                var presentation = session.Presentation;
                var needs = session.Needs;
                var position = session.Position;
                File.WriteAllText(Path.Combine(directory, "hood_spritesheet.png"), "corrupt after initial load");

                Assert.Throws<PetAssetException>(() => pet.ReloadAssetsForDpi(1.25, 1.25));

                Assert.AreSame(assets, pet.CurrentAssets);
                Assert.AreSame(originalImage, image.Source);
                Assert.AreEqual(presentation, session.Presentation);
                Assert.AreEqual(needs, session.Needs);
                Assert.AreEqual(position, session.Position);
            }
            finally
            {
                pet.Close();
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void StartupUsesTheScaleOfTheDisplayContainingThePet()
    {
        var loaded = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), "hood", null);
        var definition = loaded.Definition;
        var displays = new[]
        {
            new DisplayInfo("left", new PetBounds(0, 0, 1920, 1080), true, 1.25),
            new DisplayInfo("right", new PetBounds(1920, 0, 2560, 1440), false, 2)
        };
        var scale = WindowsDisplayProvider.GetScaleForPet(new PetPosition(2300, 200), definition, displays);

        Assert.AreEqual(2d, scale);
    }

    [TestMethod]
    public void RollbackKeepsLoadedPixelsWhenReplacementPngHasIdenticalFileMetadata()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"HoodSameMetadata-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            WriteCustomAsset(directory, solidColor: [0, 0, 180, 255]);
            var source = HoodAssets.ReadSource(directory);
            var original = HoodAssets.LoadAtScale(source, 1d, 1d);
            var sheetPath = Path.Combine(directory, "hood_spritesheet.png");
            var manifestPath = Path.Combine(directory, "hood.animations.json");
            var length = new FileInfo(sheetPath).Length;
            var sheetTimestamp = File.GetLastWriteTimeUtc(sheetPath);
            var manifestTimestamp = File.GetLastWriteTimeUtc(manifestPath);
            WriteCustomAsset(directory, solidColor: [1, 0, 180, 255]);
            File.SetLastWriteTimeUtc(sheetPath, sheetTimestamp);
            File.SetLastWriteTimeUtc(manifestPath, manifestTimestamp);
            Assert.AreEqual(length, new FileInfo(sheetPath).Length, "The regression requires identical PNG lengths.");
            var snapshot = HoodAssetRollbackSnapshot.Capture(source, original);
            var (_, restored) = snapshot.LoadAtScale(2d, 2d);
            var sheet = restored.SpriteSheet;
            AssertSolidFrame(sheet, restored.GetFrameRectangle(new SpriteFrame(0, 0)), [0, 0, 180, 255], "rollback");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void SourceDimensionsAndCorruptAssetErrorsRemainActionable()
    {
        var original = Path.Combine(AppContext.BaseDirectory, "Assets", "Hood");
        var directory = Path.Combine(Path.GetTempPath(), $"HoodAssetValidation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.Copy(Path.Combine(original, "hood.animations.json"), Path.Combine(directory, "hood.animations.json"));
            File.Copy(Path.Combine(original, "hood_spritesheet.png"), Path.Combine(directory, "hood_spritesheet.png"));
            var manifestPath = Path.Combine(directory, "hood.animations.json");
            var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
            manifest["sheetWidth"] = 6143;
            File.WriteAllText(manifestPath, manifest.ToJsonString());
            var mismatch = Assert.Throws<PetAssetException>(() => HoodAssets.ReadSource(directory));
            StringAssert.Contains(mismatch.Message, "manifest expects");

            File.Copy(Path.Combine(original, "hood.animations.json"), manifestPath, overwrite: true);
            File.WriteAllText(Path.Combine(directory, "hood_spritesheet.png"), "invalid image");
            var corrupt = Assert.Throws<PetAssetException>(() => HoodAssets.ReadSource(directory));
            StringAssert.Contains(corrupt.Message, "could not be decoded");

            using var image = File.OpenRead(Path.Combine(original, "hood_spritesheet.png"));
            var validHeader = new byte[24];
            Assert.AreEqual(validHeader.Length, image.Read(validHeader));
            File.WriteAllBytes(Path.Combine(directory, "hood_spritesheet.png"), validHeader);
            var source = HoodAssets.ReadSource(directory);
            var decodeFailure = Assert.Throws<PetAssetException>(() => HoodAssets.LoadAtScale(source, 1d, 1d));
            StringAssert.Contains(decodeFailure.Message, "could not be decoded");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void AssertScaledFrames(HoodAssetSource source, LoadedHoodAssets loaded, double dpiScale, string label)
    {
        var definition = source.Definition;
        Assert.AreSame(definition, loaded.Definition);
        var sheet = loaded.SpriteSheet;
        var expectedWidth = (int)Math.Round(definition.SheetWidth * Math.Min(1, dpiScale / definition.SpriteScale), MidpointRounding.AwayFromZero);
        var expectedHeight = (int)Math.Round(definition.SheetHeight * Math.Min(1, dpiScale / definition.SpriteScale), MidpointRounding.AwayFromZero);
        Assert.AreEqual(expectedWidth, sheet.PixelWidth, label);
        Assert.AreEqual(expectedHeight, sheet.PixelHeight, label);
        Assert.AreEqual(192d, definition.DisplayWidth, label);
        Assert.AreEqual(208d, definition.DisplayHeight, label);

        foreach (var frame in definition.Animations.SelectMany(animation => animation.Frames).Distinct())
        {
            var rectangle = loaded.GetFrameRectangle(frame);
            var expectedLeft = (int)Math.Round(frame.Column * definition.FrameWidth * sheet.PixelWidth / (double)definition.SheetWidth, MidpointRounding.AwayFromZero);
            var expectedTop = (int)Math.Round(frame.Row * definition.FrameHeight * sheet.PixelHeight / (double)definition.SheetHeight, MidpointRounding.AwayFromZero);
            Assert.AreEqual(expectedLeft, rectangle.X, $"{label}: {frame}");
            Assert.AreEqual(expectedTop, rectangle.Y, $"{label}: {frame}");
            Assert.IsTrue(rectangle.Width > 0 && rectangle.Height > 0, $"{label}: {frame}");
            Assert.IsTrue(rectangle.X + rectangle.Width <= sheet.PixelWidth && rectangle.Y + rectangle.Height <= sheet.PixelHeight, $"{label}: {frame}");
        }
    }

    private static void WriteCustomAsset(string directory, double spriteScale = 2, byte[]? solidColor = null)
    {
        var pixels = new byte[44 * 65 * 4];
        for (var y = 0; y < 65; y++)
        for (var x = 0; x < 44; x++)
        {
            var color = solidColor ?? CustomCellColor(x / 11, y / 13);
            Array.Copy(color, 0, pixels, (y * 44 + x) * 4, 4);
        }

        var bitmap = BitmapSource.Create(44, 65, 96, 96, PixelFormats.Bgra32, null, pixels, 44 * 4);
        bitmap.Freeze();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(directory, "hood_spritesheet.png"))) encoder.Save(stream);

        var definition = new PetDefinition
        {
            SchemaVersion = 1,
            Name = "Custom grid",
            SpriteSheet = "hood_spritesheet.png",
            SheetWidth = 44,
            SheetHeight = 65,
            FrameWidth = 11,
            FrameHeight = 13,
            SpriteScale = spriteScale,
            Animations = Enum.GetValues<PetAction>().Where(action => action != PetAction.Write).Select(action => new PetAnimation
            {
                Action = action.ToString(),
                Frames = [new SpriteFrame(0, 0), new SpriteFrame(1, 0), new SpriteFrame(3, 4)],
                FrameDurationMilliseconds = 100,
                Loops = action is not (PetAction.Jump or PetAction.Pet),
                MotionBounds = new PetBounds(0, 0, 11 / spriteScale, 13 / spriteScale)
            }).ToList()
        };
        File.WriteAllText(Path.Combine(directory, "hood.animations.json"), JsonSerializer.Serialize(definition));
    }

    private static byte[] CustomCellColor(int column, int row) =>
        [(byte)(20 + 45 * column), (byte)(30 + 35 * row), (byte)(180 - 12 * column - 8 * row), 255];

    private static void AssertSolidFrame(BitmapSource sheet, Int32Rect rectangle, byte[] expected, string label)
    {
        var pixels = new byte[rectangle.Width * rectangle.Height * 4];
        sheet.CopyPixels(rectangle, pixels, rectangle.Width * 4, 0);
        for (var index = 0; index < pixels.Length; index += 4)
        for (var channel = 0; channel < 4; channel++)
        {
            Assert.AreEqual(expected[channel], pixels[index + channel], $"{label}: pixel {index / 4}, channel {channel}");
        }
    }

    private sealed class ZeroRandom : IPetRandomSource
    {
        public int NextIndex(int exclusiveUpperBound) => 0;
    }

    private sealed class NoopSessionStore : ISessionStore
    {
        public void Save(HoodSessionSnapshot snapshot) { }
    }
}
