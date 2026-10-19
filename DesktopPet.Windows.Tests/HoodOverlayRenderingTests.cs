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
        var assets = LoadAssets(petName);
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
    [DataRow("hood", false)]
    [DataRow("pip", true)]
    [DataRow("crumb", false)]
    [DataRow("puddle", true)]
    public void ChecklistCelebrationPlaysOnceAndRestartsWithoutChangingNapNeedsOrFocus(string petName, bool focusBreak)
    {
        var assets = LoadAssets(petName);
        var session = CreateIdleSession(assets.Definition);
        session.Sleep();
        var needs = session.Needs;
        var pet = CreatePet(session, assets);
        var focus = new FocusSession();
        pet.AttachFocus(focus);
        if (focusBreak)
        {
            focus.StartFocus();
            focus.Advance(focus.Remaining);
            focus.TakeBreak();
        }
        var phase = focus.Phase;
        var remaining = focus.Remaining;
        var original = pet.CurrentPresentation;
        try
        {
            pet.CelebrateChecklistCompletion();
            var animation = assets.Definition.GetAnimation(PetAction.Pet);
            var image = (Image)pet.FindName("PetImage");
            Assert.AreEqual(PetAction.Pet, pet.CurrentPresentation.Action);
            Assert.AreEqual(assets.GetFrameRectangle(animation.Frames[0]), ((CroppedBitmap)image.Source).SourceRect);
            pet.AdvanceChecklistAnimation(TimeSpan.FromMilliseconds(600));
            Assert.IsGreaterThan(0, pet.CurrentPresentation.FrameIndex);
            pet.CelebrateChecklistCompletion();
            Assert.AreEqual(0, pet.CurrentPresentation.FrameIndex, "Repeated completions restart playback.");
            var duration = animation.FrameDurationMilliseconds * animation.Frames.Count;
            pet.AdvanceChecklistAnimation(TimeSpan.FromMilliseconds(duration - 1));
            Assert.AreEqual(animation.Frames.Count - 1, pet.CurrentPresentation.FrameIndex);
            pet.AdvanceChecklistAnimation(TimeSpan.FromMilliseconds(1));
            Assert.AreEqual(original, pet.CurrentPresentation);
            Assert.AreEqual(needs, session.Needs);
            Assert.AreEqual(HoodSessionMode.Sleeping, session.Mode);
            Assert.AreEqual(phase, focus.Phase);
            Assert.AreEqual(remaining, focus.Remaining);
        }
        finally { pet.Close(); }
    }

    [TestMethod]
    public void ChecklistCelebrationStartsTimingAtCompletionInsteadOfThePreviousIdleTick()
    {
        var assets = LoadAssets("hood");
        var session = CreateIdleSession(assets.Definition);
        var pet = CreatePet(session, assets);
        try
        {
            typeof(HoodOverlayWindow).GetField("_lastTick", PrivateInstance)!
                .SetValue(pet, Stopwatch.GetTimestamp() - Stopwatch.Frequency * 10);
            pet.CelebrateChecklistCompletion();
            typeof(HoodOverlayWindow).GetMethod("OnFrameTimerTick", PrivateInstance)!
                .Invoke(pet, [null, EventArgs.Empty]);
            Assert.AreEqual(PetAction.Pet, pet.CurrentPresentation.Action,
                "Time before the completion must not consume the celebration.");
        }
        finally { pet.Close(); }
    }

    [TestMethod]
    public void ChecklistCompletionWhileSpritesAreHiddenDoesNotQueueACelebration()
    {
        var assets = LoadAssets("hood");
        var session = CreateIdleSession(assets.Definition, showSprites: false);
        var pet = CreatePet(session, assets);
        try
        {
            pet.CelebrateChecklistCompletion();
            ApplyConfiguration(session, session.Configuration with
            {
                Behavior = session.Configuration.Behavior with { ShowSprites = true }
            });
            Assert.AreEqual(session.Presentation, pet.CurrentPresentation);
        }
        finally { pet.Close(); }
    }

    [TestMethod]
    public void RepeatedRectangleKeepsItsCropWhileTheSessionAndPetPositionAdvance()
    {
        var assets = LoadAssets("hood");
        var definition = assets.Definition;
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
        var assets = builtIn with { Source = builtIn.Source with { Definition = definition } };
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
                Position = new(500, 400),
                Action = action == PetAction.Write ? PetAction.Idle : action,
                FrameIndex = action == PetAction.Write ? 0 : index,
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
            var clock = new WritingClock();
            var pet = CreatePet(session, assets, new UnavailableMediaAudioProbe(), clock);
            if (action == PetAction.Write)
            {
                // Writing is transient input presentation, never a restorable session action.
                pet.ReportNoteWriting(pet, true);
                for (var frame = 0; frame < index; frame++)
                {
                    clock.Advance(TimeSpan.FromMilliseconds(animation.FrameDurationMilliseconds));
                    pet.ReportNoteWriting(pet, true);
                }
                reference.SetWritingPresentation(true);
                Assert.AreEqual(new HoodPresentation(action, index, session.Position), pet.CurrentPresentation);
            }
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
                if (action == PetAction.Write)
                {
                    clock.Advance(step);
                    pet.RefreshWritingPresentation();
                    Assert.AreEqual((index + 1) % animation.Frames.Count, pet.CurrentPresentation.FrameIndex);
                    Assert.AreEqual(PetAction.Write, pet.CurrentPresentation.Action);
                }
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
        var definition = assets.Definition;
        foreach (var petName in new[] { "hood", "pip", "crumb", "puddle" })
        {
            var petAssets = LoadAssets(petName);
            var petDefinition = petAssets.Definition;
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
        var definition = assets.Definition;
        var session = CreateIdleSession(definition);
        var pet = CreatePet(session, assets);
        var controller = new HoodConfigurationController(session,
            new YamlConfigurationStore(Path.Combine(Path.GetTempPath(), $"HoodMediaPreview-{Guid.NewGuid():N}.yaml")));
        var settings = new SettingsWindow(controller, pet, UiStyleCatalog.StickyNotes, null, "system", null);
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
        var definition = assets.Definition;
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
        var definition = source.Definition;
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
            pet.EnsureSpriteAssetsLoaded();
            pet.RefreshBehaviorSettings();
            Assert.IsTrue(pet.IsVisible);
            Assert.IsNotNull(cups.Source, "Reveal uses the latest audible state.");
            var activeObserver = observer.GetValue(pet);

            ApplyConfiguration(session, session.Configuration with
            {
                Behavior = session.Configuration.Behavior with { ShowSprites = false }
            });
            pet.RefreshBehaviorSettings();
            Assert.IsNull(band.Source);
            Assert.IsNull(cups.Source);
            Assert.AreSame(activeObserver, observer.GetValue(pet), "Hiding art leaves audio observation active.");

            pet.ApplyMediaAudioObservation(audible, TimeSpan.FromMilliseconds(200));
            ApplyConfiguration(session, session.Configuration with
            {
                Behavior = session.Configuration.Behavior with { ShowSprites = true }
            });
            pet.EnsureSpriteAssetsLoaded();
            pet.RefreshBehaviorSettings();
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
        var definition = assets.Definition;
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
            var replacementAssets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), "pip", definition);
            var replacementDefinition = replacementAssets.Definition;
            var presentation = session.Presentation;
            var replacementFrame = replacementDefinition.GetAnimation(presentation.Action).Frames[presentation.FrameIndex];
            var replacementRectangle = replacementAssets.GetFrameRectangle(replacementFrame);
            Assert.AreEqual(originalCrop.SourceRect, replacementRectangle, "The test requires equal crop coordinates across pets.");

            pet.ApplyPetAssets(replacementAssets);

            var replacementCrop = (CroppedBitmap)image.Source;
            var replacementSheet = replacementAssets.SpriteSheet;
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
        var definition = source.Definition;
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
            Assert.IsNull(pet.CurrentAssets, "Hidden startup should retain only the source metadata.");
            Assert.IsNull(image.Source);
            Assert.IsFalse(timer.IsEnabled);

            pet.EnsureSpriteAssetsLoaded();
            Assert.IsNotNull(pet.CurrentAssets);
            Assert.IsNull(image.Source, "Preloading for a reveal must not render before the setting changes.");

            ApplyConfiguration(session, session.Configuration with
            {
                Behavior = session.Configuration.Behavior with { ShowSprites = true }
            });
            pet.RefreshBehaviorSettings();
            Assert.IsTrue(pet.IsVisible);
            Assert.IsInstanceOfType<CroppedBitmap>(image.Source);
            Assert.IsTrue(timer.IsEnabled);

            ApplyConfiguration(session, session.Configuration with
            {
                Behavior = session.Configuration.Behavior with { ShowSprites = false }
            });
            pet.RefreshBehaviorSettings();

            Assert.IsFalse(pet.IsVisible);
            Assert.IsFalse(timer.IsEnabled);
            Assert.IsNull(image.Source);
            Assert.IsNull(pet.CurrentAssets, "The atlas must no longer be owned by the hidden overlay.");
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

        var definition = source.Definition;
        var session = CreateIdleSession(definition, showSprites: false);
        var pet = CreateMetadataPet(session, source);
        var image = (Image)pet.FindName("PetImage");
        var originalPresentation = session.Presentation;
        var originalNeeds = session.Needs;
        var originalPosition = session.Position;

        try
        {
            Assert.Throws<PetAssetException>(() => pet.EnsureSpriteAssetsLoaded());
            Assert.IsNull(pet.CurrentAssets);
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
        var definition = source.Definition;
        var session = CreateIdleSession(definition, showSprites: false);
        var pet = CreateMetadataPet(session, source);
        var configurationPath = Path.Combine(root, "config.yaml");
        var controller = new HoodConfigurationController(session, new YamlConfigurationStore(configurationPath));
        var settings = new SettingsWindow(controller, pet, UiStyleCatalog.StickyNotes, null, "system", null);

        try
        {
            ((CheckBox)settings.FindName("ShowSpritesCheckBox")!).IsChecked = true;
            typeof(SettingsWindow).GetMethod("OnSaveClick", PrivateInstance)!
                .Invoke(settings, [settings, new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)]);

            Assert.IsFalse(controller.Current.Behavior.ShowSprites);
            Assert.IsFalse(pet.IsVisible);
            Assert.IsNull(pet.CurrentAssets);
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
        var definition = source.Definition;
        var session = CreateIdleSession(definition, initiallyVisible);
        var pet = initiallyVisible ? CreatePet(session, LoadAssets("hood")) : CreateMetadataPet(session, source);
        pet.Opacity = 0;
        pet.ShowInTaskbar = false;
        var configPath = Path.Combine(Path.GetTempPath(), $"HoodRollbackVisibility-{Guid.NewGuid():N}.yaml");
        var controller = new HoodConfigurationController(session, new YamlConfigurationStore(configPath));
        var settings = new SettingsWindow(controller, pet, UiStyleCatalog.StickyNotes);
        try
        {
            ((ComboBox)settings.FindName("PetComboBox")).SelectedValue = PetCatalog.Pip;
            if (!initiallyVisible) pet.EnsureSpriteAssetsLoaded();
            controller.SaveAndApply("behavior.show_sprites", controller.Current with
            {
                Behavior = controller.Current.Behavior with { ShowSprites = !initiallyVisible }
            });
            pet.RefreshBehaviorSettings();
            settings.Close();

            Assert.AreEqual(!initiallyVisible, session.Configuration.Behavior.ShowSprites);
            Assert.AreEqual(!initiallyVisible, pet.IsVisible);
            Assert.AreEqual(!initiallyVisible, pet.CurrentAssets is not null);
            Assert.AreEqual(!initiallyVisible, ((Image)pet.FindName("PetImage")).Source is not null);
            var restoredSource = pet.CurrentSource;
            Assert.AreEqual(source.Directory,
                restoredSource.Directory);
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
        var definition = source.Definition;
        var session = CreateIdleSession(definition);
        var displays = WindowsDisplayProvider.GetDisplays();
        var displayScale = WindowsDisplayProvider.GetScaleForPet(session.Position, definition, displays);
        var originalAssets = HoodAssets.LoadAtScale(source, displayScale, displayScale);
        var originalSheet = originalAssets.SpriteSheet;
        var sourcePath = Path.Combine(directory, "hood_spritesheet.png");
        if (mutateBeforeSettings) File.WriteAllText(sourcePath, "changed before Settings opened");
        var pet = CreatePet(session, originalAssets);
        var controller = new HoodConfigurationController(session, new YamlConfigurationStore(Path.Combine(root, "config.yaml")));
        var settings = new SettingsWindow(controller, pet, UiStyleCatalog.StickyNotes, null, "system", null);
        var frame = definition.GetAnimation(session.Presentation.Action).Frames[session.Presentation.FrameIndex];
        var rectangle = originalAssets.GetFrameRectangle(frame);
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
                var preview = HoodAssets.LoadForDisplay(previewSource, displayScale, displayScale, 0.5);
                pet.ApplyPetAssets(preview);
                File.Delete(Path.Combine(previewDirectory, "hood_spritesheet.png"));
            }
            Assert.IsNull(typeof(SettingsWindow).GetField("_originalAssets", PrivateInstance)!.GetValue(settings),
                "The decoded original should be replaced by its compressed rollback snapshot.");
            var snapshot = (HoodAssetRollbackSnapshot)typeof(SettingsWindow).GetField("_originalAssetSnapshot", PrivateInstance)!.GetValue(settings)!;
            var snapshotBytes = snapshot.EncodedByteCount;
            var decodedBytes = (long)originalSheet.PixelWidth * originalSheet.PixelHeight * originalSheet.Format.BitsPerPixel / 8;
            Assert.IsTrue(snapshotBytes < decodedBytes, "The rollback image must use less memory than its decoded atlas.");

            if (deleteSource) File.Delete(sourcePath);
            else File.WriteAllText(sourcePath, "changed while Settings was previewing another pet");
            settings.Close();
            Assert.AreEqual(1d, session.PetScale, "Cancel must restore size even when preview artwork is unavailable.");

            var restored = pet.CurrentAssets!;
            var restoredSheet = restored.SpriteSheet;
            var restoredRectangle = restored.GetFrameRectangle(frame);
            Assert.AreEqual(originalSheet.PixelWidth, restoredSheet.PixelWidth);
            Assert.AreEqual(originalSheet.PixelHeight, restoredSheet.PixelHeight);
            CollectionAssert.AreEqual(expectedPixels, CopyPixels(restoredSheet, restoredRectangle));
            var currentSource = pet.CurrentSource;
            Assert.IsNotNull(currentSource.CachedSpriteSheet,
                "A changed or removed file must leave the original compressed source available for later DPI reloads.");

            pet.ReloadAssetsForDpi(1.25, 1.25);
            Assert.AreEqual(1.25, pet.CurrentAssets!.DpiScaleX);
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

    private static LoadedHoodAssets LoadAssets(string pet) =>
        HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), pet);

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

    private static HoodOverlayWindow CreatePet(HoodSession session, LoadedHoodAssets assets)
    {
        Action noop = () => { };
        return new HoodOverlayWindow(session, assets, noop, noop, noop, noop, noop, noop, UiStyleCatalog.StickyNotes);
    }

    private static HoodOverlayWindow CreatePet(HoodSession session, LoadedHoodAssets assets, IMediaAudioProbe mediaAudioProbe,
        TimeProvider? writingClock = null)
    {
        Action noop = () => { };
        var source = assets.Source;
        return new HoodOverlayWindow(session, source, assets, noop, noop, noop, noop, noop, noop, UiStyleCatalog.StickyNotes,
            mediaAudioProbe, writingClock);
    }

    private static HoodAssetSource LoadSource(string assetsDirectory, string pet) =>
        HoodAssets.LoadPetSource(assetsDirectory, pet);

    private static HoodOverlayWindow CreateMetadataPet(HoodSession session, HoodAssetSource source)
    {
        Action noop = () => { };
        return new HoodOverlayWindow(session, source, null, noop, noop, noop, noop, noop, noop, UiStyleCatalog.StickyNotes);
    }

    private static HoodOverlayWindow CreateMetadataPet(HoodSession session, HoodAssetSource source, IMediaAudioProbe mediaAudioProbe)
    {
        Action noop = () => { };
        return new HoodOverlayWindow(session, source, null, noop, noop, noop, noop, noop, noop, UiStyleCatalog.StickyNotes, mediaAudioProbe);
    }

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
        public void Save(HoodSessionSnapshot snapshot) { }
    }

    private sealed class WritingClock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance(TimeSpan elapsed) => _ticks += elapsed.Ticks;
    }

    private sealed class UnavailableMediaAudioProbe : IMediaAudioProbe
    {
        public MediaAudioObservation Read() => MediaAudioObservation.Unavailable;
    }
}
