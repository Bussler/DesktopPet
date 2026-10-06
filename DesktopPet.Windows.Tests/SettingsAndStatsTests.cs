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

            var source = (BitmapImage)((CroppedBitmap)((Image)pet.FindName("PetImage")).Source).Source;
            StringAssert.Contains(source.UriSource.LocalPath.ToLowerInvariant(), "pip",
                "Selecting a pet should replace the visible sheet immediately.");
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
        var settings = (NotesSettingsWindow)Activator.CreateInstance(typeof(NotesSettingsWindow), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [fixture.Controller, (Action)(() => throw new InvalidOperationException("simulated appearance failure")),
                UiStyleCatalog.StickyNotes, fixture.Controller.Current.Notes], null)!;
        try
        {
            ((ComboBox)settings.FindName("ThemeComboBox")).SelectedItem = NoteTheme.Dark;
            typeof(NotesSettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);

            Assert.AreEqual("dark", fixture.Store.LoadOrCreate().Notes.Theme);
            Assert.AreEqual("dark", fixture.Controller.Current.Notes.Theme);
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
            BindingFlags.Instance | BindingFlags.NonPublic, null, [fixture.Controller, (Action)(() => { }), style], null)!;
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
    public void TextSizeSaveFailureRestoresThePreviousFieldAndSlider()
    {
        using var fixture = new Fixture("dark");
        fixture.Store.Save(fixture.Controller.Current);
        var window = fixture.Settings();
        try
        {
            var input = (TextBox)window.FindName("TextSizeInput");
            input.Text = "24";
            using (new FileStream(fixture.ConfigPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                typeof(NotesSettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, [window, new RoutedEventArgs(Button.ClickEvent)]);
            Assert.AreEqual(16, fixture.Controller.Current.Notes.TextSize);
            Assert.AreEqual(16, fixture.Store.LoadOrCreate().Notes.TextSize);
            Assert.AreEqual("16", input.Text);
            Assert.AreEqual(16.0, ((Slider)window.FindName("TextSizeSlider")).Value);
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
                    StringAssert.Contains(((BitmapImage)crop.Source).UriSource.LocalPath.ToLowerInvariant(), $"{Path.DirectorySeparatorChar}{choice}{Path.DirectorySeparatorChar}");
                    Assert.AreEqual(new Int32Rect(4 * 384, 8 * 416, 384, 416), crop.SourceRect);
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
                Assert.AreSame(((CroppedBitmap)savedImage).Source, ((CroppedBitmap)image.Source).Source);
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
            StringAssert.Contains(((BitmapImage)((CroppedBitmap)image.Source).Source).UriSource.LocalPath.ToLowerInvariant(),
                $"{Path.DirectorySeparatorChar}{choice}{Path.DirectorySeparatorChar}");
            ((ComboBox)settings.FindName("PetComboBox")).SelectedValue = PetCatalog.Hood;
            typeof(SettingsWindow).GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(settings, [settings, new RoutedEventArgs(Button.ClickEvent)]);
            Assert.AreEqual(fixture.Session.Presentation, session.Presentation);
            Assert.AreEqual(snapshot.Needs, session.Needs);
            session.Advance(TimeSpan.FromMilliseconds(60));
            typeof(HoodOverlayWindow).GetMethod("RefreshBehaviorSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(pet, null);
            Assert.AreEqual(new Int32Rect(4 * 384, 7 * 416, 384, 416), ((CroppedBitmap)image.Source).SourceRect);
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
            Assert.AreSame(((CroppedBitmap)original).Source, ((CroppedBitmap)((Image)pet.FindName("PetImage")).Source).Source);
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
            Assert.AreSame(originalSheet, ((CroppedBitmap)image.Source).Source);
            Assert.AreEqual(originalAccent, ((SolidColorBrush)settings.Resources["AppAccentBrush"]).Color);
            Assert.AreEqual(originalAccent, ((SolidColorBrush)pet.ContextMenu!.Resources["AppAccentBrush"]).Color);
            Assert.AreEqual(baseline, File.ReadAllText(fixture.ConfigPath));
            Assert.AreEqual(PetCatalog.Pip, fixture.Controller.Current.Pet);
            Assert.AreEqual(UiStyleCatalog.Studio, fixture.Controller.Current.UiStyle);
            Assert.AreEqual(presentation, fixture.Session.Presentation, "Rollback must not rewind animation progress.");
            Assert.AreEqual(needs, fixture.Session.Needs);
            Assert.AreEqual(remainingNap, fixture.Session.RemainingNap);
            Assert.AreEqual(new Int32Rect(presentation.FrameIndex * 384, 8 * 416, 384, 416), ((CroppedBitmap)image.Source).SourceRect);
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
    public void AppPreviewsAndRestoresStyleAcrossOpenNotesSettingsListsMenusAndTray()
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
        var settings = (SettingsWindow)Activator.CreateInstance(typeof(SettingsWindow), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [fixture.Controller, pet, UiStyleCatalog.StickyNotes, (Action<string>)(style => apply.Invoke(app, [style]))], null)!;
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
            var previewTrayColor = tray.BackColor;
            settings.Close();
            foreach (var surface in new FrameworkElement[] { settings, notesSettings, notesList, note, pet.ContextMenu! })
                Assert.AreEqual("#FF446DA5", ((SolidColorBrush)surface.Resources["AppAccentBrush"]).Color.ToString());
            Assert.AreNotEqual(previewTrayColor, tray.BackColor, "Closing without Save must also restore the tray.");
            Assert.AreEqual(DrawingColor.FromArgb(255, 249, 249, 249), tray.BackColor);
            Assert.AreEqual(configurationBaseline, File.ReadAllText(fixture.ConfigPath));
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

    private static void ShowHidden(Window window)
    {
        window.Opacity = 0;
        window.ShowInTaskbar = false;
        window.Left = window.Top = -10000;
        window.Show();
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
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
        public YamlConfigurationStore Store { get; }
        public HoodConfigurationController Controller { get; }

        public Fixture(string theme, string uiStyle = UiStyleCatalog.StickyNotes)
        {
            Assets = typeof(HoodOverlayWindow).Assembly.GetType("DesktopPet.Windows.HoodAssets")!.GetMethod("Load")!
                .Invoke(null, [Path.Combine(AppContext.BaseDirectory, "Assets", "Hood")])!;
            var definition = (PetDefinition)Assets.GetType().GetProperty("Definition")!.GetValue(Assets)!;
            Session = HoodSession.Start(definition, [new("Primary", new PetBounds(0, 0, 1920, 1040), true)],
                new FixedRandomSource(), new NoopSessionStore(), HoodConfiguration.Defaults with
                { UiStyle = uiStyle, Notes = HoodConfiguration.Defaults.Notes with { Theme = theme } });
            Store = new YamlConfigurationStore(ConfigPath);
            Controller = new HoodConfigurationController(Session, Store);
        }

        public NotesSettingsWindow Settings() => (NotesSettingsWindow)Activator.CreateInstance(typeof(NotesSettingsWindow),
            BindingFlags.Instance | BindingFlags.NonPublic, null, [Controller, (Action)(() => { })], null)!;

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
        public HoodSessionSnapshot? Load() => null;
        public void Save(HoodSessionSnapshot snapshot) { }
        public string? ResetAfterAcknowledgement() => null;
    }

    private sealed class FixedRandomSource : IPetRandomSource
    {
        public int NextIndex(int exclusiveUpperBound) => 0;
    }
}
