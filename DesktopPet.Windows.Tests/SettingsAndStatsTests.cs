using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using DesktopPet.Core;
using DesktopPet.Windows;
using DrawingColor = System.Drawing.Color;
using Forms = System.Windows.Forms;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class SettingsAndStatsTests
{
    [TestMethod]
    public void TrayDoubleClickCreatesThenHidesAndRestoresNotes()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var notesPath = Path.Combine(Path.GetTempPath(), $"HoodTrayNotesTest-{Guid.NewGuid():N}.json");
        var notes = NotesApplication.Start(new JsonNotesStore(notesPath));
        var app = System.Windows.Application.Current as App ?? new TestApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var fields = new[] { "_notes", "_session", "_trayIconImage", "_effectiveNotesSettings" }
            .Select(name => typeof(App).GetField(name, flags)!).ToArray();
        var originalValues = fields.Select(field => field.GetValue(app)).ToArray();
        object?[] values = [notes, null, null, HoodConfiguration.Defaults.Notes with { AnimateShowHide = false }];
        for (var index = 0; index < fields.Length; index++) fields[index].SetValue(app, values[index]);
        var windows = (Dictionary<Guid, NoteWindow>)typeof(App).GetField("_noteWindows", flags)!.GetValue(app)!;
        Forms.NotifyIcon? icon = null;
        try
        {
            icon = (Forms.NotifyIcon)typeof(App).GetMethod("CreateTrayIcon", flags)!.Invoke(app, null)!;
            var doubleClick = typeof(Forms.NotifyIcon).GetMethod("OnDoubleClick", flags)!;
            doubleClick.Invoke(icon, [EventArgs.Empty]);
            var id = notes.Notes.Single().Id;
            var window = windows[id];
            Assert.IsTrue(notes.GroupVisible);
            Assert.IsTrue(window.IsVisible);

            doubleClick.Invoke(icon, [EventArgs.Empty]);
            Assert.IsFalse(notes.GroupVisible);
            Assert.IsFalse(window.IsVisible);
            CollectionAssert.AreEqual(new[] { id }, notes.OpenNoteIds.ToArray(), "Hiding must preserve open-note membership.");

            doubleClick.Invoke(icon, [EventArgs.Empty]);
            Assert.IsTrue(notes.GroupVisible);
            Assert.IsTrue(window.IsVisible);
            Assert.AreEqual(id, notes.Notes.Single().Id, "Showing again must reuse the existing note.");
            Assert.AreSame(window, windows[id]);
            Assert.IsNull(typeof(App).GetField("_settingsWindow", flags)!.GetValue(app));
        }
        finally
        {
            icon?.Dispose();
            (fields[2].GetValue(app) as System.Drawing.Icon)?.Dispose();
            foreach (var window in windows.Values.ToArray()) window.CloseWithoutMembershipChange();
            windows.Clear();
            for (var index = 0; index < fields.Length; index++) fields[index].SetValue(app, originalValues[index]);
            File.Delete(notesPath);
        }
    }

    [TestMethod]
    [DataRow(50d, false, false)]
    [DataRow(150d, false, false)]
    [DataRow(200d, false, false)]
    [DataRow(50d, true, false)]
    [DataRow(150d, true, false)]
    [DataRow(200d, true, false)]
    [DataRow(150d, false, true)]
    [DataRow(150d, true, true)]
    public void QuitRestoresUnsavedPetSizeBeforeSavingTheRestartPlacement(double percent, bool drag, bool cancelClose)
    {
        using var fixture = new Fixture("light");
        var pet = fixture.Pet();
        var settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
        var app = System.Windows.Application.Current as App ?? new TestApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var sessionField = typeof(App).GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var settingsField = typeof(App).GetField("_settingsWindow", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var originalSession = sessionField.GetValue(app);
        var originalSettings = settingsField.GetValue(app);
        sessionField.SetValue(app, fixture.Session);
        settingsField.SetValue(app, settings);
        settings.Closed += (_, _) => settingsField.SetValue(app, null);
        var expectedPosition = fixture.Session.Position;
        var expectedArea = fixture.Session.RoamingArea;
        try
        {
            ((Slider)settings.FindName("PetSizeSlider")).Value = percent;
            if (drag)
            {
                var previewPosition = fixture.Session.Position;
                fixture.Session.MoveTo(new(300, 300));
                expectedPosition = new(300 + (fixture.Session.PetWidth - 192) / 2, 300 + fixture.Session.PetHeight - 208);
                expectedArea = expectedArea with
                {
                    X = expectedArea.X + (300 - previewPosition.X),
                    Y = expectedArea.Y + (300 - previewPosition.Y)
                };
            }
            var prepareQuit = typeof(App).GetMethod("TrySaveSessionForQuit", BindingFlags.Instance | BindingFlags.NonPublic)!;
            if (cancelClose)
            {
                var beforeQuit = fixture.StateStore.Snapshot;
                System.ComponentModel.CancelEventHandler cancel = (_, e) => e.Cancel = true;
                settings.Closing += cancel;
                try
                {
                    Assert.AreEqual(false, prepareQuit.Invoke(app, null));
                    Assert.IsFalse(fixture.Session.QuitRequested);
                    Assert.AreSame(beforeQuit, fixture.StateStore.Snapshot);
                }
                finally { settings.Closing -= cancel; }
            }
            Assert.AreEqual(true, prepareQuit.Invoke(app, null));
            Assert.IsTrue(fixture.Session.QuitRequested);
            Assert.AreEqual(1d, fixture.Session.PetScale);
            Assert.AreEqual(expectedPosition, fixture.StateStore.Snapshot!.Position);
            Assert.AreEqual(expectedArea, fixture.StateStore.Snapshot.RoamingArea);
            var definition = (PetDefinition)fixture.Assets.GetType().GetProperty("Definition")!.GetValue(fixture.Assets)!;
            var restarted = HoodSession.Start(definition, [new("Primary", new PetBounds(0, 0, 1920, 1040), true)],
                new FixedRandomSource(), fixture.StateStore, fixture.Controller.Current, fixture.StateStore.Snapshot);
            Assert.AreEqual(expectedPosition, restarted.Position);
            Assert.AreEqual(expectedArea, restarted.RoamingArea);
        }
        finally
        {
            settings.Close(); pet.Close();
            sessionField.SetValue(app, originalSession);
            settingsField.SetValue(app, originalSettings);
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void PetSizeCancelPreservesIndependentPlacementChanges(bool editArea, bool previewAgain)
    {
        using var fixture = new Fixture("light");
        var pet = fixture.Pet();
        var settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
        try
        {
            var slider = (Slider)settings.FindName("PetSizeSlider");
            slider.Value = 150;
            if (editArea)
            {
                fixture.Session.ResizeRoamingArea(640, 500, persist: false);
                fixture.Session.MoveRoamingAreaBy(30, -100, persist: false);
            }
            else fixture.Session.MoveTo(new PetPosition(300, 300), persist: false);
            var center = fixture.Session.Position.X + fixture.Session.PetWidth / 2;
            var feet = fixture.Session.Position.Y + fixture.Session.PetHeight;
            if (previewAgain) slider.Value = 175;
            settings.Close();
            Assert.AreEqual(1d, fixture.Session.PetScale);
            Assert.AreEqual(center, fixture.Session.Position.X + fixture.Session.PetWidth / 2, 1e-9);
            Assert.AreEqual(feet, fixture.Session.Position.Y + fixture.Session.PetHeight, 1e-9);
            if (editArea)
            {
                Assert.AreEqual(640d, fixture.Session.RoamingArea.Width);
                Assert.AreEqual(500d, fixture.Session.RoamingArea.Height);
            }
        }
        finally { settings.Close(); pet.Close(); }
    }

    [TestMethod]
    [DataRow(PetCatalog.Hood, false)]
    [DataRow(PetCatalog.Pip, false)]
    [DataRow(PetCatalog.Crumb, false)]
    [DataRow(PetCatalog.Puddle, false)]
    [DataRow(PetCatalog.Hood, true)]
    [DataRow(PetCatalog.Pip, true)]
    [DataRow(PetCatalog.Crumb, true)]
    [DataRow(PetCatalog.Puddle, true)]
    public void PetSizePreviewsForEveryPetAndSaveOrCancelPreservesSleepingState(string choice, bool save)
    {
        using var fixture = new Fixture("light");
        fixture.Store.Save(fixture.Controller.Current);
        var originalYaml = File.ReadAllText(fixture.ConfigPath);
        fixture.Session.Sleep();
        fixture.Session.Advance(TimeSpan.FromMilliseconds(900));
        var position = fixture.Session.Position;
        var area = fixture.Session.RoamingArea;
        var presentation = fixture.Session.Presentation;
        var needs = fixture.Session.Needs;
        var nap = fixture.Session.RemainingNap;
        var pet = fixture.Pet();
        var settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
        try
        {
            var slider = (Slider)settings.FindName("PetSizeSlider");
            var image = (Image)pet.FindName("PetImage");
            Assert.AreEqual(100d, slider.Value);
            slider.Value = 200;
            slider.Value = 150;
            ((ComboBox)settings.FindName("PetComboBox")).SelectedValue = choice;
            Assert.AreEqual(288d, pet.Width);
            Assert.AreEqual(312d, pet.Height);
            Assert.AreEqual(pet.Width, image.Width);
            Assert.AreEqual(pet.Height, image.Height);
            Assert.AreEqual(1.5, fixture.Session.PetScale);
            Assert.AreEqual(1d, fixture.Controller.Current.PetScale);
            Assert.AreEqual(originalYaml, File.ReadAllText(fixture.ConfigPath));
            Assert.IsInstanceOfType<CroppedBitmap>(image.Source);

            typeof(SettingsWindow).GetMethod(save ? "OnSaveClick" : "OnCancelClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);

            Assert.AreEqual(save ? 1.5 : 1d, fixture.Store.LoadOrCreate().PetScale);
            Assert.AreEqual(save ? 1.5 : 1d, fixture.Session.PetScale);
            Assert.AreEqual(save ? 288d : 192d, pet.Width);
            Assert.AreEqual(presentation.Action, fixture.Session.Presentation.Action);
            Assert.AreEqual(presentation.FrameIndex, fixture.Session.Presentation.FrameIndex);
            Assert.AreEqual(needs, fixture.Session.Needs);
            Assert.AreEqual(nap, fixture.Session.RemainingNap);
            if (!save)
            {
                Assert.AreEqual(position, fixture.Session.Position);
                Assert.AreEqual(area, fixture.Session.RoamingArea);
            }
            var reopened = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
            try
            {
                Assert.AreEqual(save ? 150d : 100d, ((Slider)reopened.FindName("PetSizeSlider")).Value);
                var savedPosition = fixture.Session.Position;
                var savedArea = fixture.Session.RoamingArea;
                ((Slider)reopened.FindName("PetSizeSlider")).Value = 50;
                Assert.AreEqual(96d, pet.Width);
                Assert.AreEqual(104d, pet.Height);
                reopened.Close();
                Assert.AreEqual(save ? 1.5 : 1d, fixture.Session.PetScale);
                Assert.AreEqual(savedPosition, fixture.Session.Position);
                Assert.AreEqual(savedArea, fixture.Session.RoamingArea);
            }
            finally { reopened.Close(); }
        }
        finally { settings.Close(); pet.Close(); }
    }

    [TestMethod]
    public void PetSizeKeepsWindowDimensionsAndCropCoordinatesDuringDpiReload()
    {
        using var fixture = new Fixture("light");
        var pet = fixture.Pet();
        var settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
        try
        {
            ((Slider)settings.FindName("PetSizeSlider")).Value = 150;
            var firstSheet = ((CroppedBitmap)((Image)pet.FindName("PetImage")).Source).Source;
            ((Slider)settings.FindName("PetSizeSlider")).Value = 175;
            Assert.AreSame(firstSheet, ((CroppedBitmap)((Image)pet.FindName("PetImage")).Source).Source,
                "Growing again must reuse the atlas already decoded for the slider's maximum.");
            ((Slider)settings.FindName("PetSizeSlider")).Value = 50;
            typeof(HoodOverlayWindow).GetMethod("ReloadAssetsForDpi", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(pet, [1.5, 1.5]);
            Assert.AreEqual(96d, pet.Width);
            Assert.AreEqual(104d, pet.Height);
            var crop = (CroppedBitmap)((Image)pet.FindName("PetImage")).Source;
            Assert.AreEqual(2304, crop.Source.PixelWidth);
            Assert.AreEqual(1560, crop.Source.PixelHeight);
            Assert.AreEqual(ExpectedFrameRectangle(pet, fixture.Session), crop.SourceRect);
        }
        finally { settings.Close(); pet.Close(); }
    }

    [TestMethod]
    public void FailedPetSizeSaveRestoresSizePlacementAndControls()
    {
        using var fixture = new Fixture("light");
        fixture.Store.Save(fixture.Controller.Current);
        var yaml = File.ReadAllText(fixture.ConfigPath);
        var position = fixture.Session.Position;
        var area = fixture.Session.RoamingArea;
        var pet = fixture.Pet();
        var settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
        try
        {
            ((Slider)settings.FindName("PetSizeSlider")).Value = 200;
            using (var locked = new FileStream(fixture.ConfigPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                typeof(SettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);
            }
            Assert.AreEqual(yaml, File.ReadAllText(fixture.ConfigPath));
            Assert.AreEqual(1d, fixture.Controller.Current.PetScale);
            Assert.AreEqual(1d, fixture.Session.PetScale);
            Assert.AreEqual(192d, pet.Width);
            Assert.AreEqual(position, fixture.Session.Position);
            Assert.AreEqual(area, fixture.Session.RoamingArea);
            Assert.AreEqual(100d, ((Slider)settings.FindName("PetSizeSlider")).Value);
            StringAssert.Contains(((TextBlock)settings.FindName("AppearanceErrorText")).Text, "Settings were not saved");
        }
        finally { settings.Close(); pet.Close(); }
    }

    [TestMethod]
    public void HiddenPetSizePreviewStaysHiddenAndRevealUsesTheSavedSize()
    {
        using var fixture = new Fixture("light");
        fixture.Controller.SaveAndApply("behavior", fixture.Controller.Current with
        {
            Behavior = fixture.Controller.Current.Behavior with { ShowSprites = false }
        });
        var pet = fixture.Pet();
        var settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
        try
        {
            ((Slider)settings.FindName("PetSizeSlider")).Value = 50;
            Assert.IsFalse(pet.IsVisible);
            Assert.IsNull(((Image)pet.FindName("PetImage")).Source);
            Assert.IsNull(typeof(HoodOverlayWindow).GetProperty("CurrentAssets", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet));
            typeof(SettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);
            Assert.AreEqual(0.5, fixture.Store.LoadOrCreate().PetScale);
            typeof(HoodOverlayWindow).GetMethod("EnsureSpriteAssetsLoaded", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(pet, null);
            fixture.Controller.SaveAndApply("behavior", fixture.Controller.Current with
            {
                Behavior = fixture.Controller.Current.Behavior with { ShowSprites = true }
            });
            typeof(HoodOverlayWindow).GetMethod("RefreshBehaviorSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(pet, null);
            Assert.IsTrue(pet.IsVisible);
            Assert.AreEqual(96d, pet.Width);
            Assert.AreEqual(104d, pet.Height);
            Assert.IsInstanceOfType<CroppedBitmap>(((Image)pet.FindName("PetImage")).Source);
        }
        finally { settings.Close(); pet.Close(); }
    }

    [TestMethod]
    public void TrayNotesSubmenuTextFollowsThemeAndStyleChanges()
    {
        using var tray = new Forms.ContextMenuStrip();
        var notes = new Forms.ToolStripMenuItem("Notes");
        notes.DropDownItems.Add("New note");
        notes.DropDownItems.Add("Notes list");
        notes.DropDownItems.Add("Notes settings");
        tray.Items.Add(notes);
        var apply = typeof(App).Assembly.GetType("DesktopPet.Windows.TrayMenuAppearance")!
            .GetMethod("Apply", BindingFlags.Static | BindingFlags.Public)!;
        foreach (var style in UiStyleCatalog.Supported)
        foreach (var (theme, systemIsDark, dark) in new[]
        {
            ("dark", false, true), ("light", true, false), ("system", true, true), ("system", false, false)
        })
        {
            apply.Invoke(null, [tray, theme, style, systemIsDark]);
            Assert.IsTrue(dark ? tray.ForeColor.GetBrightness() > .8 : tray.ForeColor.GetBrightness() < .3);
            Assert.AreEqual(tray.ForeColor, notes.ForeColor);
            Assert.AreEqual(tray.ForeColor, notes.DropDown.ForeColor, $"Notes submenu: {theme}/{style}/{systemIsDark}");
            foreach (Forms.ToolStripItem item in notes.DropDownItems)
                Assert.AreEqual(tray.ForeColor, item.ForeColor, $"{item.Text}: {theme}/{style}/{systemIsDark}");
        }
    }

    [TestMethod]
    public void HiddenStartupCanEditRoamingAreaWithoutShowingOrAnimatingSprites()
    {
        using var fixture = new Fixture("light");
        fixture.Controller.SaveAndApply("behavior.show_sprites", fixture.Controller.Current with
        {
            Behavior = fixture.Controller.Current.Behavior with { ShowSprites = false }
        });
        var pet = fixture.Pet();
        var settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
        try
        {
            Assert.AreEqual(IntPtr.Zero, new System.Windows.Interop.WindowInteropHelper(pet).Handle);
            typeof(SettingsWindow).GetMethod("OnEditAreaClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);
            var editor = (Window)typeof(HoodOverlayWindow).GetField("_areaEditor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet)!;
            editor.Opacity = 0;
            Assert.IsTrue(editor.IsVisible);
            Assert.AreSame(pet, editor.Owner);
            Assert.IsTrue(fixture.Session.IsEditingArea);
            Assert.IsFalse(pet.IsVisible);
            Assert.IsNull(((Image)pet.FindName("PetImage")).Source);
            Assert.IsFalse(((System.Windows.Threading.DispatcherTimer)typeof(HoodOverlayWindow)
                .GetField("_frameTimer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet)!).IsEnabled);
            Assert.IsFalse(fixture.Store.LoadOrCreate().Behavior.ShowSprites);
            editor.Close();
            Assert.IsFalse(fixture.Session.IsEditingArea);
        }
        finally { settings.Close(); pet.Close(); }
    }

    [TestMethod]
    public void SpriteVisibilitySettingsSaveAndCancelPreserveOtherBehaviorChoices()
    {
        using var fixture = new Fixture("light");
        fixture.Store.Save(fixture.Controller.Current);
        var pet = fixture.Pet();
        pet.Opacity = 0;
        var settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
        try
        {
            ((CheckBox)settings.FindName("ShowSpritesCheckBox")).IsChecked = false;
            Assert.IsTrue(fixture.Controller.Current.Behavior.ShowSprites, "The checkbox takes effect only on Save.");
            settings.Close();
            Assert.IsTrue(fixture.Store.LoadOrCreate().Behavior.ShowSprites);
            settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
            ((CheckBox)settings.FindName("ShowSpritesCheckBox")).IsChecked = false;
            typeof(SettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);
            Assert.IsFalse(pet.IsVisible);
            Assert.IsFalse(fixture.Store.LoadOrCreate().Behavior.ShowSprites);
            Assert.IsTrue(fixture.Controller.Current.Behavior.IdleAnimations);
            Assert.IsTrue(fixture.Controller.Current.Behavior.Roaming);
            Assert.IsTrue(fixture.Controller.Current.Behavior.RandomEvents);
            settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
            Assert.IsFalse(((CheckBox)settings.FindName("ShowSpritesCheckBox")).IsChecked == true);
        }
        finally { settings.Close(); pet.Close(); }
    }

    [TestMethod]
    public void MediaHeadphonesSettingUsesTheBehaviorSaveAndCancelWorkflow()
    {
        using var fixture = new Fixture("light");
        fixture.Store.LoadOrCreate();
        var pet = fixture.Pet();
        SettingsWindow? settings = null;

        try
        {
            settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
            var checkBox = (CheckBox)settings.FindName("MediaHeadphonesCheckBox");
            Assert.AreEqual("Show headphones during audio playback", checkBox.Content);
            Assert.IsTrue(checkBox.IsChecked);
            checkBox.IsChecked = false;
            settings.Close();
            Assert.IsTrue(fixture.Controller.Current.Behavior.MediaHeadphones);
            Assert.IsTrue(fixture.Store.LoadOrCreate().Behavior.MediaHeadphones);

            settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
            ((CheckBox)settings.FindName("MediaHeadphonesCheckBox")).IsChecked = false;
            typeof(SettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);
            Assert.IsFalse(fixture.Controller.Current.Behavior.MediaHeadphones);
            Assert.IsFalse(fixture.Store.LoadOrCreate().Behavior.MediaHeadphones);

            settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
            Assert.IsFalse(((CheckBox)settings.FindName("MediaHeadphonesCheckBox")).IsChecked);
            settings.Close();
        }
        finally
        {
            settings?.Close();
            pet.Close();
        }
    }

    [TestMethod]
    public void PetAndTrayTogglesStopTimerRevealOpenNotesAndCancelPendingHide()
    {
        using var fixture = new Fixture("light");
        fixture.Store.Save(fixture.Controller.Current);
        var notesPath = fixture.ConfigPath + ".notes.json";
        var notes = NotesApplication.Start(new JsonNotesStore(notesPath));
        var openId = notes.CreateNote();
        var closedId = notes.CreateNote();
        Assert.IsTrue(notes.TryCloseNote(closedId, out _));
        notes.SetGroupVisible(false);
        var pet = fixture.Pet();
        var app = System.Windows.Application.Current as App ?? new TestApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var fields = new[] { "_session", "_notes", "_petWindow", "_configurationStore", "_trayIconImage", "_effectiveNotesSettings" }
            .Select(name => typeof(App).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!).ToArray();
        var originalValues = fields.Select(field => field.GetValue(app)).ToArray();
        object?[] values = [fixture.Session, notes, pet, fixture.Store, null, fixture.Controller.Current.Notes];
        for (var index = 0; index < fields.Length; index++) fields[index].SetValue(app, values[index]);
        var windows = (Dictionary<Guid, NoteWindow>)typeof(App).GetField("_noteWindows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app)!;
        var restore = typeof(App).GetMethod("RestoreOpenNotes", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var toggle = typeof(App).GetMethod("ToggleSprites", BindingFlags.Instance | BindingFlags.NonPublic)!;
        typeof(HoodOverlayWindow).GetEvent("ToggleSpritesRequested", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetAddMethod(true)!.Invoke(pet, [(Action)(() => toggle.Invoke(app, null))]);
        typeof(HoodOverlayWindow).GetEvent("SpritesVisibilityChanged", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetAddMethod(true)!.Invoke(pet, [(Action)(() => restore.Invoke(app, [false]))]);
        var timer = (System.Windows.Threading.DispatcherTimer)typeof(HoodOverlayWindow)
            .GetField("_frameTimer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet)!;
        Forms.NotifyIcon? icon = null;
        try
        {
            restore.Invoke(app, [false]);
            var note = windows[openId];
            note.Opacity = 0;
            ShowHidden(pet);
            Assert.IsTrue(timer.IsEnabled);
            var coordinator = (NotesVisibilityTransitionCoordinator)typeof(App)
                .GetField("_visibilityTransitions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app)!;
            var staleHide = coordinator.Request(false);
            note.BeginGroupVisibilityTransition(false, new Point(96, 96), TimeSpan.FromSeconds(10), () => { });
            var item = pet.ContextMenu!.Items.OfType<MenuItem>().Single(item => (string)item.Header == "Show Hood sprites");
            Assert.IsTrue(item.IsChecked);
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.AreSame(fixture.Session, fields[0].GetValue(app), "The app fixture must retain its session.");
            Assert.IsFalse(fixture.Session.Configuration.Behavior.ShowSprites, "The menu must apply the toggle.");
            Assert.IsFalse(pet.IsVisible);
            Assert.IsFalse(timer.IsEnabled, "The DispatcherTimer must be stopped, not merely render invisible frames.");
            Assert.IsFalse(item.IsChecked);
            Assert.IsFalse(fixture.Store.LoadOrCreate().Behavior.ShowSprites);
            Assert.IsTrue(note.IsVisible);
            Assert.IsTrue(notes.GroupVisible);
            Assert.IsFalse(windows.ContainsKey(closedId), "Closed notes must stay closed.");
            typeof(App).GetMethod("CompleteVisibilityTransition", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(app, [openId, staleHide]);
            Assert.IsTrue(note.IsVisible, "A stale hide completion cannot hide notes after sprites are hidden.");

            icon = (Forms.NotifyIcon)typeof(App).GetMethod("CreateTrayIcon", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(app, null)!;
            var trayToggle = icon.ContextMenuStrip!.Items.OfType<Forms.ToolStripMenuItem>().Single(item => item.Text == "Show Hood sprites");
            trayToggle.PerformClick();
            Assert.IsTrue(pet.IsVisible);
            Assert.IsTrue(timer.IsEnabled);
            Assert.IsTrue(fixture.Store.LoadOrCreate().Behavior.ShowSprites);
            Assert.IsTrue(note.IsVisible);

            trayToggle.PerformClick();
            Assert.IsFalse(timer.IsEnabled);

            var traySettingsItem = icon.ContextMenuStrip.Items.OfType<Forms.ToolStripMenuItem>()
                .Single(item => item.Text == "Settings");
            traySettingsItem.PerformClick();
            var hiddenSettings = (SettingsWindow)typeof(App).GetField("_settingsWindow", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(app)!;
            try
            {
                Assert.IsTrue(hiddenSettings.IsVisible, "Settings must remain available from the tray while sprites are hidden.");
                Assert.IsFalse(((CheckBox)hiddenSettings.FindName("ShowSpritesCheckBox")).IsChecked == true);
            }
            finally { hiddenSettings.Close(); }

            var trayNotes = icon.ContextMenuStrip.Items.OfType<Forms.ToolStripMenuItem>().Single(item => item.Text == "Notes");
            trayNotes.DropDownItems.OfType<Forms.ToolStripMenuItem>().Single(item => item.Text == "Notes settings").PerformClick();
            var hiddenNotesSettings = (NotesSettingsWindow)typeof(App).GetField("_notesSettingsWindow", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(app)!;
            try { Assert.IsTrue(hiddenNotesSettings.IsVisible, "Notes settings must remain usable while sprites are hidden."); }
            finally { hiddenNotesSettings.Close(); }

            trayNotes.DropDownItems.OfType<Forms.ToolStripMenuItem>().Single(item => item.Text == "Notes list").PerformClick();
            var hiddenNotesList = (NotesListWindow)typeof(App).GetField("_notesListWindow", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(app)!;
            try { Assert.IsTrue(hiddenNotesList.IsVisible, "The Notes list must remain available while sprites are hidden."); }
            finally { hiddenNotesList.Close(); }

            var hiddenPet = fixture.Pet();
            try
            {
                Assert.IsFalse(hiddenPet.IsVisible);
                Assert.IsNull(((Image)hiddenPet.FindName("PetImage")).Source, "Starting hidden must not render a sprite.");
                Assert.IsFalse(((System.Windows.Threading.DispatcherTimer)typeof(HoodOverlayWindow)
                    .GetField("_frameTimer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hiddenPet)!).IsEnabled);
            }
            finally { hiddenPet.Close(); }
            notes.SetGroupVisible(false);
            notes = NotesApplication.Start(new JsonNotesStore(notesPath));
            fields[1].SetValue(app, notes);
            restore.Invoke(app, [true]);
            Assert.IsTrue(note.IsVisible, "Restoration while hidden must reveal notes immediately even with animations enabled.");
            Assert.IsFalse((bool)typeof(NoteWindow).GetField("_isTransitioning", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(note)!,
                "No transition toward the hidden pet may remain active.");
            Assert.IsTrue(notes.TryCloseNote(openId, out _));
            restore.Invoke(app, [false]);
            Assert.IsFalse(note.IsVisible, "Individual notes may still be closed.");
        }
        finally
        {
            icon?.Dispose();
            (fields[4].GetValue(app) as System.Drawing.Icon)?.Dispose();
            foreach (var window in windows.Values.ToArray()) window.CloseWithoutMembershipChange();
            windows.Clear();
            for (var index = 0; index < fields.Length; index++) fields[index].SetValue(app, originalValues[index]);
            pet.Close();
            File.Delete(notesPath);
        }
    }

    [TestMethod]
    public void SelectionPreviewsUiStyleWithoutPersistingChanges()
    {
        using var fixture = new Fixture("light");
        fixture.Store.LoadOrCreate();
        var baseline = File.ReadAllText(fixture.ConfigPath);
        var pet = fixture.Pet();
        var settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
        try
        {
            ((ComboBox)settings.FindName("UiStyleComboBox")).SelectedValue = UiStyleCatalog.Minimal;

            Assert.AreEqual("#FF2878D0", ((SolidColorBrush)settings.Resources["AppAccentBrush"]).Color.ToString(),
                "Selecting a style should update the visible appearance immediately.");
            Assert.AreEqual(UiStyleCatalog.StickyNotes, fixture.Controller.Current.UiStyle);
            Assert.AreEqual(baseline, File.ReadAllText(fixture.ConfigPath));
        }
        finally { settings.Close(); pet.Close(); }
    }

    [TestMethod]
    public void SelectionPreviewsPetWithoutPersistingChanges()
    {
        using var fixture = new Fixture("light");
        fixture.Store.LoadOrCreate();
        var baseline = File.ReadAllText(fixture.ConfigPath);
        fixture.Session.Feed();
        fixture.Session.Advance(TimeSpan.FromMilliseconds(180));
        var presentation = fixture.Session.Presentation;
        var pet = fixture.Pet();
        var settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
        try
        {
            ((ComboBox)settings.FindName("PetComboBox")).SelectedValue = PetCatalog.Pip;

            AssertCurrentPetSource(pet, PetCatalog.Pip);
            Assert.AreEqual(presentation, fixture.Session.Presentation);
            Assert.AreEqual(PetCatalog.Hood, fixture.Controller.Current.Pet);
            Assert.AreEqual(baseline, File.ReadAllText(fixture.ConfigPath));
        }
        finally { settings.Close(); pet.Close(); }
    }

    [TestMethod]
    [DataRow("light", "#FFF4F6F9", "#FFFFFFFF", "#FFF9F9F9")]
    [DataRow("dark", "#FF202020", "#FF2D2D2D", "#FF2C2C2C")]
    public void HoodSettingsUsesTheSharedWindowAndCardPalette(string theme, string windowColor, string cardColor, string trayColor)
    {
        using var fixture = new Fixture(theme);
        var pet = fixture.Pet();
        var settings = (SettingsWindow)Activator.CreateInstance(typeof(SettingsWindow), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [fixture.Controller, pet], null)!;
        try
        {
            ShowHidden(settings);
            Assert.AreEqual((Color)ColorConverter.ConvertFromString(windowColor)!, ((SolidColorBrush)settings.Background).Color);
            var group = Descendants(settings).OfType<GroupBox>().First();
            Assert.AreEqual((Color)ColorConverter.ConvertFromString(cardColor)!, ((SolidColorBrush)group.Background).Color);
            Assert.AreEqual(theme == "dark" ? "#FFBDBDBD" : "#FF49515A",
                ((SolidColorBrush)((TextBlock)settings.FindName("DescriptionText")!).Foreground).Color.ToString());

            using var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Settings");
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Quit");
            typeof(App).Assembly.GetType("DesktopPet.Windows.TrayMenuAppearance")!
                .GetMethod("Apply", BindingFlags.Static | BindingFlags.Public)!.Invoke(null, [menu, theme, UiStyleCatalog.StickyNotes, null]);
            var expectedTray = (Color)ColorConverter.ConvertFromString(trayColor)!;
            Assert.AreEqual(DrawingColor.FromArgb(255, expectedTray.R, expectedTray.G, expectedTray.B), menu.BackColor);
            Assert.AreEqual(3, menu.Items.Count, "Appearance must preserve tray actions and separators.");
            Assert.IsInstanceOfType<Forms.ToolStripProfessionalRenderer>(menu.Renderer);
        }
        finally
        {
            settings.Close();
            pet.Close();
        }
    }

    [TestMethod]
    [DataRow(UiStyleCatalog.Minimal)]
    [DataRow(UiStyleCatalog.Soft)]
    [DataRow(UiStyleCatalog.Studio)]
    public void HoodSettingsPreviewsStyleThenSavesAndRestoresTheSavedChoice(string style)
    {
        using var fixture = new Fixture("light");
        fixture.Store.LoadOrCreate();
        var pet = fixture.Pet();
        var settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
        try
        {
            Assert.AreEqual(UiStyleCatalog.StickyNotes, ((ComboBox)settings.FindName("UiStyleComboBox")).SelectedValue);
            var selector = (ComboBox)settings.FindName("UiStyleComboBox");
            CollectionAssert.AreEqual(
                new[] { UiStyleCatalog.StickyNotes, UiStyleCatalog.Minimal, UiStyleCatalog.Soft, UiStyleCatalog.Studio },
                selector.Items.OfType<ComboBoxItem>().Select(item => (string)item.Tag).ToArray());
            var existingAccent = ((SolidColorBrush)settings.Resources["AppAccentBrush"]).Color;
            selector.SelectedValue = style;

            var previewAccent = ((SolidColorBrush)settings.Resources["AppAccentBrush"]).Color;
            Assert.AreNotEqual(existingAccent, previewAccent, "Selecting a style must preview it before Save.");
            Assert.AreEqual(UiStyleCatalog.StickyNotes, fixture.Store.LoadOrCreate().UiStyle);
            typeof(SettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);

            Assert.AreEqual(style, fixture.Controller.Current.UiStyle);
            Assert.AreEqual(style, fixture.Store.LoadOrCreate().UiStyle);
            Assert.AreEqual("yellow", fixture.Controller.Current.Notes.DefaultColor);
            Assert.AreEqual(previewAccent, ((SolidColorBrush)settings.Resources["AppAccentBrush"]).Color,
                "Saving must retain the preview instead of rolling it back on close.");

            var reopened = fixture.HoodSettings(pet, style);
            try
            {
                Assert.AreEqual(style, ((ComboBox)reopened.FindName("UiStyleComboBox")).SelectedValue);
                Assert.AreEqual(previewAccent, ((SolidColorBrush)reopened.Resources["AppAccentBrush"]).Color);
                ((ComboBox)reopened.FindName("UiStyleComboBox")).SelectedValue = UiStyleCatalog.StickyNotes;
                Assert.AreEqual(existingAccent, ((SolidColorBrush)reopened.Resources["AppAccentBrush"]).Color);
                typeof(SettingsWindow).GetMethod("OnCancelClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(reopened, [reopened, new RoutedEventArgs(Button.ClickEvent)]);
                Assert.AreEqual(style, fixture.Controller.Current.UiStyle,
                    "Cancel must leave the saved selection unchanged.");
                Assert.AreEqual(previewAccent, ((SolidColorBrush)reopened.Resources["AppAccentBrush"]).Color);
            }
            finally { reopened.Close(); }
        }
        finally { settings.Close(); pet.Close(); }
    }
    [TestMethod]
    [DataRow("sticky-notes", false, "#FF446DA5")]
    [DataRow("sticky-notes", true, "#FF91B8EE")]
    [DataRow("minimal", false, "#FF2878D0")]
    [DataRow("minimal", true, "#FF76B6FF")]
    [DataRow("soft", false, "#FF557F70")]
    [DataRow("soft", true, "#FF96C8B2")]
    [DataRow("studio", false, "#FF7652CE")]
    [DataRow("studio", true, "#FFBBA5FF")]
    public void SystemPaletteCanBeCheckedAtBothBrightnessResolutions(string style, bool systemIsDark, string accent)
    {
        var resolve = typeof(App).Assembly.GetType("DesktopPet.Windows.ApplicationAppearance")!
            .GetMethod("Resolve", BindingFlags.Static | BindingFlags.Public)!;
        var palette = resolve.Invoke(null, ["system", style, (bool?)systemIsDark])!;
        var actualAccent = (Color)palette.GetType().GetProperty("Accent")!.GetValue(palette)!;

        Assert.AreEqual((Color)ColorConverter.ConvertFromString(accent)!, actualAccent);
    }

    [TestMethod]
    [DataRow(UiStyleCatalog.Minimal)]
    [DataRow(UiStyleCatalog.Soft)]
    [DataRow(UiStyleCatalog.Studio)]
    public void HoodSettingsRefreshCallbackPreviewsBeforeSavingAndDoesNotRepeatOnSave(string style)
    {
        using var fixture = new Fixture("light");
        fixture.Store.LoadOrCreate();
        var pet = fixture.Pet();
        var refreshCount = 0;
        var effectiveStyle = UiStyleCatalog.StickyNotes;
        var settings = (SettingsWindow)Activator.CreateInstance(typeof(SettingsWindow), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [fixture.Controller, pet, effectiveStyle, (Action<string>)(selected =>
            {
                Assert.AreEqual(UiStyleCatalog.StickyNotes, fixture.Store.LoadOrCreate().UiStyle,
                    "A preview must happen before persistence.");
                effectiveStyle = selected;
                refreshCount++;
            })], null)!;
        try
        {
            var originalAccent = ((SolidColorBrush)settings.Resources["AppAccentBrush"]).Color;
            ((ComboBox)settings.FindName("UiStyleComboBox")).SelectedValue = style;
            Assert.AreNotEqual(originalAccent, ((SolidColorBrush)settings.Resources["AppAccentBrush"]).Color);
            Assert.AreEqual(1, refreshCount);
            Assert.AreEqual(style, effectiveStyle);
            Assert.AreEqual(UiStyleCatalog.StickyNotes, fixture.Controller.Current.UiStyle);
            typeof(SettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);

            Assert.AreEqual(1, refreshCount);
            Assert.AreEqual(style, effectiveStyle);
            Assert.AreEqual(style, fixture.Controller.Current.UiStyle);
        }
        finally { settings.Close(); pet.Close(); }
    }
    [TestMethod]
    [DataRow(UiStyleCatalog.Minimal)]
    [DataRow(UiStyleCatalog.Soft)]
    [DataRow(UiStyleCatalog.Studio)]
    public void HoodSettingsReportsPreviewFailureWithoutSavingOrChangingTheChoice(string style)
    {
        using var fixture = new Fixture("light");
        fixture.Store.LoadOrCreate();
        var pet = fixture.Pet();
        var settings = (SettingsWindow)Activator.CreateInstance(typeof(SettingsWindow), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [fixture.Controller, pet, UiStyleCatalog.StickyNotes,
                (Action<string>)(_ => throw new InvalidOperationException("simulated appearance failure"))], null)!;
        try
        {
            ((ComboBox)settings.FindName("UiStyleComboBox")).SelectedValue = style;

            Assert.AreEqual(UiStyleCatalog.StickyNotes, fixture.Store.LoadOrCreate().UiStyle);
            Assert.AreEqual(UiStyleCatalog.StickyNotes, fixture.Controller.Current.UiStyle);
            Assert.AreEqual(UiStyleCatalog.StickyNotes, ((ComboBox)settings.FindName("UiStyleComboBox")).SelectedValue);
            var error = (TextBlock)settings.FindName("AppearanceErrorText");
            Assert.AreEqual(Visibility.Visible, error.Visibility);
            StringAssert.Contains(error.Text, "Could not preview");
            StringAssert.Contains(error.Text, "Settings were not saved");
        }
        finally { settings.Close(); pet.Close(); }
    }
    [TestMethod]
    public void RepeatedStyleSwitchesReplacePaletteAndGeometryTokens()
    {
        using var fixture = new Fixture("light");
        var pet = fixture.Pet();
        var settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
        using var tray = new Forms.ContextMenuStrip();
        var applyTray = typeof(App).Assembly.GetType("DesktopPet.Windows.TrayMenuAppearance")!
            .GetMethod("Apply", BindingFlags.Static | BindingFlags.Public)!;
        try
        {
            settings.ApplyTheme("light", UiStyleCatalog.Minimal);
            applyTray.Invoke(null, [tray, "light", UiStyleCatalog.Minimal, null]);
            Assert.AreEqual(new CornerRadius(4), (CornerRadius)settings.Resources["AppWindowRadius"]);
            Assert.AreEqual((Color)ColorConverter.ConvertFromString("#FF2878D0")!,
                ((SolidColorBrush)settings.Resources["AppAccentBrush"]).Color);

            settings.ApplyTheme("dark", UiStyleCatalog.Soft);
            applyTray.Invoke(null, [tray, "dark", UiStyleCatalog.Soft, null]);
            Assert.AreEqual(new CornerRadius(12), (CornerRadius)settings.Resources["AppWindowRadius"]);
            Assert.AreEqual((Color)ColorConverter.ConvertFromString("#FF96C8B2")!,
                ((SolidColorBrush)settings.Resources["AppAccentBrush"]).Color);
            Assert.AreEqual(DrawingColor.FromArgb(255, 48, 46, 41), tray.BackColor);

            settings.ApplyTheme("dark", UiStyleCatalog.Studio);
            applyTray.Invoke(null, [tray, "dark", UiStyleCatalog.Studio, null]);
            Assert.AreEqual(new CornerRadius(5), (CornerRadius)settings.Resources["AppWindowRadius"]);
            Assert.AreEqual((Color)ColorConverter.ConvertFromString("#FFBBA5FF")!,
                ((SolidColorBrush)settings.Resources["AppAccentBrush"]).Color);
            Assert.AreEqual(DrawingColor.FromArgb(255, 39, 38, 51), tray.BackColor);

            settings.ApplyTheme("light", UiStyleCatalog.StickyNotes);
            applyTray.Invoke(null, [tray, "light", UiStyleCatalog.StickyNotes, null]);
            Assert.AreEqual(new CornerRadius(8), (CornerRadius)settings.Resources["AppWindowRadius"]);
            Assert.AreEqual((Color)ColorConverter.ConvertFromString("#FF446DA5")!,
                ((SolidColorBrush)settings.Resources["AppAccentBrush"]).Color);
            Assert.AreEqual(DrawingColor.FromArgb(255, 249, 249, 249), tray.BackColor);
        }
        finally { settings.Close(); pet.Close(); }
    }
    [TestMethod]
    public void NotesSettingsDistinguishesSavedDataFromFailedAppearanceRefresh()
    {
        using var fixture = new Fixture("light");
        fixture.Store.LoadOrCreate();
        var original = fixture.Controller.Current.Notes;
        NotesSettingsWindow? settings = null;
        settings = fixture.Settings(candidate =>
        {
            if (fixture.Controller.Current.Notes != original)
            {
                settings!.ApplyEffectiveSettings(original);
                throw new InvalidOperationException("simulated appearance failure");
            }
        });
        try
        {
            ((Slider)settings.FindName("TextSizeSlider")).Value = 24;
            typeof(NotesSettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);

            Assert.AreEqual(24, fixture.Store.LoadOrCreate().Notes.TextSize);
            Assert.AreEqual(24, fixture.Controller.Current.Notes.TextSize);
            Assert.AreEqual(Visibility.Visible, ((TextBlock)settings.FindName("AppearanceNoticeText")).Visibility);
            var error = (TextBlock)settings.FindName("ErrorMessage");
            Assert.AreEqual(Visibility.Visible, error.Visibility);
            StringAssert.Contains(error.Text, "settings were saved");
            StringAssert.Contains(error.Text, "Restart Hood");
            Assert.IsFalse(error.Text.Contains("not saved", StringComparison.OrdinalIgnoreCase));
        }
        finally { settings.Close(); }
    }

    [TestMethod]
    public void RepeatedStyleAndThemeRefreshPreservesActiveNoteEditingState()
    {
        var note = new NoteWindow(
            new HoodNote { Document = NotesDocument.FromText("Initial note") },
            (_, _) => null, (_, _) => null, (_, _) => null, (_, _) => (null, null), _ => (true, null),
            () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null,
            new NotesConfiguration { Theme = "light", TextSize = 18 });
        try
        {
            ShowHidden(note);
            var editor = (RichTextBox)note.FindName("Editor");
            var document = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd);
            document.Text = string.Join("\n", Enumerable.Range(0, 40).Select(index => $"Preserve formatted note line {index}"));
            document.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
            var selectionStart = editor.Document.ContentStart.GetPositionAtOffset(2, LogicalDirection.Forward)!;
            var selectionEnd = editor.Document.ContentStart.GetPositionAtOffset(12, LogicalDirection.Forward)!;
            editor.Selection.Select(selectionStart, selectionEnd);
            var beforeText = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;
            var beforeWeight = document.GetPropertyValue(TextElement.FontWeightProperty);
            var beforeSelection = editor.Selection.Text;
            var beforeStart = editor.Document.ContentStart.GetOffsetToPosition(editor.Selection.Start);
            var beforeEnd = editor.Document.ContentStart.GetOffsetToPosition(editor.Selection.End);
            var beforeBounds = (note.Left, note.Top, note.Width, note.Height);
            var beforeTextSize = editor.Document.FontSize;
            var beforeTopmost = note.Topmost;
            var scrollViewer = Descendants(editor).OfType<ScrollViewer>().First();
            scrollViewer.ScrollToVerticalOffset(120);
            note.UpdateLayout();
            var beforeScroll = scrollViewer.VerticalOffset;
            editor.Focus();
            note.UpdateLayout();
            var beforeFocus = Keyboard.FocusedElement;
            var beforeToolbarVisibility = ((StackPanel)note.FindName("FormattingToolbar")).Visibility;

            note.ApplyAppearance(new NotesConfiguration { Theme = "dark", TextSize = 18 }, NoteColor.Lavender, UiStyleCatalog.Minimal);
            note.ApplyAppearance(new NotesConfiguration { Theme = "light", TextSize = 18 }, NoteColor.Lavender, UiStyleCatalog.Soft);
            note.ApplyAppearance(new NotesConfiguration { Theme = "dark", TextSize = 18 }, NoteColor.Lavender, UiStyleCatalog.Studio);
            note.ApplyAppearance(new NotesConfiguration { Theme = "light", TextSize = 18 }, NoteColor.Lavender, UiStyleCatalog.StickyNotes);
            note.ApplyAppearance(new NotesConfiguration { Theme = "dark", TextSize = 18 }, NoteColor.Lavender, UiStyleCatalog.Minimal);

            Assert.AreEqual(beforeText, new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
            Assert.AreEqual(beforeWeight, new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd)
                .GetPropertyValue(TextElement.FontWeightProperty));
            Assert.AreEqual(beforeSelection, editor.Selection.Text);
            Assert.AreEqual(beforeStart, editor.Document.ContentStart.GetOffsetToPosition(editor.Selection.Start));
            Assert.AreEqual(beforeEnd, editor.Document.ContentStart.GetOffsetToPosition(editor.Selection.End));
            Assert.AreEqual(beforeTextSize, editor.Document.FontSize, "An unchanged text-size setting must not touch the document.");
            Assert.AreEqual(beforeBounds, (note.Left, note.Top, note.Width, note.Height));
            note.UpdateLayout();
            Assert.AreEqual(beforeScroll, Descendants(editor).OfType<ScrollViewer>().First().VerticalOffset);
            Assert.AreEqual(beforeFocus, Keyboard.FocusedElement);
            Assert.AreEqual(beforeToolbarVisibility, ((StackPanel)note.FindName("FormattingToolbar")).Visibility);
            Assert.AreEqual(beforeTopmost, note.Topmost);
            Assert.IsTrue(note.IsVisible);
            Assert.AreEqual((Color)ColorConverter.ConvertFromString(NoteColorPalette.GetBackgroundHex(NoteColor.Lavender, true, UiStyleCatalog.Minimal))!,
                ((SolidColorBrush)note.Background).Color);
            Assert.AreEqual(new CornerRadius(4), WindowChrome.GetWindowChrome(note)!.CornerRadius);
            Assert.AreEqual(new CornerRadius(3, 3, 0, 0), ((Border)note.FindName("NoteHeader")).CornerRadius);
        }
        finally { note.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(UiStyleCatalog.StickyNotes, 8.0, 7.0, 14.0)]
    [DataRow(UiStyleCatalog.Minimal, 4.0, 3.0, 13.0)]
    [DataRow(UiStyleCatalog.Soft, 12.0, 10.0, 15.0)]
    [DataRow(UiStyleCatalog.Studio, 5.0, 5.0, 14.0)]
    public void PreservesTheSelectedNoteColorInDarkMode(
        string style, double windowRadius, double headerRadius, double titleSize)
    {
        var note = new NoteWindow(
            new HoodNote { Document = NotesDocument.FromText("Style"), Color = NoteColor.Lavender },
            (_, _) => null, (_, _) => null, (_, _) => null, (_, _) => (null, null), _ => (true, null),
            () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null,
            new NotesConfiguration { Theme = "dark" }, style);
        try
        {
            Assert.AreEqual((Color)ColorConverter.ConvertFromString(NoteColorPalette.GetBackgroundHex(NoteColor.Lavender, true, style))!,
                ((SolidColorBrush)note.Background).Color);
            Assert.AreEqual(new CornerRadius(windowRadius), WindowChrome.GetWindowChrome(note)!.CornerRadius);
            Assert.AreEqual(titleSize, ((TextBox)note.FindName("NoteTitle")).FontSize);
            Assert.AreEqual(style == UiStyleCatalog.Soft ? FontWeights.Medium : FontWeights.SemiBold,
                ((TextBox)note.FindName("NoteTitle")).FontWeight);
            Assert.AreEqual(new CornerRadius(headerRadius, headerRadius, 0, 0),
                ((Border)note.FindName("NoteHeader")).CornerRadius);
        }
        finally { note.CloseWithoutMembershipChange(); }
    }
    [TestMethod]
    [DataRow("sticky-notes", "light", "#FF446DA5", "#FFFFFFFF", "#FFF9F9F9", "#D9F9F9F9", 8.0, 20.0)]
    [DataRow("sticky-notes", "dark", "#FF91B8EE", "#FF2D2D2D", "#FF2C2C2C", "#D92C2C2C", 8.0, 20.0)]
    [DataRow("minimal", "light", "#FF2878D0", "#FFFFFFFF", "#FFF9FAFC", "#FFF9FAFC", 4.0, 14.0)]
    [DataRow("minimal", "dark", "#FF76B6FF", "#FF222A35", "#FF222A35", "#FF222A35", 4.0, 14.0)]
    [DataRow("soft", "light", "#FF557F70", "#FFFFFCF7", "#FFFFFCF7", "#FFFFFCF7", 12.0, 22.0)]
    [DataRow("soft", "dark", "#FF96C8B2", "#FF35332E", "#FF302E29", "#FF302E29", 12.0, 22.0)]
    [DataRow("studio", "light", "#FF7652CE", "#FFFFFFFF", "#FFFCFCFE", "#FFFCFCFE", 5.0, 20.0)]
    [DataRow("studio", "dark", "#FFBBA5FF", "#FF292835", "#FF272633", "#FF272633", 5.0, 20.0)]
    public void RestartedStyleReachesAllSettingsMenusListsAndTray(
        string style, string theme, string accentHex, string cardHex, string trayHex, string menuHex, double radius, double padding)
    {
        using var fixture = new Fixture(theme, style);
        fixture.Store.Save(fixture.Controller.Current);
        var pet = fixture.Pet(style);
        var note = new NoteWindow(
            new HoodNote { Document = NotesDocument.FromText("Styled note"), Color = NoteColor.Lavender },
            (_, _) => null, (_, _) => null, (_, _) => null, (_, _) => (null, null), _ => (true, null),
            () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null,
            new NotesConfiguration { Theme = theme }, style);
        var hoodSettings = fixture.HoodSettings(pet, style);
        var notesSettings = (NotesSettingsWindow)Activator.CreateInstance(typeof(NotesSettingsWindow),
            BindingFlags.Instance | BindingFlags.NonPublic, null, [fixture.Controller, (Action<NotesConfiguration>)(_ => { }), style], null)!;
        var notesList = new NotesListWindow(
            _ => Array.Empty<NoteListItem>(), _ => Array.Empty<TrashedNoteListItem>(), _ => { }, () => { },
            _ => null, _ => null, () => null, theme, uiStyle: style);
        using var tray = new Forms.ContextMenuStrip();
        tray.Items.Add("Settings");
        typeof(App).Assembly.GetType("DesktopPet.Windows.TrayMenuAppearance")!
            .GetMethod("Apply", BindingFlags.Static | BindingFlags.Public)!
            .Invoke(null, [tray, theme, style, null]);
        try
        {
            var expectedAccent = (Color)ColorConverter.ConvertFromString(accentHex)!;
            var expectedHeadingSize = style == UiStyleCatalog.Minimal ? 22.0 : 24.0;
            var expectedHeadingWeight = style switch
            {
                UiStyleCatalog.Soft => FontWeights.Medium,
                UiStyleCatalog.Studio => FontWeights.Bold,
                _ => FontWeights.SemiBold
            };
            var expectedMenu = (Color)ColorConverter.ConvertFromString(menuHex)!;
            foreach (var surface in new FrameworkElement[] { hoodSettings, notesSettings, notesList, pet.ContextMenu!, note })
            {
                Assert.AreEqual(expectedAccent, ((SolidColorBrush)surface.Resources["AppAccentBrush"]).Color);
                Assert.AreEqual(expectedMenu, ((SolidColorBrush)surface.Resources["NotesMenuBrush"]).Color);
                foreach (var popupResource in new[] { "FlyoutBackground", "ContextMenuBackground", "ComboBoxDropDownBackground" })
                    Assert.AreEqual(expectedMenu, ((SolidColorBrush)surface.Resources[popupResource]).Color, popupResource);
                Assert.AreEqual(new CornerRadius(radius), (CornerRadius)surface.Resources["AppWindowRadius"]);
                Assert.AreEqual(new Thickness(padding), (Thickness)surface.Resources["AppWindowPadding"]);
                Assert.AreEqual(14.0, ((Control)surface).FontSize);
                Assert.AreEqual("Segoe UI Variable, Segoe UI", ((Control)surface).FontFamily.Source);
                Assert.AreEqual(expectedHeadingSize, surface.Resources["AppSettingsHeadingFontSize"]);
            }
            var expectedListTitleSize = style switch
            {
                UiStyleCatalog.Minimal => 13.0,
                UiStyleCatalog.Soft => 15.0,
                _ => 14.0
            };
            var expectedListTitleWeight = style == UiStyleCatalog.Soft ? FontWeights.Medium : FontWeights.SemiBold;
            Assert.AreEqual(expectedListTitleSize, notesList.Resources["AppListTitleFontSize"]);
            Assert.AreEqual(expectedListTitleWeight, notesList.Resources["AppListTitleFontWeight"]);
            Assert.AreEqual(expectedHeadingSize, ((TextBlock)hoodSettings.FindName("SettingsHeadingText")).FontSize);
            Assert.AreEqual(expectedHeadingWeight, ((TextBlock)hoodSettings.FindName("SettingsHeadingText")).FontWeight);
            Assert.AreEqual(expectedHeadingSize, ((TextBlock)notesSettings.FindName("NotesSettingsHeadingText")).FontSize);
            Assert.AreEqual(expectedHeadingWeight, ((TextBlock)notesSettings.FindName("NotesSettingsHeadingText")).FontWeight);
            Assert.AreEqual(expectedHeadingSize, ((TextBlock)notesList.FindName("NotesListHeadingText")).FontSize);
            Assert.AreEqual(expectedHeadingWeight, ((TextBlock)notesList.FindName("NotesListHeadingText")).FontWeight);
            Assert.AreEqual(style, ((ComboBox)hoodSettings.FindName("UiStyleComboBox")).SelectedValue);
            Assert.AreEqual(Visibility.Collapsed, ((TextBlock)hoodSettings.FindName("AppearanceErrorText")).Visibility);
            Assert.AreEqual((Color)ColorConverter.ConvertFromString(cardHex)!,
                ((SolidColorBrush)notesList.Resources["AppCardBrush"]).Color);
            var expectedTray = (Color)ColorConverter.ConvertFromString(trayHex)!;
            Assert.AreEqual(DrawingColor.FromArgb(expectedTray.A, expectedTray.R, expectedTray.G, expectedTray.B), tray.BackColor);
        }
        finally
        {
            hoodSettings.Close();
            notesSettings.Close();
            notesList.Close();
            note.CloseWithoutMembershipChange();
            pet.Close();
        }
    }
    [TestMethod]
    public void NoteWindowChromeUsesTheSharedCornerRadius()
    {
        var window = new NoteWindow(
            new HoodNote { Document = NotesDocument.FromText("Style") },
            (_, _) => null, (_, _) => null, (_, _) => null, (_, _) => (null, null), _ => (true, null),
            () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null, new NotesConfiguration());
        try
        {
            Assert.AreEqual((CornerRadius)window.Resources["AppWindowRadius"],
                WindowChrome.GetWindowChrome(window)!.CornerRadius);
        }
        finally
        {
            window.CloseWithoutMembershipChange();
        }
    }

    [TestMethod]
    [DataRow("sticky-notes", "light")]
    [DataRow("sticky-notes", "dark")]
    [DataRow("minimal", "light")]
    [DataRow("minimal", "dark")]
    [DataRow("soft", "light")]
    [DataRow("soft", "dark")]
    [DataRow("studio", "light")]
    [DataRow("studio", "dark")]
    public void HoodMenuNotesTextAlignsWithOtherItems(string style, string theme)
    {
        using var fixture = new Fixture(theme, style);
        var hood = fixture.Pet(style);
        var menu = hood.ContextMenu!;
        try
        {
            menu.Opacity = 0;
            menu.Placement = PlacementMode.Absolute;
            menu.HorizontalOffset = menu.VerticalOffset = -10000;
            menu.IsOpen = true;
            menu.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var notes = menu.Items.OfType<MenuItem>().Single(item => (string)item.Header == "_Notes");
            var feed = menu.Items.OfType<MenuItem>().Single(item => (string)item.Header == "Feed");
            var notesText = Descendants(notes).OfType<ContentPresenter>().Single(presenter => Equals(presenter.Content, notes.Header));
            var feedText = Descendants(feed).OfType<ContentPresenter>().Single(presenter => Equals(presenter.Content, feed.Header));
            Assert.AreEqual(feedText.TransformToAncestor(menu).Transform(new Point()).X,
                notesText.TransformToAncestor(menu).Transform(new Point()).X, 0.1,
                "The Notes submenu header must use the same text indentation as ordinary menu items.");
        }
        finally { menu.IsOpen = false; hood.Close(); }
    }

    [TestMethod]
    [DataRow("sticky-notes", "light")]
    [DataRow("sticky-notes", "dark")]
    [DataRow("minimal", "light")]
    [DataRow("minimal", "dark")]
    [DataRow("soft", "light")]
    [DataRow("soft", "dark")]
    [DataRow("studio", "light")]
    [DataRow("studio", "dark")]
    public void OpeningHoodMenuShowsCurrentStats(string style, string theme)
    {
        using var fixture = new Fixture(theme, style);
        var hood = fixture.Pet(style);
        var menu = hood.ContextMenu!;
        try
        {
            menu.Opacity = 0;
            menu.Placement = PlacementMode.Absolute;
            menu.HorizontalOffset = menu.VerticalOffset = -10000;
            menu.IsOpen = true;
            menu.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var stats = menu.Items.OfType<MenuItem>().Skip(1).Take(3).ToArray();
            var names = new[] { "Hunger:", "Boredom:", "Sleep:" };
            for (var index = 0; index < stats.Length; index++)
            {
                StringAssert.StartsWith(stats[index].Header as string ?? string.Empty, names[index]);
                Assert.IsFalse(stats[index].IsEnabled, "Stats must remain informational.");
                var text = Descendants(stats[index]).OfType<TextBlock>().First(block => block.Text.Contains(names[index]));
                Assert.IsInstanceOfType<SolidColorBrush>(text.Foreground);
                Assert.IsTrue(((SolidColorBrush)text.Foreground).Color.A >= 200, "Stats text must stay readable when disabled.");
            }
            menu.IsOpen = false;
            fixture.Session.Advance(TimeSpan.FromMinutes(2));
            menu.IsOpen = true;
            StringAssert.Contains(stats[0].Header as string ?? string.Empty, $"{fixture.Session.Needs.Hunger:0.#}/100");
        }
        finally { menu.IsOpen = false; hood.Close(); }
    }

    [TestMethod]
    [DataRow("light")]
    [DataRow("dark")]
    [DataRow("system")]
    public void SettingsCheckboxesOutlineTheBoxInsteadOfTheLabel(string theme)
    {
        using var fixture = new Fixture(theme);
        var window = fixture.Settings();
        try
        {
            ShowHidden(window);
            foreach (var name in new[] { "AlwaysOnTopCheckBox", "AnimateShowHideCheckBox" })
            {
                var checkbox = (CheckBox)window.FindName(name);
                foreach (var value in new[] { false, true })
                {
                    checkbox.IsChecked = value;
                    window.UpdateLayout();
                    var borders = Descendants(checkbox).OfType<Border>().Where(border =>
                        border.BorderThickness.Left > 0 && border.BorderBrush is SolidColorBrush brush && brush.Color.A > 0).ToArray();
                    Assert.IsTrue(borders.Any(border => border.ActualWidth is >= 16 and <= 24 && border.ActualHeight is >= 16 and <= 24),
                        "The checkbox square needs a visible outline.");
                    Assert.IsFalse(borders.Any(border => border.ActualWidth > 28), "The outline must not surround the label.");
                }
            }
        }
        finally { window.Close(); }
    }

    [TestMethod]
    [DataRow("12", true)]
    [DataRow("27", true)]
    [DataRow("32", true)]
    [DataRow("11", false)]
    [DataRow("33", false)]
    [DataRow("20.5", false)]
    [DataRow("abc", false)]
    [DataRow("", false)]
    public void TypedTextSizeSavesOnlyWholeNumbersInTheExistingRange(string text, bool valid)
    {
        using var fixture = new Fixture("dark");
        var window = fixture.Settings();
        try
        {
            var input = window.FindName("TextSizeInput") as TextBox;
            Assert.IsNotNull(input, "Text size needs an editable field.");
            input.Text = text;
            typeof(NotesSettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, [window, new RoutedEventArgs(Button.ClickEvent)]);
            if (valid)
            {
                Assert.AreEqual(int.Parse(text), fixture.Controller.Current.Notes.TextSize);
                Assert.AreEqual(int.Parse(text), fixture.Store.LoadOrCreate().Notes.TextSize);
            }
            else
            {
                Assert.AreEqual(16, fixture.Controller.Current.Notes.TextSize);
                Assert.IsFalse(File.Exists(fixture.ConfigPath), "Invalid input must not be saved.");
                Assert.AreEqual(Visibility.Visible, ((TextBlock)window.FindName("ErrorMessage")).Visibility);
                Assert.AreEqual(text, input.Text, "Keep invalid text available for correction.");
            }
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public void TextSizeAndLineSpacingSaveFailureRestoresThePreviousFieldsAndSliders()
    {
        using var fixture = new Fixture("dark");
        fixture.Store.Save(fixture.Controller.Current);
        var effective = fixture.Controller.Current.Notes;
        var window = fixture.Settings(settings => effective = settings);
        try
        {
            var input = (TextBox)window.FindName("TextSizeInput");
            input.Text = "24";
            ((TextBox)window.FindName("LineSpacingInput")).Text = "1";
            Assert.AreEqual(24, effective.TextSize);
            Assert.AreEqual(1.0, effective.LineSpacing);
            using (new FileStream(fixture.ConfigPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                typeof(NotesSettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, [window, new RoutedEventArgs(Button.ClickEvent)]);
            Assert.AreEqual(16, fixture.Controller.Current.Notes.TextSize);
            Assert.AreEqual(16, fixture.Store.LoadOrCreate().Notes.TextSize);
            Assert.AreEqual("16", input.Text);
            Assert.AreEqual(16.0, ((Slider)window.FindName("TextSizeSlider")).Value);
            Assert.AreEqual(2.0, fixture.Controller.Current.Notes.LineSpacing);
            Assert.AreEqual("2", ((TextBox)window.FindName("LineSpacingInput")).Text);
            Assert.AreEqual(2.0, ((Slider)window.FindName("LineSpacingSlider")).Value);
            Assert.AreEqual(fixture.Controller.Current.Notes, effective, "Failed saving must roll back the live preview.");
            Assert.AreEqual(Visibility.Visible, ((TextBlock)window.FindName("ErrorMessage")).Visibility);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public void SliderAndTypedTextSizeStayInSync()
    {
        using var fixture = new Fixture("dark");
        var window = fixture.Settings();
        try
        {
            var input = window.FindName("TextSizeInput") as TextBox;
            Assert.IsNotNull(input);
            var slider = (Slider)window.FindName("TextSizeSlider");
            Assert.AreEqual("16", input.Text);
            input.Text = "24";
            Assert.AreEqual(24.0, slider.Value);
            slider.Value = 20;
            Assert.AreEqual("20", input.Text);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    [DataRow("en-US", "1", true)]
    [DataRow("en-US", "1.25", true)]
    [DataRow("de-DE", "1,25", true)]
    [DataRow("en-US", "3", true)]
    [DataRow("en-US", "0.9", false)]
    [DataRow("en-US", "3.1", false)]
    [DataRow("en-US", "NaN", false)]
    [DataRow("en-US", "Infinity", false)]
    [DataRow("en-US", "abc", false)]
    [DataRow("en-US", "", false)]
    public void TypedLineSpacingSavesValidLocalizedValues(string culture, string text, bool valid)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        using var fixture = new Fixture("dark");
        var window = fixture.Settings();
        try
        {
            var input = (TextBox)window.FindName("LineSpacingInput");
            input.Text = text;
            typeof(NotesSettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, [window, new RoutedEventArgs(Button.ClickEvent)]);
            if (valid)
            {
                var expected = double.Parse(text, CultureInfo.CurrentCulture);
                Assert.AreEqual(expected, fixture.Controller.Current.Notes.LineSpacing);
                Assert.AreEqual(expected, fixture.Store.LoadOrCreate().Notes.LineSpacing);
                var reopened = fixture.Settings();
                try { Assert.AreEqual(expected, ((Slider)reopened.FindName("LineSpacingSlider")).Value); }
                finally { reopened.Close(); }
            }
            else
            {
                Assert.AreEqual(2.0, fixture.Controller.Current.Notes.LineSpacing);
                Assert.IsFalse(File.Exists(fixture.ConfigPath), "Invalid input must not be saved.");
                Assert.AreEqual(Visibility.Visible, ((TextBlock)window.FindName("ErrorMessage")).Visibility);
                Assert.AreEqual(text, input.Text);
            }
        }
        finally { window.Close(); CultureInfo.CurrentCulture = previousCulture; }
    }

    [TestMethod]
    public void LineSpacingSliderAndFieldStayInSyncAndCancelKeepsSavedValue()
    {
        using var fixture = new Fixture("dark");
        var window = fixture.Settings();
        try
        {
            var input = (TextBox)window.FindName("LineSpacingInput");
            var slider = (Slider)window.FindName("LineSpacingSlider");
            Assert.AreEqual("2", input.Text);
            input.Text = 1.25.ToString(CultureInfo.CurrentCulture);
            Assert.AreEqual(1.25, slider.Value);
            slider.Value = 2.5;
            Assert.AreEqual(2.5.ToString(CultureInfo.CurrentCulture), input.Text);
            typeof(NotesSettingsWindow).GetMethod("OnCancelClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, [window, new RoutedEventArgs(Button.ClickEvent)]);
            Assert.AreEqual(2.0, fixture.Controller.Current.Notes.LineSpacing);
            Assert.IsFalse(File.Exists(fixture.ConfigPath));
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public void PetDropdownPreviewsAllFourSheetsAndSaveKeepsTheSleepingSession()
    {
        using var fixture = new Fixture("light");
        fixture.Store.LoadOrCreate();
        fixture.Session.Sleep();
        fixture.Session.Advance(TimeSpan.FromMilliseconds(900));
        var pet = fixture.Pet();
        try
        {
            var image = (Image)pet.FindName("PetImage");
            var presentation = fixture.Session.Presentation;
            var needs = fixture.Session.Needs;
            var nap = fixture.Session.RemainingNap;
            var interval = fixture.Session.RemainingRandomInterval;
            foreach (var choice in new[] { PetCatalog.Pip, PetCatalog.Crumb, PetCatalog.Puddle, PetCatalog.Hood })
            {
                var settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
                try
                {
                    var selector = (ComboBox)settings.FindName("PetComboBox");
                    CollectionAssert.AreEqual(PetCatalog.Supported.ToArray(),
                        selector.Items.OfType<ComboBoxItem>().Select(item => (string)item.Tag).ToArray());
                    Assert.AreEqual(fixture.Controller.Current.Pet, selector.SelectedValue);
                    var oldImage = image.Source;
                    var oldChoice = fixture.Controller.Current.Pet;
                    selector.SelectedValue = choice;
                    Assert.AreNotSame(oldImage, image.Source, "An unsaved choice must preview the sprite.");
                    Assert.AreEqual(oldChoice, fixture.Store.LoadOrCreate().Pet);
                    var previewImage = image.Source;
                    typeof(SettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);

                    Assert.AreEqual(choice, fixture.Store.LoadOrCreate().Pet);
                    Assert.AreEqual(choice, fixture.Controller.Current.Pet);
                    Assert.AreEqual(presentation, fixture.Session.Presentation);
                    Assert.AreEqual(needs, fixture.Session.Needs);
                    Assert.AreEqual(nap, fixture.Session.RemainingNap);
                    Assert.AreEqual(interval, fixture.Session.RemainingRandomInterval);
                    Assert.AreSame(previewImage, image.Source, "Save must retain the preview image.");
                    var crop = (CroppedBitmap)image.Source;
                    AssertCurrentPetSource(pet, choice);
                    Assert.AreEqual(ExpectedFrameRectangle(pet, fixture.Session), crop.SourceRect);
                }
                finally { settings.Close(); }
            }
            var reopened = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
            try
            {
                Assert.AreEqual(PetCatalog.Hood, ((ComboBox)reopened.FindName("PetComboBox")).SelectedValue);
                var savedImage = image.Source;
                ((ComboBox)reopened.FindName("PetComboBox")).SelectedValue = PetCatalog.Pip;
                typeof(SettingsWindow).GetMethod("OnCancelClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(reopened, [reopened, new RoutedEventArgs(Button.ClickEvent)]);
                Assert.AreNotSame(((CroppedBitmap)savedImage).Source, ((CroppedBitmap)image.Source).Source,
                    "Cancel restores the original pixels from a new decoded sheet.");
                AssertCurrentPetSource(pet, PetCatalog.Hood);
                Assert.AreEqual(PetCatalog.Hood, fixture.Store.LoadOrCreate().Pet);
            }
            finally { reopened.Close(); }
        }
        finally { pet.Close(); }
    }

    [TestMethod]
    [DataRow(PetCatalog.Pip)]
    [DataRow(PetCatalog.Crumb)]
    [DataRow(PetCatalog.Puddle)]
    public void SavedPetLoadsAtStartupAndCanSwitchBackDuringFeeding(string choice)
    {
        using var fixture = new Fixture("light");
        fixture.Controller.SaveAndApply("pet", fixture.Controller.Current with { Pet = choice });
        fixture.Session.Feed();
        fixture.Session.Advance(TimeSpan.FromMilliseconds(180));
        var snapshot = new HoodSessionSnapshot
        {
            Position = fixture.Session.Position, RoamingArea = fixture.Session.RoamingArea,
            Action = fixture.Session.Presentation.Action, FrameIndex = fixture.Session.Presentation.FrameIndex,
            Needs = fixture.Session.Needs, RemainingRandomInterval = fixture.Session.RemainingRandomInterval
        };
        var load = typeof(HoodOverlayWindow).Assembly.GetType("DesktopPet.Windows.HoodAssets")!.GetMethod("LoadPet")!;
        var saved = fixture.Store.LoadOrCreate();
        var assets = load.Invoke(null, [Path.Combine(AppContext.BaseDirectory, "Assets"), saved.Pet, null])!;
        var definition = (PetDefinition)assets.GetType().GetProperty("Definition")!.GetValue(assets)!;
        var session = HoodSession.Start(definition, [new("Primary", new PetBounds(0, 0, 1920, 1040), true)],
            new FixedRandomSource(), new NoopSessionStore(), saved, snapshot);
        Action noop = () => { };
        var pet = (HoodOverlayWindow)Activator.CreateInstance(typeof(HoodOverlayWindow), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [session, assets, noop, noop, noop, noop, noop, noop, UiStyleCatalog.StickyNotes], null)!;
        var controller = new HoodConfigurationController(session, fixture.Store);
        var settings = (SettingsWindow)Activator.CreateInstance(typeof(SettingsWindow), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [controller, pet], null)!;
        try
        {
            Assert.AreEqual(fixture.Session.Presentation, session.Presentation);
            Assert.AreEqual(choice, ((ComboBox)settings.FindName("PetComboBox")).SelectedValue);
            var image = (Image)pet.FindName("PetImage");
            AssertCurrentPetSource(pet, choice);
            ((ComboBox)settings.FindName("PetComboBox")).SelectedValue = PetCatalog.Hood;
            typeof(SettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);
            Assert.AreEqual(fixture.Session.Presentation, session.Presentation);
            Assert.AreEqual(snapshot.Needs, session.Needs);
            session.Advance(TimeSpan.FromMilliseconds(60));
            typeof(HoodOverlayWindow).GetMethod("RefreshBehaviorSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(pet, null);
            Assert.AreEqual(ExpectedFrameRectangle(pet, session), ((CroppedBitmap)image.Source).SourceRect);
            Assert.AreEqual(PetCatalog.Hood, fixture.Store.LoadOrCreate().Pet);
        }
        finally { settings.Close(); pet.Close(); }
    }

    [TestMethod]
    public void FailedSettingsSaveKeepsTheCurrentSpriteAndRestoresTheDropdown()
    {
        using var fixture = new Fixture("light");
        fixture.Store.LoadOrCreate();
        var baseline = File.ReadAllText(fixture.ConfigPath);
        var pet = fixture.Pet();
        var settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
        try
        {
            var original = ((Image)pet.FindName("PetImage")).Source;
            ((ComboBox)settings.FindName("PetComboBox")).SelectedValue = PetCatalog.Pip;
            using (var locked = new FileStream(fixture.ConfigPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                typeof(SettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);
            }

            Assert.AreEqual(baseline, File.ReadAllText(fixture.ConfigPath));
            Assert.AreEqual(PetCatalog.Hood, fixture.Controller.Current.Pet);
            Assert.AreEqual(PetCatalog.Hood, ((ComboBox)settings.FindName("PetComboBox")).SelectedValue);
            Assert.AreNotSame(((CroppedBitmap)original).Source,
                ((CroppedBitmap)((Image)pet.FindName("PetImage")).Source).Source,
                "A failed save restores the original pixels from its rollback snapshot.");
            AssertCurrentPetSource(pet, PetCatalog.Hood);
            Assert.AreEqual(Visibility.Visible, ((TextBlock)settings.FindName("AppearanceErrorText")).Visibility);
            StringAssert.Contains(((TextBlock)settings.FindName("AppearanceErrorText")).Text, "Settings were not saved");
        }
        finally { settings.Close(); pet.Close(); }
    }

    [TestMethod]
    public void PetLoaderRejectsMissingAndIncompatibleAssetsBeforeTheCurrentSheetChanges()
    {
        using var fixture = new Fixture("light");
        var pet = fixture.Pet();
        var root = Path.Combine(Path.GetTempPath(), $"HoodPetAssets-{Guid.NewGuid():N}");
        try
        {
            var original = ((Image)pet.FindName("PetImage")).Source;
            var definition = (PetDefinition)fixture.Assets.GetType().GetProperty("Definition")!.GetValue(fixture.Assets)!;
            var load = typeof(HoodOverlayWindow).Assembly.GetType("DesktopPet.Windows.HoodAssets")!.GetMethod("LoadPet")!;
            var missing = Assert.Throws<TargetInvocationException>(() => load.Invoke(null, [root, PetCatalog.Pip, definition]));
            Assert.IsInstanceOfType<PetAssetException>(missing.InnerException);
            var directory = Path.Combine(root, "Pip");
            Directory.CreateDirectory(directory);
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Assets", "Pip", "hood_spritesheet.png"), Path.Combine(directory, "hood_spritesheet.png"));
            var json = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "Pip", "hood.animations.json")))!;
            json["animations"]![0]!["frameDurationMilliseconds"] = 81;
            File.WriteAllText(Path.Combine(directory, "hood.animations.json"), json.ToJsonString());
            var incompatible = Assert.Throws<TargetInvocationException>(() => load.Invoke(null, [root, PetCatalog.Pip, definition]));
            Assert.IsInstanceOfType<PetAssetException>(incompatible.InnerException);
            StringAssert.Contains(incompatible.InnerException!.Message, "must match");
            Assert.AreSame(original, ((Image)pet.FindName("PetImage")).Source);
            Assert.AreEqual(PetCatalog.Hood, fixture.Controller.Current.Pet);
        }
        finally
        {
            pet.Close();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CancelAndWindowCloseRestoreBothOriginalChoicesAfterRepeatedPreviews(bool cancelButton)
    {
        using var fixture = new Fixture("dark", UiStyleCatalog.Studio);
        fixture.Store.Save(fixture.Controller.Current);
        fixture.Session.Sleep();
        fixture.Session.Advance(TimeSpan.FromMilliseconds(900));
        var pet = fixture.Pet(UiStyleCatalog.Studio);
        var initial = fixture.HoodSettings(pet, UiStyleCatalog.Studio);
        ((ComboBox)initial.FindName("PetComboBox")).SelectedValue = PetCatalog.Pip;
        typeof(SettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(initial, [initial, new RoutedEventArgs(Button.ClickEvent)]);
        var settings = fixture.HoodSettings(pet, UiStyleCatalog.Studio);
        try
        {
            ShowHidden(settings);
            var baseline = File.ReadAllText(fixture.ConfigPath);
            var image = (Image)pet.FindName("PetImage");
            var originalSheet = ((CroppedBitmap)image.Source).Source;
            var originalAccent = ((SolidColorBrush)settings.Resources["AppAccentBrush"]).Color;
            ((ComboBox)settings.FindName("PetComboBox")).SelectedValue = PetCatalog.Puddle;
            ((ComboBox)settings.FindName("UiStyleComboBox")).SelectedValue = UiStyleCatalog.Minimal;
            fixture.Session.Advance(TimeSpan.FromMilliseconds(225));
            ((ComboBox)settings.FindName("PetComboBox")).SelectedValue = PetCatalog.Crumb;
            ((ComboBox)settings.FindName("UiStyleComboBox")).SelectedValue = UiStyleCatalog.Soft;
            ((CheckBox)settings.FindName("RoamingCheckBox")).IsChecked = false;

            Assert.AreNotSame(originalSheet, ((CroppedBitmap)image.Source).Source);
            Assert.AreNotEqual(originalAccent, ((SolidColorBrush)settings.Resources["AppAccentBrush"]).Color);
            Assert.IsTrue(fixture.Controller.Current.Behavior.Roaming, "Other settings must remain pending until Save.");
            var presentation = fixture.Session.Presentation;
            var needs = fixture.Session.Needs;
            var remainingNap = fixture.Session.RemainingNap;
            if (cancelButton)
                typeof(SettingsWindow).GetMethod("OnCancelClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);
            else
                settings.Close();

            Assert.IsFalse(settings.IsVisible);
            Assert.AreNotSame(originalSheet, ((CroppedBitmap)image.Source).Source,
                "Cancel decodes the saved original from its compressed snapshot.");
            AssertCurrentPetSource(pet, PetCatalog.Pip);
            Assert.AreEqual(originalAccent, ((SolidColorBrush)settings.Resources["AppAccentBrush"]).Color);
            Assert.AreEqual(originalAccent, ((SolidColorBrush)pet.ContextMenu!.Resources["AppAccentBrush"]).Color);
            Assert.AreEqual(baseline, File.ReadAllText(fixture.ConfigPath));
            Assert.AreEqual(PetCatalog.Pip, fixture.Controller.Current.Pet);
            Assert.AreEqual(UiStyleCatalog.Studio, fixture.Controller.Current.UiStyle);
            Assert.AreEqual(presentation, fixture.Session.Presentation, "Rollback must not rewind animation progress.");
            Assert.AreEqual(needs, fixture.Session.Needs);
            Assert.AreEqual(remainingNap, fixture.Session.RemainingNap);
            Assert.AreEqual(ExpectedFrameRectangle(pet, fixture.Session), ((CroppedBitmap)image.Source).SourceRect);
            var reopened = fixture.HoodSettings(pet, UiStyleCatalog.Studio);
            try
            {
                Assert.AreEqual(PetCatalog.Pip, ((ComboBox)reopened.FindName("PetComboBox")).SelectedValue);
                Assert.AreEqual(UiStyleCatalog.Studio, ((ComboBox)reopened.FindName("UiStyleComboBox")).SelectedValue);
            }
            finally { reopened.Close(); }
        }
        finally { settings.Close(); initial.Close(); pet.Close(); }
    }

    [TestMethod]
    public void SaveKeepsBothLiveChoicesAndDoesNotRollBackOnWindowClose()
    {
        using var fixture = new Fixture("light");
        fixture.Store.LoadOrCreate();
        var pet = fixture.Pet();
        var settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
        try
        {
            ((ComboBox)settings.FindName("UiStyleComboBox")).SelectedValue = UiStyleCatalog.Soft;
            ((ComboBox)settings.FindName("PetComboBox")).SelectedValue = PetCatalog.Crumb;
            var sheet = ((CroppedBitmap)((Image)pet.FindName("PetImage")).Source).Source;
            var accent = ((SolidColorBrush)settings.Resources["AppAccentBrush"]).Color;
            typeof(SettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);

            Assert.AreEqual(PetCatalog.Crumb, fixture.Store.LoadOrCreate().Pet);
            Assert.AreEqual(UiStyleCatalog.Soft, fixture.Store.LoadOrCreate().UiStyle);
            Assert.AreSame(sheet, ((CroppedBitmap)((Image)pet.FindName("PetImage")).Source).Source);
            Assert.AreEqual(accent, ((SolidColorBrush)settings.Resources["AppAccentBrush"]).Color);
            Assert.AreEqual(accent, ((SolidColorBrush)pet.ContextMenu!.Resources["AppAccentBrush"]).Color);
        }
        finally { settings.Close(); pet.Close(); }
    }

    [TestMethod]
    public void FailedRollbackKeepsSettingsOpenForAnotherCloseAttempt()
    {
        using var fixture = new Fixture("light");
        fixture.Store.LoadOrCreate();
        var pet = fixture.Pet();
        var failRestore = true;
        var settings = (SettingsWindow)Activator.CreateInstance(typeof(SettingsWindow), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [fixture.Controller, pet, UiStyleCatalog.StickyNotes, (Action<string>)(style =>
            {
                if (style == UiStyleCatalog.StickyNotes && failRestore) throw new InvalidOperationException("restore failed");
            })], null)!;
        try
        {
            ShowHidden(settings);
            ((ComboBox)settings.FindName("UiStyleComboBox")).SelectedValue = UiStyleCatalog.Minimal;
            settings.Close();
            Assert.IsTrue(settings.IsVisible);
            StringAssert.Contains(((TextBlock)settings.FindName("AppearanceErrorText")).Text, "Could not restore");
            Assert.AreEqual(UiStyleCatalog.StickyNotes, fixture.Store.LoadOrCreate().UiStyle);
            failRestore = false;
            settings.Close();
            Assert.IsFalse(settings.IsVisible);
            Assert.AreEqual("#FF446DA5", ((SolidColorBrush)settings.Resources["AppAccentBrush"]).Color.ToString());
        }
        finally { failRestore = false; settings.Close(); pet.Close(); }
    }

    [TestMethod]
    public void InvalidPetPreviewPreservesTheLastSuccessfulPreviewAndTheSavedChoice()
    {
        using var fixture = new Fixture("light");
        fixture.Store.LoadOrCreate();
        var pet = fixture.Pet();
        var settings = fixture.HoodSettings(pet, UiStyleCatalog.StickyNotes);
        try
        {
            var selector = (ComboBox)settings.FindName("PetComboBox");
            selector.SelectedValue = PetCatalog.Pip;
            var image = ((Image)pet.FindName("PetImage")).Source;
            selector.Items.Add(new ComboBoxItem { Tag = "missing", Content = "Missing pet" });
            selector.SelectedValue = "missing";
            Assert.AreEqual(PetCatalog.Pip, selector.SelectedValue);
            Assert.AreSame(image, ((Image)pet.FindName("PetImage")).Source);
            Assert.AreEqual(PetCatalog.Hood, fixture.Store.LoadOrCreate().Pet);
            StringAssert.Contains(((TextBlock)settings.FindName("AppearanceErrorText")).Text, "Could not preview this pet");
        }
        finally { settings.Close(); pet.Close(); }
    }

    [TestMethod]
    [DataRow("cancel")]
    [DataRow("close")]
    [DataRow("save")]
    public void HoodSettingsPreviewsStyleAndThemeAcrossOpenNotesSettingsListsMenusAndTray(string action)
    {
        using var fixture = new Fixture("light");
        fixture.Store.LoadOrCreate();
        var notesPath = fixture.ConfigPath + ".notes.json";
        var notes = NotesApplication.Start(new JsonNotesStore(notesPath));
        var noteData = notes.GetNote(notes.CreateNote());
        var note = new NoteWindow(noteData,
            (_, _) => null, (_, _) => null, (_, _) => null, (_, _) => (null, null), _ => (true, null),
            () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null, fixture.Controller.Current.Notes);
        var notesBaseline = File.ReadAllText(notesPath);
        var configurationBaseline = File.ReadAllText(fixture.ConfigPath);
        var pet = fixture.Pet();
        var notesSettings = fixture.Settings();
        var notesList = new NotesListWindow(_ => [], _ => [], _ => { }, () => { }, _ => null, _ => null, () => null, "light");
        using var tray = new Forms.ContextMenuStrip();
        tray.Items.Add("Settings");
        using var icon = new Forms.NotifyIcon { ContextMenuStrip = tray };
        var app = System.Windows.Application.Current as App ?? new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var fields = new[] { "_session", "_notes", "_petWindow", "_settingsWindow", "_notesSettingsWindow", "_notesListWindow", "_trayIcon", "_effectiveNotesSettings", "_effectiveUiStyle" }
            .Select(name => typeof(App).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!).ToArray();
        var originalValues = fields.Select(field => field.GetValue(app)).ToArray();
        var apply = typeof(App).GetMethod("ApplyUiStyle", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var applyTheme = typeof(App).GetMethod("ApplyTheme", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var settings = (SettingsWindow)Activator.CreateInstance(typeof(SettingsWindow), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [fixture.Controller, pet, UiStyleCatalog.StickyNotes, (Action<string>)(style => apply.Invoke(app, [style])),
                "light", (Action<string>)(theme => applyTheme.Invoke(app, [theme]))], null)!;
        var windows = (Dictionary<Guid, NoteWindow>)typeof(App).GetField("_noteWindows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app)!;
        object[] values = [fixture.Session, notes, pet, settings, notesSettings, notesList, icon, fixture.Controller.Current.Notes, UiStyleCatalog.StickyNotes];
        for (var index = 0; index < fields.Length; index++) fields[index].SetValue(app, values[index]);
        windows.Add(noteData.Id, note);
        try
        {
            foreach (var (style, expectedAccent) in new[] { (UiStyleCatalog.Soft, "#FF557F70"), (UiStyleCatalog.Studio, "#FF7652CE") })
            {
                ((ComboBox)settings.FindName("UiStyleComboBox")).SelectedValue = style;
                foreach (var surface in new FrameworkElement[] { settings, notesSettings, notesList, note, pet.ContextMenu! })
                    Assert.AreEqual(expectedAccent, ((SolidColorBrush)surface.Resources["AppAccentBrush"]).Color.ToString());
                Assert.AreEqual(UiStyleCatalog.StickyNotes, fixture.Controller.Current.UiStyle);
                Assert.AreEqual(configurationBaseline, File.ReadAllText(fixture.ConfigPath));
                Assert.AreEqual(notesBaseline, File.ReadAllText(notesPath), "Live styling must not rewrite note data.");
            }
            Assert.IsNull(notesSettings.FindName("ThemeComboBox"), "Brightness is selected in Hood Settings.");
            CollectionAssert.AreEquivalent(Enum.GetValues<NoteTheme>(), ((ComboBox)settings.FindName("ThemeComboBox")).Items.Cast<NoteTheme>().ToArray());
            ((ComboBox)settings.FindName("ThemeComboBox")).SelectedItem = NoteTheme.Dark;
            foreach (var surface in new FrameworkElement[] { settings, notesSettings, notesList, note, pet.ContextMenu! })
                Assert.AreEqual("#FFBBA5FF", ((SolidColorBrush)surface.Resources["AppAccentBrush"]).Color.ToString());
            Assert.AreEqual(configurationBaseline, File.ReadAllText(fixture.ConfigPath));
            var previewTrayColor = tray.BackColor;
            if (action == "close") settings.Close();
            else typeof(SettingsWindow).GetMethod(action == "save" ? "OnSaveClick" : "OnCancelClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);
            foreach (var surface in new FrameworkElement[] { settings, notesSettings, notesList, note, pet.ContextMenu! })
                Assert.AreEqual(action == "save" ? "#FFBBA5FF" : "#FF446DA5", ((SolidColorBrush)surface.Resources["AppAccentBrush"]).Color.ToString());
            if (action == "save")
            {
                Assert.AreEqual(previewTrayColor, tray.BackColor);
                Assert.AreEqual("dark", fixture.Store.LoadOrCreate().Notes.Theme);
                Assert.AreEqual(UiStyleCatalog.Studio, fixture.Store.LoadOrCreate().UiStyle);
            }
            else
            {
                Assert.AreNotEqual(previewTrayColor, tray.BackColor, "Closing without Save must also restore the tray.");
                Assert.AreEqual(DrawingColor.FromArgb(255, 249, 249, 249), tray.BackColor);
                Assert.AreEqual(configurationBaseline, File.ReadAllText(fixture.ConfigPath));
            }
            Assert.AreEqual(notesBaseline, File.ReadAllText(notesPath));
            Assert.AreEqual(noteData.Color, notes.GetNote(noteData.Id).Color);
            Assert.AreEqual(noteData.Document.GetPlainText(), notes.GetNote(noteData.Id).Document.GetPlainText());
        }
        finally
        {
            settings.Close();
            windows.Remove(noteData.Id);
            for (var index = 0; index < fields.Length; index++) fields[index].SetValue(app, originalValues[index]);
            notesSettings.Close(); notesList.Close(); note.Close(); pet.Close();
            File.Delete(notesPath);
        }
    }

    [TestMethod]
    [DataRow("cancel")]
    [DataRow("close")]
    [DataRow("save")]
    public void NotesSettingsPreviewsEveryChoiceAcrossOpenSurfacesAndRestoresOrSaves(string action)
    {
        using var fixture = new Fixture("light");
        fixture.Store.Save(fixture.Controller.Current);
        var original = fixture.Controller.Current.Notes;
        var configBaseline = File.ReadAllText(fixture.ConfigPath);
        var notesPath = fixture.ConfigPath + ".notes.json";
        var notes = NotesApplication.Start(new JsonNotesStore(notesPath));
        var noteData = notes.GetNote(notes.CreateNote());
        var notesBaseline = File.ReadAllText(notesPath);
        var savedEdits = 0;
        var note = new NoteWindow(noteData, (_, _) => { savedEdits++; return null; }, (_, _) => null, (_, _) => null,
            (_, _) => (null, null), _ => (true, null), () => { }, () => { }, () => { },
            (_, _) => null, _ => null, () => null, original);
        var notesList = new NotesListWindow(_ => [], _ => [], _ => { }, () => { }, _ => null, _ => null, () => null, "light");
        var pet = fixture.Pet();
        using var tray = new Forms.ContextMenuStrip();
        tray.Items.Add("Settings");
        using var icon = new Forms.NotifyIcon { ContextMenuStrip = tray };
        var app = System.Windows.Application.Current as App ?? new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var fields = new[] { "_session", "_notes", "_petWindow", "_notesSettingsWindow", "_notesListWindow", "_trayIcon", "_effectiveNotesSettings", "_effectiveUiStyle" }
            .Select(name => typeof(App).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!).ToArray();
        var previousValues = fields.Select(field => field.GetValue(app)).ToArray();
        var apply = typeof(App).GetMethod("ApplyNotesSettings", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var settings = fixture.Settings(candidate => apply.Invoke(app, [candidate]));
        var windows = (Dictionary<Guid, NoteWindow>)typeof(App).GetField("_noteWindows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app)!;
        object[] values = [fixture.Session, notes, pet, settings, notesList, icon, original, UiStyleCatalog.StickyNotes];
        for (var index = 0; index < fields.Length; index++) fields[index].SetValue(app, values[index]);
        windows.Add(noteData.Id, note);
        try
        {
            var editor = (RichTextBox)note.FindName("Editor");
            var document = editor.Document;
            ((ComboBox)settings.FindName("DefaultColorComboBox")).SelectedItem = NoteColor.Blue;
            Assert.AreEqual("blue", Effective().DefaultColor);
            foreach (var surface in new FrameworkElement[] { settings, notesList, note, pet.ContextMenu! })
                Assert.AreEqual("#FFF7F7F7", ((SolidColorBrush)surface.Resources["NotesSurfaceBrush"]).Color.ToString());
            ((TextBox)settings.FindName("TextSizeInput")).Text = "24";
            Assert.AreEqual(24.0, document.FontSize);
            ((Slider)settings.FindName("LineSpacingSlider")).Value = 1;
            Assert.AreEqual(LineStackingStrategy.BlockLineHeight, document.LineStackingStrategy);
            ((CheckBox)settings.FindName("AlwaysOnTopCheckBox")).IsChecked = true;
            Assert.IsTrue(note.Topmost);
            ((CheckBox)settings.FindName("AnimateShowHideCheckBox")).IsChecked = true;
            Assert.IsTrue(Effective().AnimateShowHide);
            Assert.AreEqual(original, fixture.Controller.Current.Notes);
            Assert.AreEqual(configBaseline, File.ReadAllText(fixture.ConfigPath));
            Assert.AreEqual(notesBaseline, File.ReadAllText(notesPath));
            Assert.AreSame(document, editor.Document);
            Assert.AreEqual(0, savedEdits, "Preview must not create document edits.");
            StringAssert.Contains(((TextBlock)settings.FindName("AppearanceNoticeText")).Text, "Previewing unsaved");

            typeof(App).GetMethod("ApplyUiStyle", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(app, [UiStyleCatalog.Soft]);
            var preview = Effective();
            if (action == "close") settings.Close();
            else typeof(NotesSettingsWindow).GetMethod(action == "save" ? "OnSaveClick" : "OnCancelClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);
            var expected = action == "save" ? preview : original;
            Assert.AreEqual(expected, Effective());
            Assert.AreEqual(expected, fixture.Store.LoadOrCreate().Notes);
            Assert.AreEqual(expected.TextSize, document.FontSize);
            Assert.AreEqual(expected.AlwaysOnTop, note.Topmost);
            Assert.AreEqual(UiStyleCatalog.Soft, fields.Last().GetValue(app), "Notes rollback must keep the active Hood style.");
            if (action != "save")
            {
                Assert.IsTrue(double.IsNaN(document.LineHeight));
                Assert.AreEqual(LineStackingStrategy.MaxHeight, document.LineStackingStrategy);
                Assert.AreEqual(configBaseline, File.ReadAllText(fixture.ConfigPath));
            }
            Assert.AreEqual(notesBaseline, File.ReadAllText(notesPath));
            Assert.AreEqual(action == "save" ? NoteColor.Blue : NoteColor.Yellow, notes.GetNote(notes.CreateNote()).Color);
            Assert.AreEqual(noteData.Color, notes.GetNote(noteData.Id).Color);

            NotesConfiguration Effective() => (NotesConfiguration)fields[^2].GetValue(app)!;
        }
        finally
        {
            settings.Close();
            windows.Remove(noteData.Id);
            for (var index = 0; index < fields.Length; index++) fields[index].SetValue(app, previousValues[index]);
            notesList.Close(); note.CloseWithoutMembershipChange(); pet.Close();
            File.Delete(notesPath);
        }
    }

    [TestMethod]
    [DataRow("text-size")]
    [DataRow("line-spacing")]
    public void FailedNotesPreviewRestoresControlsAndPreservesConfiguration(string field)
    {
        using var fixture = new Fixture("light");
        fixture.Store.LoadOrCreate();
        var baseline = File.ReadAllText(fixture.ConfigPath);
        var settings = fixture.Settings(_ => throw new InvalidOperationException("simulated preview failure"));
        try
        {
            ((TextBox)settings.FindName(field == "text-size" ? "TextSizeInput" : "LineSpacingInput")).Text = field == "text-size" ? "024" : 1.0.ToString("0.00", CultureInfo.CurrentCulture);
            Assert.AreEqual("16", ((TextBox)settings.FindName("TextSizeInput")).Text);
            Assert.AreEqual("2", ((TextBox)settings.FindName("LineSpacingInput")).Text);
            Assert.AreEqual(baseline, File.ReadAllText(fixture.ConfigPath));
            var error = (TextBlock)settings.FindName("ErrorMessage");
            Assert.AreEqual(Visibility.Visible, error.Visibility);
            StringAssert.Contains(error.Text, "Could not preview");
        }
        finally { settings.Close(); }
    }

    [TestMethod]
    public void InvalidNumericInputKeepsTheLastValidNotesPreviewUntilClose()
    {
        using var fixture = new Fixture("light");
        var effective = fixture.Controller.Current.Notes;
        var settings = fixture.Settings(candidate => effective = candidate);
        try
        {
            var input = (TextBox)settings.FindName("TextSizeInput");
            input.Text = "24";
            Assert.AreEqual(24, effective.TextSize);
            input.Text = "invalid";
            typeof(NotesSettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);
            Assert.AreEqual(24, effective.TextSize);
            Assert.AreEqual(16, fixture.Controller.Current.Notes.TextSize);
            Assert.IsFalse(File.Exists(fixture.ConfigPath));
            settings.Close();
            Assert.AreEqual(fixture.Controller.Current.Notes, effective);
        }
        finally { settings.Close(); }
    }

    [TestMethod]
    public void FailedNotesPreviewRestoreKeepsWindowOpenForRetry()
    {
        using var fixture = new Fixture("light");
        var original = fixture.Controller.Current.Notes;
        var effective = original;
        var failRestore = false;
        var closed = false;
        var settings = fixture.Settings(candidate =>
        {
            if (failRestore && candidate == original) throw new InvalidOperationException("simulated restore failure");
            effective = candidate;
        });
        settings.Closed += (_, _) => closed = true;
        try
        {
            ((Slider)settings.FindName("TextSizeSlider")).Value = 24;
            failRestore = true;
            settings.Close();
            Assert.IsFalse(closed, "A failed restore must keep the window available to retry.");
            Assert.AreEqual(24, effective.TextSize);
            StringAssert.Contains(((TextBlock)settings.FindName("ErrorMessage")).Text, "Could not restore");
            failRestore = false;
            settings.Close();
            Assert.IsTrue(closed);
            Assert.AreEqual(original, effective);
            Assert.IsFalse(File.Exists(fixture.ConfigPath));
        }
        finally { failRestore = false; settings.Close(); }
    }

    [TestMethod]
    public void NotesCancelRestoresTheEffectiveSettingsCapturedWhenOpened()
    {
        using var fixture = new Fixture("light");
        var saved = fixture.Controller.Current.Notes;
        var original = saved with { TextSize = 20, LineSpacing = 1, Theme = "dark" };
        var effective = original;
        var settings = (NotesSettingsWindow)Activator.CreateInstance(typeof(NotesSettingsWindow), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [fixture.Controller, (Action<NotesConfiguration>)(candidate => effective = candidate), UiStyleCatalog.StickyNotes, original], null)!;
        try
        {
            Assert.AreEqual(original, effective, "Opening settings must not apply the saved fields before editing.");
            ((Slider)settings.FindName("TextSizeSlider")).Value = 24;
            Assert.AreEqual(24, effective.TextSize);
            settings.Close();
            Assert.AreEqual(original, effective, "Cancel must restore the actual starting appearance, including an unapplied saved setting.");
            Assert.AreEqual(saved, fixture.Controller.Current.Notes);
            Assert.IsFalse(File.Exists(fixture.ConfigPath));
        }
        finally { settings.Close(); }
    }

    [TestMethod]
    [DataRow("preview")]
    [DataRow("save")]
    public void FailedHoodThemeChangeRestoresTheThemeAndPreservesSavedSettings(string failure)
    {
        using var fixture = new Fixture("light");
        fixture.Store.Save(fixture.Controller.Current);
        var baseline = File.ReadAllText(fixture.ConfigPath);
        var pet = fixture.Pet();
        var effective = "light";
        var settings = (SettingsWindow)Activator.CreateInstance(typeof(SettingsWindow), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [fixture.Controller, pet, UiStyleCatalog.StickyNotes, (Action<string>)(_ => { }), "light", (Action<string>)(theme =>
            {
                if (failure == "preview" && theme == "dark") throw new InvalidOperationException("simulated preview failure");
                effective = theme;
            })], null)!;
        try
        {
            var selector = (ComboBox)settings.FindName("ThemeComboBox");
            selector.SelectedItem = NoteTheme.Dark;
            if (failure == "save")
            {
                Assert.AreEqual("dark", effective);
                using var locked = new FileStream(fixture.ConfigPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                typeof(SettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);
            }
            Assert.AreEqual("light", effective);
            Assert.AreEqual(NoteTheme.Light, selector.SelectedItem);
            Assert.AreEqual("light", fixture.Controller.Current.Notes.Theme);
            Assert.AreEqual(baseline, File.ReadAllText(fixture.ConfigPath));
            Assert.AreEqual(Visibility.Visible, ((TextBlock)settings.FindName("AppearanceErrorText")).Visibility);
        }
        finally { settings.Close(); pet.Close(); }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void HoodThemeAndNotesSettingsCanSaveOrCancelIndependently(bool saveNotes, bool saveHood)
    {
        using var fixture = new Fixture("light");
        fixture.Store.Save(fixture.Controller.Current);
        var effective = fixture.Controller.Current.Notes;
        var pet = fixture.Pet();
        SettingsWindow? hood = null;
        var notes = fixture.Settings(candidate => { effective = candidate; hood?.ApplyTheme(candidate.Theme); });
        hood = (SettingsWindow)Activator.CreateInstance(typeof(SettingsWindow), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [fixture.Controller, pet, UiStyleCatalog.StickyNotes, (Action<string>)(_ => { }), "light", (Action<string>)(theme =>
            {
                effective = effective with { Theme = theme };
                notes.ApplyEffectiveSettings(effective);
            })], null)!;
        try
        {
            ((Slider)notes.FindName("TextSizeSlider")).Value = 24;
            ((ComboBox)hood.FindName("ThemeComboBox")).SelectedItem = NoteTheme.Dark;
            Assert.AreEqual(24, effective.TextSize);
            Assert.AreEqual("dark", effective.Theme);
            if (saveNotes)
                typeof(NotesSettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(notes, [notes, new RoutedEventArgs(Button.ClickEvent)]);
            else notes.Close();
            Assert.AreEqual("dark", effective.Theme, "Notes must preserve Hood's active theme preview.");
            Assert.AreEqual("light", fixture.Store.LoadOrCreate().Notes.Theme, "Saving Notes must not save Hood's unsaved preview.");
            if (saveHood)
                typeof(SettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(hood, [hood, new RoutedEventArgs(Button.ClickEvent)]);
            else hood.Close();
            Assert.AreEqual(saveHood ? "dark" : "light", effective.Theme);
            Assert.AreEqual(saveNotes ? 24 : 16, effective.TextSize);
            Assert.AreEqual(effective, fixture.Store.LoadOrCreate().Notes);
        }
        finally { notes.Close(); hood.Close(); pet.Close(); }
    }

    private static void ShowHidden(Window window)
    {
        window.Opacity = 0;
        window.ShowInTaskbar = false;
        window.Left = window.Top = -10000;
        window.Show();
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
    }

    private static Int32Rect ExpectedFrameRectangle(HoodOverlayWindow pet, HoodSession session)
    {
        var assets = typeof(HoodOverlayWindow).GetProperty("CurrentAssets", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet)!;
        var definition = (PetDefinition)assets.GetType().GetProperty("Definition")!.GetValue(assets)!;
        var presentation = session.Presentation;
        var frame = definition.GetAnimation(presentation.Action).Frames[presentation.FrameIndex];
        return (Int32Rect)assets.GetType().GetMethod("GetFrameRectangle")!.Invoke(assets, [frame])!;
    }

    private static void AssertCurrentPetSource(HoodOverlayWindow pet, string expectedPet)
    {
        var source = typeof(HoodOverlayWindow).GetProperty("CurrentSource", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(pet)!;
        var directory = (string)source.GetType().GetProperty("Directory")!.GetValue(source)!;
        StringAssert.Contains(directory.ToLowerInvariant(), $"{Path.DirectorySeparatorChar}{expectedPet.ToLowerInvariant()}");
        var assets = typeof(HoodOverlayWindow).GetProperty("CurrentAssets", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(pet)!;
        var sheet = assets.GetType().GetProperty("SpriteSheet")!.GetValue(assets);
        Assert.AreSame(sheet, ((CroppedBitmap)((Image)pet.FindName("PetImage")).Source).Source,
            "The expected pet's loaded sheet must be the visible crop source.");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private sealed class TestApp : App
    {
        protected override void OnStartup(StartupEventArgs e) { }
    }

    private sealed class Fixture : IDisposable
    {
        public string ConfigPath { get; } = Path.Combine(Path.GetTempPath(), $"HoodSettingsTest-{Guid.NewGuid():N}.yaml");
        public object Assets { get; }
        public HoodSession Session { get; }
        public NoopSessionStore StateStore { get; } = new();
        public YamlConfigurationStore Store { get; }
        public HoodConfigurationController Controller { get; }

        public Fixture(string theme, string uiStyle = UiStyleCatalog.StickyNotes)
        {
            Assets = typeof(HoodOverlayWindow).Assembly.GetType("DesktopPet.Windows.HoodAssets")!.GetMethod("Load")!
                .Invoke(null, [Path.Combine(AppContext.BaseDirectory, "Assets", "Hood")])!;
            var definition = (PetDefinition)Assets.GetType().GetProperty("Definition")!.GetValue(Assets)!;
            Session = HoodSession.Start(definition, [new("Primary", new PetBounds(0, 0, 1920, 1040), true)],
                new FixedRandomSource(), StateStore, HoodConfiguration.Defaults with
                { UiStyle = uiStyle, Notes = HoodConfiguration.Defaults.Notes with { Theme = theme } });
            Store = new YamlConfigurationStore(ConfigPath);
            Controller = new HoodConfigurationController(Session, Store);
        }

        public NotesSettingsWindow Settings(Action<NotesConfiguration>? preview = null) => (NotesSettingsWindow)Activator.CreateInstance(typeof(NotesSettingsWindow),
            BindingFlags.Instance | BindingFlags.NonPublic, null, [Controller, preview ?? (_ => { })], null)!;

        public SettingsWindow HoodSettings(HoodOverlayWindow pet, string effectiveStyle) =>
            (SettingsWindow)Activator.CreateInstance(typeof(SettingsWindow), BindingFlags.Instance | BindingFlags.NonPublic,
                null, [Controller, pet, effectiveStyle], null)!;

        public HoodOverlayWindow Pet(string uiStyle = UiStyleCatalog.StickyNotes)
        {
            Action noop = () => { };
            return (HoodOverlayWindow)Activator.CreateInstance(typeof(HoodOverlayWindow), BindingFlags.Instance | BindingFlags.NonPublic,
                null, [Session, Assets, noop, noop, noop, noop, noop, noop, uiStyle], null)!;
        }

        public void Dispose() => File.Delete(ConfigPath);
    }

    private sealed class NoopSessionStore : ISessionStore
    {
        public HoodSessionSnapshot? Snapshot { get; private set; }
        public HoodSessionSnapshot? Load() => null;
        public void Save(HoodSessionSnapshot snapshot) => Snapshot = snapshot;
        public string? ResetAfterAcknowledgement() => null;
    }

    private sealed class FixedRandomSource : IPetRandomSource
    {
        public int NextIndex(int exclusiveUpperBound) => 0;
    }
}
