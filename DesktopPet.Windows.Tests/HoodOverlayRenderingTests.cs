using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using DesktopPet.Core;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class HoodOverlayRenderingTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    [DataRow("hood")]
    [DataRow("pip")]
    [DataRow("crumb")]
    [DataRow("puddle")]
    public void PetMenuActionFollowsFeedAndPlaysTheSelectedPetsAttentionAnimationOnce(string petName)
    {
        var assets = (LoadedHoodAssets)LoadAssets(petName);
        var session = CreateIdleSession(assets.Definition);
        session.Advance(TimeSpan.FromHours(3));
        var boredom = session.Needs.Boredom;
        var position = session.Position;
        var pet = CreatePet(session, assets);
        try
        {
            var menu = pet.ContextMenu;
            var feed = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Feed"));
            var action = (MenuItem)menu.Items[menu.Items.IndexOf(feed) + 1];
            Assert.AreEqual("Pet", action.Header);
            action.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.AreEqual(boredom - 30, session.Needs.Boredom, 1e-9);
            Assert.AreEqual(PetAction.Pet, session.Presentation.Action);
            var animation = assets.Definition.GetAnimation(PetAction.Pet);
            Assert.AreEqual(16, animation.Frames.Count);
            Assert.IsFalse(animation.Loops);
            Assert.AreEqual(120, animation.FrameDurationMilliseconds);
            var image = (Image)pet.FindName("PetImage");
            Assert.AreEqual(assets.GetFrameRectangle(animation.Frames[0]), ((CroppedBitmap)image.Source).SourceRect);

            session.Advance(TimeSpan.FromMilliseconds(1919));
            typeof(HoodOverlayWindow).GetMethod("RenderPresentation", PrivateInstance)!.Invoke(pet, null);
            Assert.AreEqual(PetAction.Pet, session.Presentation.Action);
            Assert.AreEqual(assets.GetFrameRectangle(animation.Frames[^1]), ((CroppedBitmap)image.Source).SourceRect);
            session.Advance(TimeSpan.FromMilliseconds(1));
            Assert.AreEqual(PetAction.Idle, session.Presentation.Action);
            session.Advance(TimeSpan.FromSeconds(2));
            Assert.AreEqual(PetAction.Idle, session.Presentation.Action);
            Assert.AreEqual(position, session.Position);
        }
        finally { pet.Close(); }
    }

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
    public void ValidCustomManifestNameRetainsOrdinaryArtworkDuringPlayback()
    {
        var builtIn = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), "hood");
        var manifest = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(builtIn.Definition))!;
        manifest["Name"] = "Custom grid";
        var definition = PetDefinition.Parse(manifest.ToJsonString(), builtIn.Definition.SheetWidth, builtIn.Definition.SheetHeight);
        var assets = builtIn with { Definition = definition, Source = builtIn.Source! with { Definition = definition } };
        var session = CreateIdleSession(definition);
        var pet = CreatePet(session, assets, new UnavailableMediaAudioProbe());
        var image = (Image)pet.FindName("PetImage");
        var ordinary = image.Source;
        try
        {
            pet.ApplyMediaAudioObservation(new(true, [new("spotify", "spotify", 0.1f, false, 1, false, 1)]), TimeSpan.Zero);
            Assert.AreSame(ordinary, image.Source);
            var accessory = (DrawingImage)((Image)pet.FindName("HeadphoneCupsImage")).Source;
            Assert.AreEqual(1, ((DrawingGroup)accessory.Drawing).Children.Count, "Unknown custom names use transparent accessory fallback.");
        }
        finally { pet.Close(); }
    }

    [TestMethod]
    [DataRow(PetCatalog.Hood)]
    [DataRow(PetCatalog.Pip)]
    [DataRow(PetCatalog.Crumb)]
    [DataRow(PetCatalog.Puddle)]
    public void EveryOverlayPosePreservesItsSessionThroughMuteHandoffSilenceAndRecovery(string petName)
    {
        var assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), petName);
        foreach (var animation in assets.Definition.Animations)
        for (var index = 0; index < animation.Frames.Count; index++)
        {
            var action = Enum.Parse<PetAction>(animation.Action, true);
            var snapshot = new HoodSessionSnapshot
            {
                Position = new(500, 400), Action = action, FrameIndex = index,
                Mode = action == PetAction.Sleep ? HoodSessionMode.Sleeping : HoodSessionMode.Awake,
                Needs = new(25, 40, 30), RemainingRandomInterval = TimeSpan.FromSeconds(30),
                RemainingNap = action == PetAction.Sleep ? TimeSpan.FromSeconds(20) : TimeSpan.Zero,
                NapRecoveryPointsPerSecond = action == PetAction.Sleep ? 1.5 : 0
            };
            var configuration = HoodConfiguration.Defaults with
            { Pet = petName, Behavior = HoodConfiguration.Defaults.Behavior with { RandomEvents = false } };
            var session = HoodSession.Start(assets.Definition, [new DisplayInfo("Primary", new(0, 0, 1920, 1080), true)],
                new FixedRandomSource(), new NoopSessionStore(), configuration, snapshot);
            var reference = HoodSession.Start(assets.Definition, [new DisplayInfo("Primary", new(0, 0, 1920, 1080), true)],
                new FixedRandomSource(), new NoopSessionStore(), configuration, snapshot);
            var pet = CreatePet(session, assets, new UnavailableMediaAudioProbe());
            var cups = (Image)pet.FindName("HeadphoneCupsImage");
            var ordinary = ((Image)pet.FindName("PetImage")).Source;
            var expected = State();
            try
            {
                Observe(new(true, [Source("a", "spotify", 0.1f), Source("b", "chrome", 0.1f)]), 0, true);
                Observe(new(true, [Source("a", "spotify", 0, muted: true), Source("b", "chrome", 0)]), 500, true);
                Observe(new(true, [], [new("b", true, 1)]), 1000, false);
                Observe(new(true, [Source("c", "firefox", 0.1f)]), 1500, true);
                Observe(MediaAudioObservation.Unavailable, 2000, false);
                Observe(new(true, [Source("c", "firefox", 0.1f)]), 2500, true);
                Observe(new(true, [Source("c", "firefox", 0)]), 5499, true);
                Observe(new(true, [Source("c", "firefox", 0)]), 5500, false);
                pet.ApplyMediaHeadphones(false);
                Observe(new(true, [Source("c", "firefox", 0.1f)]), 6000, false);
                var step = TimeSpan.FromMilliseconds(animation.FrameDurationMilliseconds);
                session.Advance(step);
                reference.Advance(step);
                Assert.AreEqual(reference.Presentation, session.Presentation, "The next frame and movement must continue at the original timing.");
                Assert.AreEqual(reference.Needs, session.Needs);
                Assert.AreEqual(reference.RemainingNap, session.RemainingNap);
            }
            finally { pet.Close(); }

            object State() => new
            {
                session.Presentation, session.Needs, session.Mode, session.Position,
                session.RemainingNap, session.NapRecoveryPointsPerSecond, session.RemainingEarlyWakeGrace,
                session.RemainingRandomInterval,
                session.RoamingArea, session.MinimumRoamingArea, session.PetWidth, session.PetHeight
            };
            void Observe(MediaAudioObservation observation, int milliseconds, bool visible)
            {
                pet.ApplyMediaAudioObservation(observation, TimeSpan.FromMilliseconds(milliseconds));
                Assert.AreEqual(visible, cups.Source is not null, $"{petName} {action} frame {index}, t={milliseconds}");
                Assert.AreEqual(expected, State(), "Audio must preserve action/frame, needs, naps, timers and bounds.");
                Assert.AreSame(ordinary, ((Image)pet.FindName("PetImage")).Source);
                Assert.AreEqual(session.PetWidth, pet.Width);
                Assert.AreEqual(session.PetHeight, pet.Height);
                if (visible)
                {
                    var art = MediaHeadphoneArtwork.Get(petName, action, animation.Frames[index]);
                    Assert.AreSame(art.InFront, cups.Source, "The actual overlay must use the current pet/action/frame artwork.");
                }
            }
        }
        static MediaAudioSample Source(string endpoint, string process, float peak, bool muted = false) =>
            new($"{endpoint}:{process}", process, peak, muted, 1, false, 1, endpoint);
    }

    [TestMethod]
    public void AllAvailablePetHeadphonesCoverEveryAnimationFrameAndPreserveTheSession()
    {
        var assets = LoadAssets("hood");
        var definition = (PetDefinition)assets.GetType().GetProperty("Definition")!.GetValue(assets)!;
        foreach (var petName in new[] { "hood", "pip", "crumb", "puddle" })
        {
            var petAssets = LoadAssets(petName);
            var petDefinition = (PetDefinition)petAssets.GetType().GetProperty("Definition")!.GetValue(petAssets)!;
            foreach (var animation in petDefinition.Animations)
            {
                var action = Enum.Parse<PetAction>(animation.Action, ignoreCase: true);
                for (var index = 0; index < animation.Frames.Count; index++)
                {
                    var art = MediaHeadphoneArtwork.Get(petDefinition.Name, action, animation.Frames[index]);
                    Assert.AreEqual(new Rect(0, 0, 192, 208), art.Behind.Drawing.Bounds, $"{petName} {action} frame {index} band canvas");
                    Assert.AreEqual(new Rect(0, 0, 192, 208), art.InFront.Drawing.Bounds, $"{petName} {action} frame {index} cup canvas");
                    var bandDrawing = petName is "hood" or "puddle" ? art.InFront.Drawing : art.Behind.Drawing;
                    Assert.IsTrue(((DrawingGroup)bandDrawing).Children.Count > 1, $"{petName} {action} frame {index} band art");
                    Assert.IsTrue(((DrawingGroup)art.InFront.Drawing).Children.Count > 1, $"{petName} {action} frame {index} cup art");
                }
            }
        }

        var session = CreateIdleSession(definition);
        var pet = CreatePet(session, assets);
        var band = (Image)pet.FindName("HeadphoneBandImage");
        var cups = (Image)pet.FindName("HeadphoneCupsImage");
        var presentation = session.Presentation;
        var needs = session.Needs;
        var position = session.Position;
        var audible = new MediaAudioObservation(true,
            [new MediaAudioSample("device:spotify", "spotify", 0.02f, false, 1, false, 1)]);

        try
        {
            pet.ApplyMediaAudioObservation(audible, TimeSpan.Zero);
            Assert.IsInstanceOfType<DrawingImage>(band.Source);
            Assert.IsInstanceOfType<DrawingImage>(cups.Source);
            Assert.AreEqual(presentation, session.Presentation);
            Assert.AreEqual(needs, session.Needs);
            Assert.AreEqual(position, session.Position);

            pet.ApplyMediaAudioObservation(new MediaAudioObservation(true, []), TimeSpan.FromMilliseconds(2_999));
            Assert.IsNotNull(cups.Source, "Recent sound remains visible during the quiet grace.");
            pet.ApplyMediaAudioObservation(new MediaAudioObservation(true, []), TimeSpan.FromSeconds(3));
            Assert.IsNull(band.Source);
            Assert.IsNull(cups.Source);

            pet.ApplyMediaAudioObservation(audible, TimeSpan.FromSeconds(4));
            Assert.IsNotNull(cups.Source);
            pet.ApplyMediaHeadphones(false);
            Assert.IsNull(band.Source, "Disabling the preference clears the accessory immediately.");
            Assert.IsNull(cups.Source);
            pet.ApplyMediaAudioObservation(audible, TimeSpan.FromSeconds(4.2));
            Assert.IsNull(cups.Source, "Disabled headphones remain hidden even if audio continues.");
        }
        finally { pet.Close(); }
    }

    [TestMethod]
    [DataRow(PetCatalog.Pip)]
    [DataRow(PetCatalog.Crumb)]
    [DataRow(PetCatalog.Puddle)]
    public void AlternatePetPreviewUsesCurrentAudioStateWhenCancelRestoresHood(string selectedPet)
    {
        var assets = LoadAssets("hood");
        var definition = (PetDefinition)assets.GetType().GetProperty("Definition")!.GetValue(assets)!;
        var session = CreateIdleSession(definition);
        var pet = CreatePet(session, assets);
        var controller = new HoodConfigurationController(session,
            new YamlConfigurationStore(Path.Combine(Path.GetTempPath(), $"HoodMediaPreview-{Guid.NewGuid():N}.yaml")));
        var settings = (Window)typeof(HoodOverlayWindow).Assembly.GetType("DesktopPet.Windows.SettingsWindow")!
            .GetConstructors(PrivateInstance).Single(candidate => candidate.GetParameters().Length == 6)
            .Invoke([controller, pet, UiStyleCatalog.StickyNotes, null, "system", null]);
        var presentation = session.Presentation;
        var needs = session.Needs;
        var audible = new MediaAudioObservation(true,
            [new MediaAudioSample("device:chrome", "chrome", 0.02f, false, 1, false, 1)]);

        try
        {
            pet.ApplyMediaAudioObservation(audible, TimeSpan.Zero);
            ((ComboBox)settings.FindName("PetComboBox")!).SelectedValue = selectedPet;
            var alternateArt = MediaHeadphoneArtwork.Get(selectedPet, presentation.Action,
                definition.GetAnimation(presentation.Action).Frames[presentation.FrameIndex]);
            Assert.AreSame(alternateArt.Behind, ((Image)pet.FindName("HeadphoneBandImage")).Source);
            Assert.AreSame(alternateArt.InFront, ((Image)pet.FindName("HeadphoneCupsImage")).Source);

            pet.ApplyMediaAudioObservation(new MediaAudioObservation(true, []), TimeSpan.FromSeconds(3));
            Assert.IsNull(((Image)pet.FindName("HeadphoneCupsImage")).Source);
            typeof(SettingsWindow).GetMethod("OnCancelClick", PrivateInstance)!
                .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);

            Assert.AreEqual(PetCatalog.Hood, controller.Current.Pet);
            Assert.IsNull(((Image)pet.FindName("HeadphoneBandImage")).Source,
                "Cancel restores Hood with the current silent state, not the audio status at preview start.");
            Assert.AreEqual(presentation, session.Presentation);
            Assert.AreEqual(needs, session.Needs);
        }
        finally { settings.Close(); pet.Close(); }
    }

    [TestMethod]
    public void SuspendClearsHeadphonesAndResumeStartsFreshObservationWithoutChangingPetState()
    {
        var assets = LoadAssets("hood");
        var definition = (PetDefinition)assets.GetType().GetProperty("Definition")!.GetValue(assets)!;
        var session = CreateIdleSession(definition);
        var pet = CreatePet(session, assets, new UnavailableMediaAudioProbe());
        var presentation = session.Presentation;
        var needs = session.Needs;
        var position = session.Position;
        var observer = typeof(HoodOverlayWindow).GetField("_mediaHeadphoneObserver", PrivateInstance)!;
        var suspended = typeof(HoodOverlayWindow).GetField("_suspended", PrivateInstance)!;
        var band = (Image)pet.FindName("HeadphoneBandImage");
        var cups = (Image)pet.FindName("HeadphoneCupsImage");
        var audible = new MediaAudioObservation(true,
            [new MediaAudioSample("device:spotify", "spotify", 0.02f, false, 1, false, 1)]);
        var applyPowerMode = typeof(HoodOverlayWindow).GetMethod("ApplyPowerMode", PrivateInstance)!;

        try
        {
            pet.ApplyMediaAudioObservation(audible, TimeSpan.Zero);
            Assert.IsNotNull(cups.Source);

            applyPowerMode.Invoke(pet, [PowerModes.Suspend]);
            Assert.AreEqual(1, suspended.GetValue(pet));
            Assert.IsNull(observer.GetValue(pet));
            Assert.IsNull(band.Source);
            Assert.IsNull(cups.Source);

            applyPowerMode.Invoke(pet, [PowerModes.Resume]);
            Assert.AreEqual(0, suspended.GetValue(pet));
            Assert.IsNotNull(observer.GetValue(pet), "Enabled detection starts a new observation after resume.");
            Assert.IsNull(cups.Source, "Pre-suspend sound does not survive the resume reset.");

            pet.ApplyMediaAudioObservation(audible, TimeSpan.Zero);
            Assert.IsNotNull(cups.Source, "A fresh post-resume observation can activate headphones again.");
            Assert.AreEqual(presentation, session.Presentation);
            Assert.AreEqual(needs, session.Needs);
            Assert.AreEqual(position, session.Position);

        }
        finally { pet.Close(); }

        applyPowerMode.Invoke(pet, [PowerModes.Resume]);
        Assert.IsNull(observer.GetValue(pet), "A queued resume cannot restart observation after the overlay closes.");
    }

    [TestMethod]
    public void HiddenSpritesKeepObservationActiveAndRevealUsesTheCurrentAudioState()
    {
        var source = LoadSource(Path.Combine(AppContext.BaseDirectory, "Assets"), "hood");
        var definition = (PetDefinition)source.GetType().GetProperty("Definition")!.GetValue(source)!;
        var session = CreateIdleSession(definition, showSprites: false);
        var pet = CreateMetadataPet(session, source, new UnavailableMediaAudioProbe());
        pet.Opacity = 0;
        pet.ShowInTaskbar = false;
        var observer = typeof(HoodOverlayWindow).GetField("_mediaHeadphoneObserver", PrivateInstance)!;
        var band = (Image)pet.FindName("HeadphoneBandImage");
        var cups = (Image)pet.FindName("HeadphoneCupsImage");
        var audible = new MediaAudioObservation(true,
            [new MediaAudioSample("device:chrome", "chrome", 0.02f, false, 1, false, 1)]);

        try
        {
            pet.ApplyMediaHeadphones(true);
            pet.ApplyMediaAudioObservation(audible, TimeSpan.Zero);
            Assert.IsNotNull(observer.GetValue(pet));
            Assert.IsFalse(pet.IsVisible);
            Assert.IsNull(band.Source);
            Assert.IsNull(cups.Source);

            ApplyConfiguration(session, session.Configuration with
            {
                Behavior = session.Configuration.Behavior with { ShowSprites = true }
            });
            typeof(HoodOverlayWindow).GetMethod("EnsureSpriteAssetsLoaded", PrivateInstance)!.Invoke(pet, null);
            typeof(HoodOverlayWindow).GetMethod("RefreshBehaviorSettings", PrivateInstance)!.Invoke(pet, null);
            Assert.IsTrue(pet.IsVisible);
            Assert.IsNotNull(cups.Source, "Reveal uses the latest audible state.");
            var activeObserver = observer.GetValue(pet);

            ApplyConfiguration(session, session.Configuration with
            {
                Behavior = session.Configuration.Behavior with { ShowSprites = false }
            });
            typeof(HoodOverlayWindow).GetMethod("RefreshBehaviorSettings", PrivateInstance)!.Invoke(pet, null);
            Assert.IsNull(band.Source);
            Assert.IsNull(cups.Source);
            Assert.AreSame(activeObserver, observer.GetValue(pet), "Hiding art leaves audio observation active.");

            pet.ApplyMediaAudioObservation(audible, TimeSpan.FromMilliseconds(200));
            ApplyConfiguration(session, session.Configuration with
            {
                Behavior = session.Configuration.Behavior with { ShowSprites = true }
            });
            typeof(HoodOverlayWindow).GetMethod("EnsureSpriteAssetsLoaded", PrivateInstance)!.Invoke(pet, null);
            typeof(HoodOverlayWindow).GetMethod("RefreshBehaviorSettings", PrivateInstance)!.Invoke(pet, null);
            Assert.IsNotNull(cups.Source, "Sound observed while hidden appears when sprites are revealed.");
            Assert.AreSame(activeObserver, observer.GetValue(pet));
        }
        finally
        {
            pet.ApplyMediaHeadphones(false);
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

    private static HoodOverlayWindow CreatePet(HoodSession session, object assets, IMediaAudioProbe mediaAudioProbe)
    {
        Action noop = () => { };
        var constructor = typeof(HoodOverlayWindow).GetConstructors(PrivateInstance)
            .Single(candidate => candidate.GetParameters().Length == 11);
        var source = assets.GetType().GetProperty("Source")!.GetValue(assets);
        return (HoodOverlayWindow)constructor.Invoke(
            [session, source, assets, noop, noop, noop, noop, noop, noop, UiStyleCatalog.StickyNotes, mediaAudioProbe]);
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

    private static HoodOverlayWindow CreateMetadataPet(HoodSession session, object source, IMediaAudioProbe mediaAudioProbe)
    {
        Action noop = () => { };
        var constructor = typeof(HoodOverlayWindow).GetConstructors(PrivateInstance)
            .Single(candidate => candidate.GetParameters().Length == 11);
        return (HoodOverlayWindow)constructor.Invoke(
            [session, source, null, noop, noop, noop, noop, noop, noop, UiStyleCatalog.StickyNotes, mediaAudioProbe]);
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

    private sealed class UnavailableMediaAudioProbe : IMediaAudioProbe
    {
        public MediaAudioObservation Read() => MediaAudioObservation.Unavailable;
    }
}
