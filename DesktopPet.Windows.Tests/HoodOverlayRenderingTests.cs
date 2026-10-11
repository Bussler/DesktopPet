using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using DesktopPet.Core;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class HoodOverlayRenderingTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    public void RepeatedRectangleKeepsItsCropWhileTheSessionAndPetPositionAdvance()
    {
        var assets = LoadAssets("hood");
        var definition = (PetDefinition)assets.GetType().GetProperty("Definition")!.GetValue(assets)!;
        var session = CreateIdleSession(definition);
        var pet = CreatePet(session, assets);
        var image = (Image)pet.FindName("PetImage");
        var originalSource = image.Source;
        var sourceChanges = 0;
        var descriptor = DependencyPropertyDescriptor.FromProperty(Image.SourceProperty, typeof(Image))!;
        EventHandler sourceChanged = (_, _) => sourceChanges++;
        descriptor.AddValueChanged(image, sourceChanged);

        try
        {
            typeof(HoodOverlayWindow).GetField("_lastTick", PrivateInstance)!
                .SetValue(pet, Stopwatch.GetTimestamp() - Stopwatch.Frequency / 2);
            typeof(HoodOverlayWindow).GetMethod("OnFrameTimerTick", PrivateInstance)!
                .Invoke(pet, [null, EventArgs.Empty]);

            Assert.IsTrue(session.Presentation.FrameIndex > 0, "The real frame tick must advance the idle animation.");
            Assert.AreNotEqual(NeedLevels.Initial, session.Needs, "Needs must continue advancing on repeated artwork frames.");
            Assert.AreSame(originalSource, image.Source, "A different animation index for the same sprite rectangle must reuse the existing crop.");
            Assert.AreEqual(0, sourceChanges, "The repeated rectangle must not assign Image.Source again.");

            var movedPosition = new PetPosition(session.Position.X + 10, session.Position.Y);
            session.MoveTo(movedPosition, persist: false);
            typeof(HoodOverlayWindow).GetMethod("RenderPresentation", PrivateInstance)!
                .Invoke(pet, null);

            Assert.AreEqual(movedPosition, session.Position);
            Assert.AreEqual(movedPosition.X, pet.Left, "Position updates must remain outside the image-work guard.");
            Assert.AreEqual(movedPosition.Y, pet.Top);
            Assert.AreSame(originalSource, image.Source);
            Assert.AreEqual(0, sourceChanges);
        }
        finally
        {
            descriptor.RemoveValueChanged(image, sourceChanged);
            pet.Close();
        }
    }

    [TestMethod]
    public void SwitchingPetWithTheSameRectangleRefreshesTheDecodedSpriteSource()
    {
        var assets = LoadAssets("hood");
        var definition = (PetDefinition)assets.GetType().GetProperty("Definition")!.GetValue(assets)!;
        var session = CreateIdleSession(definition);
        var pet = CreatePet(session, assets);
        var image = (Image)pet.FindName("PetImage");
        var originalCrop = (CroppedBitmap)image.Source;
        var sourceChanges = 0;
        var descriptor = DependencyPropertyDescriptor.FromProperty(Image.SourceProperty, typeof(Image))!;
        EventHandler sourceChanged = (_, _) => sourceChanges++;
        descriptor.AddValueChanged(image, sourceChanged);

        try
        {
            var loadPet = typeof(HoodOverlayWindow).Assembly.GetType("DesktopPet.Windows.HoodAssets")!
                .GetMethod("LoadPet", BindingFlags.Static | BindingFlags.Public)!;
            var replacementAssets = loadPet.Invoke(null,
                [Path.Combine(AppContext.BaseDirectory, "Assets"), "pip", definition])!;
            var replacementDefinition = (PetDefinition)replacementAssets.GetType().GetProperty("Definition")!.GetValue(replacementAssets)!;
            var presentation = session.Presentation;
            var replacementFrame = replacementDefinition.GetAnimation(presentation.Action).Frames[presentation.FrameIndex];
            var replacementRectangle = (Int32Rect)replacementAssets.GetType().GetMethod("GetFrameRectangle")!
                .Invoke(replacementAssets, [replacementFrame])!;
            Assert.AreEqual(originalCrop.SourceRect, replacementRectangle, "The test requires equal crop coordinates across pets.");

            typeof(HoodOverlayWindow).GetMethods(PrivateInstance)
                .Single(method => method.Name == "ApplyPetAssets" && method.GetParameters().Length == 1)
                .Invoke(pet, [replacementAssets]);

            var replacementCrop = (CroppedBitmap)image.Source;
            var replacementSheet = (BitmapSource)replacementAssets.GetType().GetProperty("SpriteSheet")!.GetValue(replacementAssets)!;
            Assert.AreEqual(originalCrop.SourceRect, replacementCrop.SourceRect);
            Assert.AreNotSame(originalCrop.Source, replacementCrop.Source);
            Assert.AreSame(replacementSheet, replacementCrop.Source);
            Assert.AreEqual(1, sourceChanges, "Switching the decoded asset must refresh Image.Source even at equal coordinates.");
        }
        finally
        {
            descriptor.RemoveValueChanged(image, sourceChanged);
            pet.Close();
        }
    }

    [TestMethod]
    public void HiddenPetLoadsOnRevealAndReleasesAllDecodedImageReferencesOnHide()
    {
        var source = LoadSource(Path.Combine(AppContext.BaseDirectory, "Assets"), "hood");
        var definition = (PetDefinition)source.GetType().GetProperty("Definition")!.GetValue(source)!;
        var session = CreateIdleSession(definition, showSprites: false);
        var pet = CreateMetadataPet(session, source);
        var image = (Image)pet.FindName("PetImage");
        var timer = (System.Windows.Threading.DispatcherTimer)typeof(HoodOverlayWindow)
            .GetField("_frameTimer", PrivateInstance)!.GetValue(pet)!;
        var originalPresentation = session.Presentation;
        var originalNeeds = session.Needs;
        var originalPosition = session.Position;

        try
        {
            Assert.IsNull(GetCurrentAssets(pet), "Hidden startup should retain only the source metadata.");
            Assert.IsNull(image.Source);
            Assert.IsFalse(timer.IsEnabled);

            typeof(HoodOverlayWindow).GetMethod("EnsureSpriteAssetsLoaded", PrivateInstance)!.Invoke(pet, null);
            Assert.IsNotNull(GetCurrentAssets(pet));
            Assert.IsNull(image.Source, "Preloading for a reveal must not render before the setting changes.");

            ApplyConfiguration(session, session.Configuration with
            {
                Behavior = session.Configuration.Behavior with { ShowSprites = true }
            });
            typeof(HoodOverlayWindow).GetMethod("RefreshBehaviorSettings", PrivateInstance)!.Invoke(pet, null);
            Assert.IsTrue(pet.IsVisible);
            Assert.IsInstanceOfType<CroppedBitmap>(image.Source);
            Assert.IsTrue(timer.IsEnabled);

            ApplyConfiguration(session, session.Configuration with
            {
                Behavior = session.Configuration.Behavior with { ShowSprites = false }
            });
            typeof(HoodOverlayWindow).GetMethod("RefreshBehaviorSettings", PrivateInstance)!.Invoke(pet, null);

            Assert.IsFalse(pet.IsVisible);
            Assert.IsFalse(timer.IsEnabled);
            Assert.IsNull(image.Source);
            Assert.IsNull(GetCurrentAssets(pet), "The atlas must no longer be owned by the hidden overlay.");
            Assert.IsNull(typeof(HoodOverlayWindow).GetField("_renderedSpriteSheet", PrivateInstance)!.GetValue(pet));
            Assert.IsNull(typeof(HoodOverlayWindow).GetField("_renderedRectangle", PrivateInstance)!.GetValue(pet));
            Assert.AreEqual(originalPresentation, session.Presentation);
            Assert.AreEqual(originalNeeds, session.Needs);
            Assert.AreEqual(originalPosition, session.Position);
        }
        finally { pet.Close(); }
    }

    [TestMethod]
    public void CorruptArtworkAtRevealKeepsHiddenSessionAndImageStateUsable()
    {
        var root = Path.Combine(Path.GetTempPath(), $"HoodHiddenReveal-{Guid.NewGuid():N}");
        var directory = Path.Combine(root, "Hood");
        Directory.CreateDirectory(directory);
        var packaged = Path.Combine(AppContext.BaseDirectory, "Assets", "Hood");
        File.Copy(Path.Combine(packaged, "hood.animations.json"), Path.Combine(directory, "hood.animations.json"));
        File.Copy(Path.Combine(packaged, "hood_spritesheet.png"), Path.Combine(directory, "hood_spritesheet.png"));
        var source = LoadSource(root, "hood");
        File.WriteAllText(Path.Combine(directory, "hood_spritesheet.png"), "corrupt after metadata load");

        var definition = (PetDefinition)source.GetType().GetProperty("Definition")!.GetValue(source)!;
        var session = CreateIdleSession(definition, showSprites: false);
        var pet = CreateMetadataPet(session, source);
        var image = (Image)pet.FindName("PetImage");
        var originalPresentation = session.Presentation;
        var originalNeeds = session.Needs;
        var originalPosition = session.Position;

        try
        {
            var failure = Assert.Throws<TargetInvocationException>(() =>
                typeof(HoodOverlayWindow).GetMethod("EnsureSpriteAssetsLoaded", PrivateInstance)!.Invoke(pet, null));

            Assert.IsInstanceOfType<PetAssetException>(failure.InnerException);
            Assert.IsNull(GetCurrentAssets(pet));
            Assert.IsNull(image.Source);
            Assert.AreEqual(originalPresentation, session.Presentation);
            Assert.AreEqual(originalNeeds, session.Needs);
            Assert.AreEqual(originalPosition, session.Position);
        }
        finally
        {
            pet.Close();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void HiddenSettingsReportsBrokenArtworkWithoutSavingTheReveal()
    {
        var root = Path.Combine(Path.GetTempPath(), $"HoodHiddenSettings-{Guid.NewGuid():N}");
        var directory = Path.Combine(root, "Hood");
        Directory.CreateDirectory(directory);
        var packaged = Path.Combine(AppContext.BaseDirectory, "Assets", "Hood");
        File.Copy(Path.Combine(packaged, "hood.animations.json"), Path.Combine(directory, "hood.animations.json"));
        File.Copy(Path.Combine(packaged, "hood_spritesheet.png"), Path.Combine(directory, "hood_spritesheet.png"));
        var source = LoadSource(root, "hood");
        File.WriteAllText(Path.Combine(directory, "hood_spritesheet.png"), "corrupt after metadata load");
        var definition = (PetDefinition)source.GetType().GetProperty("Definition")!.GetValue(source)!;
        var session = CreateIdleSession(definition, showSprites: false);
        var pet = CreateMetadataPet(session, source);
        var configurationPath = Path.Combine(root, "config.yaml");
        var controller = new HoodConfigurationController(session, new YamlConfigurationStore(configurationPath));
        var settings = (Window)typeof(HoodOverlayWindow).Assembly.GetType("DesktopPet.Windows.SettingsWindow")!
            .GetConstructors(PrivateInstance).Single(candidate => candidate.GetParameters().Length == 6)
            .Invoke([controller, pet, UiStyleCatalog.StickyNotes, null, "system", null]);

        try
        {
            ((CheckBox)settings.FindName("ShowSpritesCheckBox")!).IsChecked = true;
            typeof(SettingsWindow).GetMethod("OnSaveClick", PrivateInstance)!
                .Invoke(settings, [settings, new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)]);

            Assert.IsFalse(controller.Current.Behavior.ShowSprites);
            Assert.IsFalse(pet.IsVisible);
            Assert.IsNull(GetCurrentAssets(pet));
            Assert.IsFalse(File.Exists(configurationPath), "A failed reveal must not persist ShowSprites=true.");
            var error = (TextBlock)settings.FindName("AppearanceErrorText")!;
            Assert.AreEqual(Visibility.Visible, error.Visibility);
            StringAssert.Contains(error.Text, "Settings were not saved");
            StringAssert.Contains(error.Text, "sprite sheet");
        }
        finally
        {
            settings.Close();
            pet.Close();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CancelAfterATrayVisibilityChangeRestoresArtworkForCurrentVisibility(bool initiallyVisible)
    {
        var source = LoadSource(Path.Combine(AppContext.BaseDirectory, "Assets"), "hood");
        var definition = (PetDefinition)source.GetType().GetProperty("Definition")!.GetValue(source)!;
        var session = CreateIdleSession(definition, initiallyVisible);
        var pet = initiallyVisible ? CreatePet(session, LoadAssets("hood")) : CreateMetadataPet(session, source);
        pet.Opacity = 0;
        pet.ShowInTaskbar = false;
        var configPath = Path.Combine(Path.GetTempPath(), $"HoodRollbackVisibility-{Guid.NewGuid():N}.yaml");
        var controller = new HoodConfigurationController(session, new YamlConfigurationStore(configPath));
        var settings = (SettingsWindow)typeof(SettingsWindow).GetConstructors(PrivateInstance)
            .Single(candidate => candidate.GetParameters().Length == 3)
            .Invoke([controller, pet, UiStyleCatalog.StickyNotes]);
        try
        {
            ((ComboBox)settings.FindName("PetComboBox")).SelectedValue = PetCatalog.Pip;
            if (!initiallyVisible) typeof(HoodOverlayWindow).GetMethod("EnsureSpriteAssetsLoaded", PrivateInstance)!.Invoke(pet, null);
            controller.SaveAndApply("behavior.show_sprites", controller.Current with
            {
                Behavior = controller.Current.Behavior with { ShowSprites = !initiallyVisible }
            });
            typeof(HoodOverlayWindow).GetMethod("RefreshBehaviorSettings", PrivateInstance)!.Invoke(pet, null);
            settings.Close();

            Assert.AreEqual(!initiallyVisible, session.Configuration.Behavior.ShowSprites);
            Assert.AreEqual(!initiallyVisible, pet.IsVisible);
            Assert.AreEqual(!initiallyVisible, GetCurrentAssets(pet) is not null);
            Assert.AreEqual(!initiallyVisible, ((Image)pet.FindName("PetImage")).Source is not null);
            var restoredSource = typeof(HoodOverlayWindow).GetProperty("CurrentSource", PrivateInstance)!.GetValue(pet)!;
            Assert.AreEqual(source.GetType().GetProperty("Directory")!.GetValue(source),
                restoredSource.GetType().GetProperty("Directory")!.GetValue(restoredSource));
        }
        finally { settings.Close(); pet.Close(); File.Delete(configPath); }
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, false, true)]
    public void CancelRestoresOriginalArtworkFromCompressedSnapshotAfterSourceChanges(
        bool deleteSource, bool mutateBeforeSettings, bool resizePreview)
    {
        var root = Path.Combine(Path.GetTempPath(), $"HoodPreviewRollback-{Guid.NewGuid():N}");
        var directory = Path.Combine(root, "Hood");
        Directory.CreateDirectory(directory);
        var packaged = Path.Combine(AppContext.BaseDirectory, "Assets", "Hood");
        File.Copy(Path.Combine(packaged, "hood.animations.json"), Path.Combine(directory, "hood.animations.json"));
        File.Copy(Path.Combine(packaged, "hood_spritesheet.png"), Path.Combine(directory, "hood_spritesheet.png"));

        var source = LoadSource(root, "hood");
        var loadAtScale = typeof(HoodOverlayWindow).Assembly.GetType("DesktopPet.Windows.HoodAssets")!
            .GetMethod("LoadAtScale", BindingFlags.Static | BindingFlags.Public)!;
        var definition = (PetDefinition)source.GetType().GetProperty("Definition")!.GetValue(source)!;
        var session = CreateIdleSession(definition);
        var displayProvider = typeof(HoodOverlayWindow).Assembly.GetType("DesktopPet.Windows.WindowsDisplayProvider")!;
        var displays = displayProvider.GetMethod("GetDisplays", BindingFlags.Static | BindingFlags.Public)!.Invoke(null, null)!;
        var displayScale = (double)displayProvider.GetMethod("GetScaleForPet", BindingFlags.Static | BindingFlags.Public)!
            .Invoke(null, [session.Position, definition, displays])!;
        var originalAssets = loadAtScale.Invoke(null, [source, displayScale, displayScale])!;
        var originalSheet = (BitmapSource)originalAssets.GetType().GetProperty("SpriteSheet")!.GetValue(originalAssets)!;
        var sourcePath = Path.Combine(directory, "hood_spritesheet.png");
        if (mutateBeforeSettings) File.WriteAllText(sourcePath, "changed before Settings opened");
        var pet = CreatePet(session, originalAssets);
        var controller = new HoodConfigurationController(session, new YamlConfigurationStore(Path.Combine(root, "config.yaml")));
        var settings = (Window)typeof(HoodOverlayWindow).Assembly.GetType("DesktopPet.Windows.SettingsWindow")!
            .GetConstructors(PrivateInstance).Single(candidate => candidate.GetParameters().Length == 6)
            .Invoke([controller, pet, UiStyleCatalog.StickyNotes, null, "system", null]);
        var frame = definition.GetAnimation(session.Presentation.Action).Frames[session.Presentation.FrameIndex];
        var rectangle = (Int32Rect)originalAssets.GetType().GetMethod("GetFrameRectangle")!.Invoke(originalAssets, [frame])!;
        var expectedPixels = CopyPixels(originalSheet, rectangle);

        try
        {
            if (resizePreview) ((Slider)settings.FindName("PetSizeSlider")).Value = 50;
            ((System.Windows.Controls.ComboBox)settings.FindName("PetComboBox")!).SelectedValue = PetCatalog.Pip;
            if (resizePreview)
            {
                var previewDirectory = Path.Combine(root, "Pip");
                Directory.CreateDirectory(previewDirectory);
                var pip = Path.Combine(AppContext.BaseDirectory, "Assets", "Pip");
                File.Copy(Path.Combine(pip, "hood.animations.json"), Path.Combine(previewDirectory, "hood.animations.json"));
                File.Copy(Path.Combine(pip, "hood_spritesheet.png"), Path.Combine(previewDirectory, "hood_spritesheet.png"));
                var previewSource = LoadSource(root, "pip");
                var preview = typeof(HoodOverlayWindow).Assembly.GetType("DesktopPet.Windows.HoodAssets")!
                    .GetMethod("LoadForDisplay")!.Invoke(null, [previewSource, displayScale, displayScale, 0.5])!;
                typeof(HoodOverlayWindow).GetMethod("ApplyPetAssets", PrivateInstance, [preview.GetType()])!
                    .Invoke(pet, [preview]);
                File.Delete(Path.Combine(previewDirectory, "hood_spritesheet.png"));
            }
            Assert.IsNull(typeof(SettingsWindow).GetField("_originalAssets", PrivateInstance)!.GetValue(settings),
                "The decoded original should be replaced by its compressed rollback snapshot.");
            var snapshot = typeof(SettingsWindow).GetField("_originalAssetSnapshot", PrivateInstance)!.GetValue(settings)!;
            var snapshotBytes = (int)snapshot.GetType().GetProperty("EncodedByteCount")!.GetValue(snapshot)!;
            var decodedBytes = (long)originalSheet.PixelWidth * originalSheet.PixelHeight * originalSheet.Format.BitsPerPixel / 8;
            Assert.IsTrue(snapshotBytes < decodedBytes, "The rollback image must use less memory than its decoded atlas.");

            if (deleteSource) File.Delete(sourcePath);
            else File.WriteAllText(sourcePath, "changed while Settings was previewing another pet");
            settings.Close();
            Assert.AreEqual(1d, session.PetScale, "Cancel must restore size even when preview artwork is unavailable.");

            var restored = GetCurrentAssets(pet)!;
            var restoredSheet = (BitmapSource)restored.GetType().GetProperty("SpriteSheet")!.GetValue(restored)!;
            var restoredRectangle = (Int32Rect)restored.GetType().GetMethod("GetFrameRectangle")!.Invoke(restored, [frame])!;
            Assert.AreEqual(originalSheet.PixelWidth, restoredSheet.PixelWidth);
            Assert.AreEqual(originalSheet.PixelHeight, restoredSheet.PixelHeight);
            CollectionAssert.AreEqual(expectedPixels, CopyPixels(restoredSheet, restoredRectangle));
            var currentSource = typeof(HoodOverlayWindow).GetProperty("CurrentSource", PrivateInstance)!.GetValue(pet)!;
            Assert.IsNotNull(currentSource.GetType().GetProperty("CachedSpriteSheet")!.GetValue(currentSource),
                "A changed or removed file must leave the original compressed source available for later DPI reloads.");

            typeof(HoodOverlayWindow).GetMethod("ReloadAssetsForDpi", PrivateInstance)!.Invoke(pet, [1.25, 1.25]);
            Assert.AreEqual(1.25, GetCurrentAssets(pet)!.GetType().GetProperty("DpiScaleX")!.GetValue(GetCurrentAssets(pet)));
        }
        finally
        {
            if (resizePreview && Directory.Exists(Path.Combine(root, "Pip")))
                File.Copy(Path.Combine(AppContext.BaseDirectory, "Assets", "Pip", "hood_spritesheet.png"),
                    Path.Combine(root, "Pip", "hood_spritesheet.png"), overwrite: true);
            settings.Close();
            pet.Close();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static object LoadAssets(string pet) => typeof(HoodOverlayWindow).Assembly
        .GetType("DesktopPet.Windows.HoodAssets")!
        .GetMethod("LoadPet", BindingFlags.Static | BindingFlags.Public)!
        .Invoke(null, [Path.Combine(AppContext.BaseDirectory, "Assets"), pet, null])!;

    private static HoodSession CreateIdleSession(PetDefinition definition, bool showSprites = true)
    {
        var configuration = HoodConfiguration.Defaults with
        {
            Behavior = HoodConfiguration.Defaults.Behavior with
                { Roaming = false, RandomEvents = false, ShowSprites = showSprites }
        };
        var snapshot = new HoodSessionSnapshot
        {
            Position = new PetPosition(500, 400),
            Action = PetAction.Idle,
            FrameIndex = 0,
            RemainingRandomInterval = TimeSpan.FromSeconds(30)
        };
        return HoodSession.Start(definition,
            [new DisplayInfo("Primary", new PetBounds(0, 0, 1920, 1080), true)],
            new FixedRandomSource(), new NoopSessionStore(), configuration, snapshot);
    }

    private static HoodOverlayWindow CreatePet(HoodSession session, object assets)
    {
        Action noop = () => { };
        var constructor = typeof(HoodOverlayWindow).GetConstructors(PrivateInstance)
            .Single(candidate => candidate.GetParameters().Length == 9);
        return (HoodOverlayWindow)constructor.Invoke(
            [session, assets, noop, noop, noop, noop, noop, noop, UiStyleCatalog.StickyNotes]);
    }

    private static object LoadSource(string assetsDirectory, string pet) => typeof(HoodOverlayWindow).Assembly
        .GetType("DesktopPet.Windows.HoodAssets")!
        .GetMethod("LoadPetSource", BindingFlags.Static | BindingFlags.Public)!
        .Invoke(null, [assetsDirectory, pet, null])!;

    private static HoodOverlayWindow CreateMetadataPet(HoodSession session, object source)
    {
        Action noop = () => { };
        var constructor = typeof(HoodOverlayWindow).GetConstructors(PrivateInstance)
            .Single(candidate => candidate.GetParameters().Length == 10);
        return (HoodOverlayWindow)constructor.Invoke(
            [session, source, null, noop, noop, noop, noop, noop, noop, UiStyleCatalog.StickyNotes]);
    }

    private static object? GetCurrentAssets(HoodOverlayWindow pet) =>
        typeof(HoodOverlayWindow).GetProperty("CurrentAssets", PrivateInstance)!.GetValue(pet);

    private static byte[] CopyPixels(BitmapSource source, Int32Rect rectangle)
    {
        var pixels = new byte[rectangle.Width * rectangle.Height * source.Format.BitsPerPixel / 8];
        source.CopyPixels(rectangle, pixels, rectangle.Width * source.Format.BitsPerPixel / 8, 0);
        return pixels;
    }

    private static void ApplyConfiguration(HoodSession session, HoodConfiguration configuration) =>
        typeof(HoodSession).GetMethod("ApplyConfiguration", PrivateInstance)!.Invoke(session, [configuration]);

    private sealed class FixedRandomSource : IPetRandomSource
    {
        public int NextIndex(int exclusiveUpperBound) => 0;
    }

    private sealed class NoopSessionStore : ISessionStore
    {
        public HoodSessionSnapshot? Load() => null;
        public void Save(HoodSessionSnapshot snapshot) { }
        public string? ResetAfterAcknowledgement() => null;
    }
}
