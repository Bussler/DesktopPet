using System.Diagnostics;
using System.IO;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using DesktopPet.Core;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class FocusInteractionTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [TestMethod]
    public void WindowControlsPreserveCountdownOnMinimizeAndReuseAndStopOnClose()
    {
        long clock = 0;
        using var focus = new FocusApplication(System.Windows.Threading.Dispatcher.CurrentDispatcher, "light", UiStyleCatalog.StickyNotes, () => clock,
            playCompletionSound: () => { });
        focus.Show();
        var window = GetWindow(focus);
        Assert.IsFalse(window.Topmost);
        Assert.AreEqual("25:00", Text(window, "CountdownText"));
        Assert.AreEqual("_Start focus", Button(window, "PrimaryButton").Content);
        Assert.AreEqual(Visibility.Collapsed, Button(window, "StopButton").Visibility);
        Assert.AreEqual(Visibility.Collapsed, Button(window, "SkipButton").Visibility);
        Input(window, "FocusInput").Text = "1";
        Input(window, "BreakInput").Text = "2";
        Click(window, "PrimaryButton");
        Assert.AreEqual("_Pause", Button(window, "PrimaryButton").Content);
        Assert.AreEqual(Visibility.Visible, Button(window, "StopButton").Visibility);
        clock += Stopwatch.Frequency * 10 + Stopwatch.Frequency / 2;
        focus.Tick();
        Assert.AreEqual("00:50", Text(window, "CountdownText"));
        Click(window, "PrimaryButton");
        var paused = focus.Session.Remaining;
        clock += Stopwatch.Frequency * 500;
        focus.Tick();
        Assert.AreEqual(paused, focus.Session.Remaining);
        Assert.AreEqual("Focus paused", Text(window, "PhaseText"));
        Assert.AreEqual("_Resume", Button(window, "PrimaryButton").Content);
        ((CheckBox)window.FindName("TopmostCheckBox")).IsChecked = true;
        window.WindowState = WindowState.Minimized;
        focus.Show();
        Assert.AreSame(window, GetWindow(focus));
        Assert.AreEqual(WindowState.Normal, window.WindowState);
        Assert.IsTrue(window.Topmost);
        Click(window, "PrimaryButton");
        window.WindowState = WindowState.Minimized;
        clock += Stopwatch.Frequency * 50;
        focus.Tick();
        Assert.AreEqual(FocusPhase.FocusFinished, focus.Session.Phase);
        Assert.AreEqual("_Take break", Button(window, "PrimaryButton").Content);
        Assert.AreEqual(Visibility.Collapsed, Button(window, "SkipButton").Visibility);
        Assert.AreEqual(WindowState.Minimized, window.WindowState, "Focus completion only waves.");
        focus.Show();
        Click(window, "PrimaryButton");
        Assert.AreEqual(TimeSpan.FromMinutes(2), focus.Session.Remaining);
        Assert.AreEqual("_Pause", Button(window, "PrimaryButton").Content);
        Assert.AreEqual(Visibility.Visible, Button(window, "SkipButton").Visibility);
        Click(window, "PrimaryButton");
        Assert.AreEqual("_Resume", Button(window, "PrimaryButton").Content);
        Assert.AreEqual(Visibility.Visible, Button(window, "SkipButton").Visibility);
        Click(window, "SkipButton");
        Assert.AreEqual("Break finished", Text(window, "PhaseText"));
        Assert.AreEqual("_Start focus", Button(window, "PrimaryButton").Content);
        Assert.AreEqual(Visibility.Collapsed, Button(window, "SkipButton").Visibility);
        Assert.AreEqual(PetAction.Sleep, focus.Session.PetActionOverride);
        Click(window, "PrimaryButton");
        Click(window, "StopButton");
        Assert.AreEqual(FocusPhase.Idle, focus.Session.Phase);
        Click(window, "PrimaryButton");
        window.Close();
        Assert.AreEqual(FocusPhase.Idle, focus.Session.Phase);
        focus.Show();
        var reopened = GetWindow(focus);
        Assert.AreNotSame(window, reopened);
        Assert.IsTrue(reopened.Topmost);
        Assert.AreEqual("1", Input(reopened, "FocusInput").Text);
        Assert.AreEqual("2", Input(reopened, "BreakInput").Text);
    }

    [TestMethod]
    [DataRow("")] [DataRow("abc")] [DataRow("0")] [DataRow("-1")] [DataRow("1.5")]
    [DataRow("NaN")] [DataRow("Infinity")] [DataRow("1441")] [DataRow("999999999999999999")]
    public void DurationValidationExplainsInvalidInputAndDisablesPhaseStart(string invalid)
    {
        using var focus = NewFocus();
        focus.Show();
        var window = GetWindow(focus);
        Input(window, "FocusInput").Text = invalid;
        Assert.IsFalse(Button(window, "PrimaryButton").IsEnabled);
        StringAssert.Contains(Text(window, "ValidationText"), "whole minutes");
        Assert.AreEqual(25, focus.Session.FocusMinutes);
        Input(window, "FocusInput").Text = "3";
        Assert.IsTrue(Button(window, "PrimaryButton").IsEnabled);
        Click(window, "PrimaryButton");
        var remaining = focus.Session.Remaining;
        Input(window, "FocusInput").Text = "4";
        Assert.AreEqual(remaining, focus.Session.Remaining);
        Assert.AreEqual(4, focus.Session.FocusMinutes);
        Input(window, "BreakInput").Text = invalid;
        Assert.IsTrue(Button(window, "PrimaryButton").IsEnabled, "Invalid future settings must not prevent Pause or Resume.");
        Click(window, "PrimaryButton");
        Assert.IsTrue(focus.Session.IsPaused);
        Click(window, "PrimaryButton");
        Assert.IsFalse(focus.Session.IsPaused);
        Assert.AreEqual(remaining, focus.Session.Remaining);
        focus.Session.Advance(remaining);
        Assert.IsFalse(Button(window, "PrimaryButton").IsEnabled);
        Input(window, "BreakInput").Text = "2";
        Click(window, "PrimaryButton");
        Assert.AreEqual(FocusPhase.Break, focus.Session.Phase);
        Assert.AreEqual(TimeSpan.FromMinutes(2), focus.Session.Remaining);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PrimaryPauseAtExpiryCannotStartTheNextPhase(bool duringBreak)
    {
        long clock = 0;
        using var focus = new FocusApplication(System.Windows.Threading.Dispatcher.CurrentDispatcher, "light", UiStyleCatalog.StickyNotes,
            () => clock, playCompletionSound: () => { });
        focus.Show();
        focus.Session.Configure(1, 1);
        var window = GetWindow(focus);
        Click(window, "PrimaryButton");
        if (duringBreak)
        {
            clock += Stopwatch.Frequency * 60;
            focus.Tick();
            Click(window, "PrimaryButton");
        }
        Assert.AreEqual("_Pause", Button(window, "PrimaryButton").Content);
        clock += Stopwatch.Frequency * 60;
        Click(window, "PrimaryButton");
        Assert.AreEqual(duringBreak ? FocusPhase.BreakFinished : FocusPhase.FocusFinished, focus.Session.Phase);
        Assert.AreEqual(TimeSpan.Zero, focus.Session.Remaining);
        Assert.IsFalse(focus.Session.IsPaused);
        Assert.AreEqual(duringBreak ? "_Start focus" : "_Take break", Button(window, "PrimaryButton").Content);
    }

    [TestMethod]
    public void BreakExpiryRestoresWithoutChangingTopmostAndLifecycleResetCancels()
    {
        long clock = 0;
        using var focus = new FocusApplication(System.Windows.Threading.Dispatcher.CurrentDispatcher, "dark", UiStyleCatalog.StickyNotes, () => clock,
            playCompletionSound: () => { });
        focus.Show();
        focus.Session.Configure(1, 1);
        focus.Execute(focus.Session.StartFocus);
        clock += Stopwatch.Frequency * 60;
        focus.Tick();
        focus.Execute(focus.Session.TakeBreak);
        var window = GetWindow(focus);
        window.WindowState = WindowState.Minimized;
        clock += Stopwatch.Frequency * 60;
        focus.Tick();
        Assert.AreEqual(WindowState.Normal, window.WindowState);
        Assert.IsFalse(window.Topmost);
        Assert.AreEqual("Break finished", Text(window, "PhaseText"));
        Assert.AreEqual("00:00", Text(window, "CountdownText"));
        typeof(FocusApplication).GetMethod("OnPowerModeChanged", Private)!
            .Invoke(focus, [new object(), new Microsoft.Win32.PowerModeChangedEventArgs(Microsoft.Win32.PowerModes.Suspend)]);
        PumpDispatcher();
        clock += Stopwatch.Frequency * 10000;
        focus.Tick();
        Assert.AreEqual(FocusPhase.Idle, focus.Session.Phase);
        Assert.IsNull(focus.Session.PetActionOverride);
        focus.Session.StartFocus();
        typeof(FocusApplication).GetMethod("OnPowerModeChanged", Private)!
            .Invoke(focus, [new object(), new Microsoft.Win32.PowerModeChangedEventArgs(Microsoft.Win32.PowerModes.Resume)]);
        PumpDispatcher();
        Assert.AreEqual(FocusPhase.Idle, focus.Session.Phase);
        using var fresh = NewFocus();
        fresh.Show();
        Assert.AreEqual(25, fresh.Session.FocusMinutes);
        Assert.AreEqual(5, fresh.Session.BreakMinutes);
        Assert.IsFalse(GetWindow(fresh).Topmost);
    }

    [TestMethod]
    public void DefaultCompletionSoundLoadsTheApprovedRippleForAsynchronousPlayback()
    {
        using var focus = new FocusApplication(System.Windows.Threading.Dispatcher.CurrentDispatcher, "light", UiStyleCatalog.StickyNotes, () => 0);
        var play = (Action)typeof(FocusApplication).GetField("_playCompletionSound", Private)!.GetValue(focus)!;
        Assert.IsInstanceOfType<SoundPlayer>(play.Target);
        var player = (SoundPlayer)play.Target!;
        Assert.AreEqual(nameof(SoundPlayer.Play), play.Method.Name);
        Assert.IsTrue(player.IsLoadCompleted);
        Assert.IsNotNull(player.Stream);
        player.Stream.Position = 0;
        Assert.AreEqual("ACA352C92003E0687BA2B270F508D838F64E3612C5A09A8E28AAB0AB9743CDBA",
            Convert.ToHexString(SHA256.HashData(player.Stream)), "Ship the exact approved happier Little Ripple WAV.");
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void NaturalExpirySoundsOnceForEachPhaseIncludingWhileMinimized(int finalSeconds)
    {
        long clock = 0;
        var sounds = 0;
        using var focus = new FocusApplication(System.Windows.Threading.Dispatcher.CurrentDispatcher, "light", UiStyleCatalog.StickyNotes,
            () => clock, playCompletionSound: () => sounds++);
        focus.Show();
        focus.Session.Configure(1, 1);
        focus.Execute(focus.Session.StartFocus);
        var window = GetWindow(focus);
        foreach (var phase in new[] { FocusPhase.Focus, FocusPhase.Break })
        {
            window.WindowState = WindowState.Minimized;
            var before = sounds;
            clock += Stopwatch.Frequency * 59;
            focus.Tick();
            Assert.AreEqual(before, sounds);
            focus.Execute(focus.Session.TogglePause);
            clock += Stopwatch.Frequency * 200;
            focus.Tick();
            Assert.AreEqual(before, sounds, "Paused time cannot produce a completion sound.");
            focus.Execute(focus.Session.TogglePause);
            clock += Stopwatch.Frequency * finalSeconds;
            focus.Tick();
            Assert.AreEqual(before + 1, sounds);
            Assert.AreEqual(phase == FocusPhase.Focus ? FocusPhase.FocusFinished : FocusPhase.BreakFinished, focus.Session.Phase);
            Assert.AreEqual(phase == FocusPhase.Focus ? WindowState.Minimized : WindowState.Normal, window.WindowState);
            clock += Stopwatch.Frequency * 200;
            focus.Tick();
            focus.Session.HandlePetClick();
            focus.Show();
            Assert.AreEqual(before + 1, sounds, "Waiting, acknowledging and reopening must not repeat the sound.");
            if (phase == FocusPhase.Focus) focus.Execute(focus.Session.TakeBreak);
        }
        Assert.AreEqual(2, sounds);
    }

    [TestMethod]
    public void StoppingClosingResettingAndSkippingDoNotPlayCompletionSounds()
    {
        long clock = 0;
        var sounds = 0;
        using var focus = new FocusApplication(System.Windows.Threading.Dispatcher.CurrentDispatcher, "light", UiStyleCatalog.StickyNotes,
            () => clock, playCompletionSound: () => sounds++);
        focus.Show();
        focus.Session.Configure(1, 1);
        focus.Execute(focus.Session.StartFocus);
        focus.Execute(focus.Session.Stop);
        focus.Execute(focus.Session.StartFocus);
        focus.Reset();
        focus.Execute(focus.Session.StartFocus);
        GetWindow(focus).Close();
        Assert.AreEqual(0, sounds);
        focus.Show();
        focus.Execute(focus.Session.StartFocus);
        clock += Stopwatch.Frequency * 60;
        focus.Tick();
        Assert.AreEqual(1, sounds);
        focus.Execute(focus.Session.TakeBreak);
        focus.Execute(focus.Session.TogglePause);
        focus.Execute(focus.Session.SkipBreak);
        clock += Stopwatch.Frequency * 200;
        focus.Tick();
        focus.Dispose();
        Assert.AreEqual(1, sounds, "Skip break and cleanup are not natural countdown completions.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BreakExpiryComesAboveCoveringWindowWhenActivationIsDenied(bool topmost)
    {
        long clock = 0;
        var denyActivation = false;
        var deniedAttempts = 0;
        var cover = new Window { Title = "Focus activation regression", ShowActivated = false, ShowInTaskbar = false,
            Width = 410, Height = 640, Topmost = topmost };
        using var focus = new FocusApplication(System.Windows.Threading.Dispatcher.CurrentDispatcher, "light", UiStyleCatalog.StickyNotes,
            () => clock, activateWindow: window =>
            {
                if (!denyActivation) return true;
                // Model Windows leaving the other window above Focus when activation is refused.
                cover.Topmost = !topmost;
                cover.Topmost = topmost;
                Assert.IsTrue(IsAbove(cover, window), "The denial must leave Focus covered before the fallback.");
                deniedAttempts++;
                return false;
            }, playCompletionSound: () => { });
        try
        {
            focus.Show();
            var window = GetWindow(focus);
            ((CheckBox)window.FindName("TopmostCheckBox")).IsChecked = topmost;
            focus.Session.Configure(1, 1);
            focus.Execute(focus.Session.StartFocus);
            clock += Stopwatch.Frequency * 60;
            focus.Tick();
            focus.Execute(focus.Session.TakeBreak);
            cover.Left = window.Left;
            cover.Top = window.Top;
            cover.Show();
            window.WindowState = WindowState.Minimized;
            denyActivation = true;

            clock += Stopwatch.Frequency * 60;
            focus.Tick();

            Assert.AreEqual(1, deniedAttempts);
            Assert.AreEqual(FocusPhase.BreakFinished, focus.Session.Phase);
            Assert.AreEqual(WindowState.Normal, window.WindowState);
            Assert.IsTrue(IsAbove(window, cover), "Break finished must be visible above the covering window despite denied activation.");
            Assert.AreEqual(topmost, window.Topmost);
            Assert.AreEqual(topmost, ((CheckBox)window.FindName("TopmostCheckBox")).IsChecked);
            Assert.AreEqual(PetAction.Sleep, focus.Session.PetActionOverride);
            window.Close();
            denyActivation = false;
            focus.Show();
            Assert.AreEqual(topmost, GetWindow(focus).Topmost, "The temporary raise must not change the in-memory preference.");
        }
        finally { cover.Close(); }
    }

    [TestMethod]
    public void AutomaticNotificationsFollowRealControlsAndDoNotRunDuringBreaks()
    {
        long clock = 0;
        var profile = FocusNotifications.Unrestricted;
        var writes = new List<string>();
        var notifications = new FocusNotifications(() => profile, value => { writes.Add(value); profile = value; });
        using var focus = new FocusApplication(System.Windows.Threading.Dispatcher.CurrentDispatcher, "light", UiStyleCatalog.StickyNotes,
            () => clock, playCompletionSound: () => { }, notifications: notifications);
        focus.Show();
        var window = GetWindow(focus);
        var option = (CheckBox)window.FindName("AutomaticNotificationsCheckBox");
        Assert.IsFalse(option.IsChecked);
        focus.Session.Configure(1, 1);
        Click(window, "PrimaryButton");
        Assert.AreEqual(0, writes.Count, "Automatic control is opt-in.");
        option.IsChecked = true;
        Assert.AreEqual(FocusNotifications.PriorityOnly, profile);
        window.WindowState = WindowState.Minimized;
        clock += Stopwatch.Frequency * 10;
        focus.Tick();
        Assert.AreEqual(1, writes.Count, "Minimizing and countdown ticks retain the same acquisition.");
        Click(window, "PrimaryButton");
        Assert.AreEqual(FocusNotifications.Unrestricted, profile, "Pause restores notifications.");
        Click(window, "PrimaryButton");
        Assert.AreEqual(FocusNotifications.PriorityOnly, profile, "Resume reacquires DND.");
        clock += Stopwatch.Frequency * 50;
        focus.Tick();
        Assert.AreEqual(FocusPhase.FocusFinished, focus.Session.Phase);
        Assert.AreEqual(FocusNotifications.Unrestricted, profile);
        var beforeBreak = writes.Count;
        Click(window, "PrimaryButton");
        Click(window, "PrimaryButton");
        Click(window, "PrimaryButton");
        Click(window, "SkipButton");
        Assert.AreEqual(beforeBreak, writes.Count, "Break controls never acquire DND.");
        Click(window, "PrimaryButton");
        option.IsChecked = false;
        Assert.AreEqual(FocusNotifications.Unrestricted, profile, "Unchecking immediately releases DND.");
    }

    [TestMethod]
    [DataRow("Stop")]
    [DataRow("Close")]
    [DataRow("Reset")]
    [DataRow("Dispose")]
    public void AutomaticNotificationsRestoreOnEveryCleanupPath(string cleanup)
    {
        var profile = FocusNotifications.Unrestricted;
        var notifications = new FocusNotifications(() => profile, value => profile = value);
        using var focus = new FocusApplication(System.Windows.Threading.Dispatcher.CurrentDispatcher, "light", UiStyleCatalog.StickyNotes,
            () => 0, playCompletionSound: () => { }, notifications: notifications);
        focus.Show();
        var window = GetWindow(focus);
        ((CheckBox)window.FindName("AutomaticNotificationsCheckBox")).IsChecked = true;
        Click(window, "PrimaryButton");
        Assert.AreEqual(FocusNotifications.PriorityOnly, profile);
        switch (cleanup)
        {
            case "Stop": Click(window, "StopButton"); break;
            case "Close": window.Close(); break;
            case "Reset": focus.Reset(); break;
            case "Dispose": focus.Dispose(); break;
        }
        Assert.AreEqual(FocusNotifications.Unrestricted, profile);
        if (cleanup == "Close")
        {
            focus.Show();
            Assert.IsTrue(((CheckBox)GetWindow(focus).FindName("AutomaticNotificationsCheckBox")).IsChecked);
            Click(GetWindow(focus), "PrimaryButton");
            Assert.AreEqual(FocusNotifications.PriorityOnly, profile);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void StopRetriesFailedNotificationRestoration(bool finishFocus)
    {
        long clock = 0;
        var profile = FocusNotifications.Unrestricted;
        var restoreAttempts = 0;
        var notifications = new FocusNotifications(() => profile, value =>
        {
            if (value == FocusNotifications.Unrestricted && ++restoreAttempts == 1) throw new COMException("Busy");
            profile = value;
        });
        using var focus = new FocusApplication(System.Windows.Threading.Dispatcher.CurrentDispatcher, "light", UiStyleCatalog.StickyNotes,
            () => clock, playCompletionSound: () => { }, notifications: notifications);
        focus.Show();
        var window = GetWindow(focus);
        focus.Session.Configure(1, 1);
        ((CheckBox)window.FindName("AutomaticNotificationsCheckBox")).IsChecked = true;
        Click(window, "PrimaryButton");
        if (finishFocus)
        {
            clock += Stopwatch.Frequency * 60;
            focus.Tick();
        }
        else Click(window, "PrimaryButton");
        Assert.AreEqual(FocusNotifications.PriorityOnly, profile);
        StringAssert.Contains(Text(window, "NotificationErrorText"), "Could not restore");
        clock += Stopwatch.Frequency;
        focus.Tick();
        Assert.AreEqual(1, restoreAttempts, "Countdown ticks must not repeatedly retry a failed restoration.");
        Click(window, "StopButton");
        Assert.AreEqual(2, restoreAttempts, "Explicit Stop must retry the outstanding restoration.");
        Assert.AreEqual(FocusNotifications.Unrestricted, profile);
        Assert.AreEqual(FocusPhase.Idle, focus.Session.Phase);
        Assert.AreEqual("", Text(window, "NotificationErrorText"));
    }

    [TestMethod]
    [DataRow(unchecked((int)0x80004005))] // E_FAIL
    [DataRow(unchecked((int)0x80004001))] // E_NOTIMPL
    public void AutomaticNotificationFailureIsVisibleWithOptionsCollapsedAndTimerContinues(int errorCode)
    {
        long clock = 0;
        var notifications = new FocusNotifications(() =>
        {
            Marshal.ThrowExceptionForHR(errorCode);
            return FocusNotifications.Unrestricted;
        });
        using var focus = new FocusApplication(System.Windows.Threading.Dispatcher.CurrentDispatcher, "light", UiStyleCatalog.StickyNotes,
            () => clock, playCompletionSound: () => { }, notifications: notifications);
        focus.Show();
        var window = GetWindow(focus);
        ((CheckBox)window.FindName("AutomaticNotificationsCheckBox")).IsChecked = true;
        Click(window, "PrimaryButton");
        Assert.IsFalse(((Expander)window.FindName("OptionsExpander")).IsExpanded);
        Assert.AreEqual(Visibility.Visible, ((TextBlock)window.FindName("NotificationErrorText")).Visibility);
        StringAssert.Contains(Text(window, "NotificationErrorText"), "Could not enable");
        Assert.AreEqual(FocusPhase.Focus, focus.Session.Phase);
        Assert.IsFalse(focus.Session.IsPaused);
        clock += Stopwatch.Frequency;
        focus.Tick();
        Assert.AreEqual(TimeSpan.FromMinutes(25) - TimeSpan.FromSeconds(1), focus.Session.Remaining);
    }

    [TestMethod]
    public void BothActualMenuRoutesReuseFocusWhileHiddenPetTimerIsStopped()
    {
        long clock = 0;
        using var focus = new FocusApplication(System.Windows.Threading.Dispatcher.CurrentDispatcher, "light", UiStyleCatalog.StickyNotes, () => clock,
            playCompletionSound: () => { });
        var app = System.Windows.Application.Current as App ?? new TestApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var fields = new[] { "_focus", "_trayIconImage" }.Select(name => typeof(App).GetField(name, Private)!).ToArray();
        var old = fields.Select(field => field.GetValue(app)).ToArray();
        fields[0].SetValue(app, focus);
        fields[1].SetValue(app, null);
        var assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), "hood");
        var session = HoodSession.Start(assets.Definition, [new("Primary", new(0, 0, 1920, 1080), true)], new FixedRandom(), new RecordingStore(),
            HoodConfiguration.Defaults with { Behavior = HoodConfiguration.Defaults.Behavior with { ShowSprites = false } });
        Action noop = () => { };
        var pet = new HoodOverlayWindow(session, assets, noop, noop, noop, noop, noop, noop);
        pet.AttachFocus(focus.Session);
        pet.FocusRequested += () => typeof(App).GetMethod("ShowFocus", Private)!.Invoke(app, null);
        System.Windows.Forms.NotifyIcon? tray = null;
        try
        {
            var menuItem = pet.ContextMenu!.Items.OfType<MenuItem>().Single(item => (string)item.Header == "_Focus");
            menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            var first = GetWindow(focus);
            Click(first, "PrimaryButton");
            first.WindowState = WindowState.Minimized;
            clock += Stopwatch.Frequency * 30;
            focus.Tick();
            var remaining = focus.Session.Remaining;
            tray = (System.Windows.Forms.NotifyIcon)typeof(App).GetMethod("CreateTrayIcon", Private)!.Invoke(app, null)!;
            tray.ContextMenuStrip!.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().Single(item => item.Text == "Focus").PerformClick();
            Assert.AreSame(first, GetWindow(focus));
            Assert.AreEqual(WindowState.Normal, first.WindowState);
            Assert.AreEqual(remaining, focus.Session.Remaining);
            Assert.IsFalse(pet.IsVisible);
            Assert.IsFalse(((System.Windows.Threading.DispatcherTimer)typeof(HoodOverlayWindow).GetField("_frameTimer", Private)!.GetValue(pet)!).IsEnabled);
            Assert.IsNull(((Image)pet.FindName("PetImage")).Source);
            Assert.AreEqual(TimeSpan.FromMinutes(25) - TimeSpan.FromSeconds(30), remaining);
            Assert.IsTrue(pet.Topmost, "Focus topmost defaults must not alter Hood's own setting.");
        }
        finally
        {
            tray?.Dispose();
            (fields[1].GetValue(app) as IDisposable)?.Dispose();
            for (var i = 0; i < fields.Length; i++) fields[i].SetValue(app, old[i]);
            pet.Close();
        }
    }

    [TestMethod]
    [DataRow("light", "sticky-notes")] [DataRow("dark", "sticky-notes")]
    [DataRow("light", "minimal")] [DataRow("dark", "minimal")]
    [DataRow("light", "soft")] [DataRow("dark", "soft")]
    [DataRow("light", "studio")] [DataRow("dark", "studio")]
    public void AppearanceAndKeyboardLabelsRemainAvailableAtMinimumWindowSize(string theme, string style)
    {
        using var focus = NewFocus();
        focus.Show();
        focus.ApplyTheme(theme, style);
        var window = GetWindow(focus);
        window.UpdateLayout();
        var options = (Expander)window.FindName("OptionsExpander");
        Assert.IsFalse(options.IsExpanded);
        Assert.IsTrue(window.ActualWidth <= 300 && window.ActualHeight <= 280, "The default timer must stay compact in every theme/style.");
        SavePreview(window, $"focus-{theme}-{style}-compact");
        options.IsExpanded = true;
        window.UpdateLayout();
        SavePreview(window, $"focus-{theme}-{style}-options");
        window.SizeToContent = SizeToContent.Manual;
        window.Width = window.MinWidth;
        window.Height = window.MinHeight;
        window.UpdateLayout();
        Assert.AreEqual(window.MinHeight, window.ActualHeight, 1, "Exercise the actual minimum height, with automatic sizing disabled as it is on manual resize.");
        Assert.AreEqual(window.FindResource("NotesSurfaceBrush"), window.Background);
        foreach (var name in new[] { "PrimaryButton", "AutomaticNotificationsCheckBox" })
        {
            var control = (Control)window.FindName(name);
            Assert.IsTrue(control.Focusable);
            Assert.IsTrue(control.ActualWidth <= window.ActualWidth);
            Assert.AreEqual(Visibility.Visible, control.Visibility);
        }
        Assert.AreEqual("Focus minutes", System.Windows.Automation.AutomationProperties.GetName(Input(window, "FocusInput")));
        Assert.AreEqual("Break minutes", System.Windows.Automation.AutomationProperties.GetName(Input(window, "BreakInput")));
        Assert.IsTrue(((ScrollViewer)window.Content).ScrollableHeight > 0, "Expanded options must remain reachable at minimum size.");
        options.IsExpanded = false;
        window.Width = 300;
        window.SizeToContent = SizeToContent.Height;
        focus.Session.StartFocus();
        focus.Session.Advance(focus.Session.Remaining);
        Click(window, "PrimaryButton");
        Click(window, "PrimaryButton");
        Assert.AreEqual("_Resume", Button(window, "PrimaryButton").Content);
        Assert.AreEqual(Visibility.Visible, Button(window, "SkipButton").Visibility);
        window.UpdateLayout();
        SavePreview(window, $"focus-{theme}-{style}-break");
    }

    // Optional WPF-rendered artifacts for local visual review; never shipped in the package.
    private static void SavePreview(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("HOOD_FOCUS_PREVIEW_DIRECTORY") is { Length: > 0 } directory)
        {
            // Let the Fluent expander's visual transition finish for optional screenshots only.
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            timer.Tick += (_, _) => frame.Continue = false;
            timer.Start();
            try { System.Windows.Threading.Dispatcher.PushFrame(frame); }
            finally { timer.Stop(); }
            window.UpdateLayout();
            var content = (FrameworkElement)window.Content;
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),
                (int)Math.Ceiling(content.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            Directory.CreateDirectory(directory);
            using var output = File.Create(Path.Combine(directory, name + ".png"));
            encoder.Save(output);
        }
    }

    [TestMethod]
    [DataRow("hood")] [DataRow("pip")] [DataRow("crumb")] [DataRow("puddle")]
    public void PetRemindersPreserveCareAndBreakClicksRestoreFocusAndToggleNotes(string petName)
    {
        var assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), petName);
        var store = new RecordingStore();
        var configuration = HoodConfiguration.Defaults with
        {
            Needs = HoodConfiguration.Defaults.Needs with { SleepPointsPerHour = 0 }
        };
        var session = HoodSession.Start(assets.Definition,
            [new("Primary", new(0, 0, 1920, 1080), true)], new FixedRandom(), store, configuration,
            new HoodSessionSnapshot { Position = new(500, 400), Needs = new(40, 40, 50), RemainingRandomInterval = TimeSpan.FromSeconds(5) });
        var notesClicks = 0;
        Action noop = () => { };
        var pet = new HoodOverlayWindow(session, assets, noop, noop, () => notesClicks++, noop, noop, noop);
        var focus = new FocusSession();
        var restores = 0;
        focus.RestoreRequested += () => restores++;
        pet.AttachFocus(focus);
        try
        {
            focus.StartFocus();
            PetClick(pet, session);
            Assert.AreEqual(1, notesClicks);
            session.Advance(TimeSpan.FromSeconds(2));
            focus.Advance(focus.Remaining);
            var wave = assets.Definition.GetAnimation(PetAction.Wave);
            pet.AdvanceFocusAnimation(TimeSpan.FromMilliseconds(wave.FrameDurationMilliseconds));
            Assert.AreEqual(1, pet.CurrentPresentation.FrameIndex);
            pet.AdvanceFocusAnimation(TimeSpan.FromMilliseconds(wave.FrameDurationMilliseconds * wave.Frames.Count * 3));
            Assert.AreEqual(1, pet.CurrentPresentation.FrameIndex, "A nonlooping source Wave must still loop as a reminder.");
            typeof(HoodOverlayWindow).GetMethod("RenderPresentation", Private)!.Invoke(pet, null);
            Assert.AreEqual(assets.GetFrameRectangle(wave.Frames[1]),
                ((System.Windows.Media.Imaging.CroppedBitmap)((Image)pet.FindName("PetImage")).Source).SourceRect);
            var position = session.Position;
            for (var i = 0; i < 5; i++)
            {
                session.Advance(TimeSpan.FromSeconds(4));
                pet.AdvanceFocusAnimation(TimeSpan.FromMilliseconds(1234));
                Assert.AreEqual(PetAction.Wave, pet.CurrentPresentation.Action);
                Assert.AreEqual(position, pet.CurrentPresentation.Position);
            }
            var beforeClick = session.Presentation.Action;
            PetClick(pet, session);
            Assert.AreEqual(1, notesClicks);
            Assert.AreEqual(1, restores);
            Assert.IsFalse(focus.WavePending);
            Assert.AreEqual(beforeClick, session.Presentation.Action);
            focus.TakeBreak();
            position = session.Position;
            var sleepNeed = session.Needs.Sleep;
            var hungerBeforeFeed = session.Needs.Hunger;
            session.Feed();
            Assert.AreEqual(hungerBeforeFeed - configuration.Needs.FeedGain, session.Needs.Hunger, 0.001,
                "Ordinary feeding still applies beneath the presentation.");
            session.Advance(TimeSpan.FromSeconds(30));
            Assert.AreEqual(PetAction.Sleep, pet.CurrentPresentation.Action);
            Assert.AreEqual(position, pet.CurrentPresentation.Position);
            Assert.AreEqual(sleepNeed, session.Needs.Sleep);
            Assert.AreEqual(HoodSessionMode.Awake, session.Mode);
            Assert.AreEqual(TimeSpan.Zero, session.RemainingNap);
            // Legitimate care naps can recover underneath the visual override and finish normally.
            session.Sleep();
            var remainingNap = session.RemainingNap;
            PetClick(pet, session);
            Assert.AreEqual(2, notesClicks);
            Assert.AreEqual(2, restores);
            Assert.AreEqual(HoodSessionMode.Sleeping, session.Mode);
            Assert.AreEqual(remainingNap, session.RemainingNap, "A break click must not wake an underlying care nap.");
            Assert.AreEqual(sleepNeed, session.Needs.Sleep);
            session.Advance(session.RemainingNap / 2);
            Assert.AreEqual(sleepNeed / 2, session.Needs.Sleep, 0.001);
            Assert.AreEqual(PetAction.Sleep, pet.CurrentPresentation.Action);
            session.Advance(session.RemainingNap);
            Assert.AreEqual(PetAction.Sleep, pet.CurrentPresentation.Action);
            Assert.AreEqual(HoodSessionMode.Awake, session.Mode);
            PetClick(pet, session);
            Assert.AreEqual(3, notesClicks);
            Assert.AreEqual(3, restores);
            focus.TogglePause();
            Assert.AreEqual(PetAction.Sleep, pet.CurrentPresentation.Action);
            PetClick(pet, session);
            Assert.AreEqual(4, notesClicks);
            Assert.AreEqual(4, restores);
            focus.SkipBreak();
            PetClick(pet, session);
            Assert.AreEqual(5, notesClicks);
            Assert.AreEqual(5, restores);
            Assert.AreEqual(PetAction.Sleep, pet.CurrentPresentation.Action);
            session.SaveCurrentState();
            Assert.AreEqual(HoodSessionMode.Awake, store.Snapshot!.Mode);
            Assert.AreNotEqual(PetAction.Sleep, store.Snapshot.Action);
            // A drag does not acknowledge a reminder or activate either window.
            var restoreCount = restores;
            PetClick(pet, session, dragged: true);
            Assert.AreEqual(restoreCount, restores);
            Assert.AreEqual(5, notesClicks);
            focus.StartFocus();
            Assert.AreEqual(session.Presentation, pet.CurrentPresentation);
            focus.Advance(focus.Remaining);
            focus.Stop();
            Assert.AreEqual(session.Presentation, pet.CurrentPresentation);
        }
        finally { pet.Close(); }
    }

    private static FocusApplication NewFocus() => new(System.Windows.Threading.Dispatcher.CurrentDispatcher, "light", UiStyleCatalog.StickyNotes, () => 0,
        playCompletionSound: () => { });
    private static FocusWindow GetWindow(FocusApplication focus) => (FocusWindow)typeof(FocusApplication).GetField("_window", Private)!.GetValue(focus)!;
    private static TextBox Input(Window window, string name) => (TextBox)window.FindName(name);
    private static Button Button(Window window, string name) => (Button)window.FindName(name);
    private static string Text(Window window, string name) => ((TextBlock)window.FindName(name)).Text;
    private static void Click(Window window, string name) => Button(window, name).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    private static void PetClick(HoodOverlayWindow pet, HoodSession session, bool dragged = false)
    {
        session.BeginDragging();
        typeof(HoodOverlayWindow).GetField("_trackingMouse", Private)!.SetValue(pet, true);
        typeof(HoodOverlayWindow).GetField("_dragged", Private)!.SetValue(pet, dragged);
        pet.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
    }
    private static void PumpDispatcher()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(() => frame.Continue = false, System.Windows.Threading.DispatcherPriority.ContextIdle);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
    private static bool IsAbove(Window upper, Window lower)
    {
        var upperHandle = new WindowInteropHelper(upper).Handle;
        var seen = new HashSet<IntPtr>();
        for (var handle = GetRelatedWindow(new WindowInteropHelper(lower).Handle, 3 /* GW_HWNDPREV */);
             handle != IntPtr.Zero && seen.Add(handle); handle = GetRelatedWindow(handle, 3))
        {
            if (handle == upperHandle) return true;
        }
        return false;
    }
    [DllImport("user32.dll", EntryPoint = "GetWindow")]
    private static extern IntPtr GetRelatedWindow(IntPtr handle, uint command);
    private sealed class FixedRandom : IPetRandomSource { public int NextIndex(int exclusiveUpperBound) => 0; }
    private sealed class TestApp : App { protected override void OnStartup(StartupEventArgs e) { } }
    private sealed class RecordingStore : ISessionStore
    {
        public HoodSessionSnapshot? Snapshot;
        public void Save(HoodSessionSnapshot snapshot) => Snapshot = snapshot;
    }
}
