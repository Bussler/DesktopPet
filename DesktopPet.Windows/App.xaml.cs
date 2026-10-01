using System.Configuration;
using System.IO;
using System.Windows;
using DesktopPet.Core;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace DesktopPet.Windows;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private HoodSession? _session;
    private HoodOverlayWindow? _petWindow;
    private SettingsWindow? _settingsWindow;
    private Forms.NotifyIcon? _trayIcon;
    private readonly WindowsStartupRegistration _startupRegistration = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var storageDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HoodDesktopPet");
        var configurationPath = Path.Combine(storageDirectory, "config.yaml");
        try
        {
            var configurationStore = new YamlConfigurationStore(configurationPath);
            var configuration = configurationStore.LoadOrCreate();
            try
            {
                _startupRegistration.SetEnabled(configuration.Behavior.StartWithWindows);
            }
            catch (IOException exception)
            {
                System.Windows.MessageBox.Show(
                    $"Hood's saved Windows startup preference could not be applied. The app can still run, but startup may not match config.yaml. Open Settings and try again.\n\n{exception.Message}",
                    "Windows startup setting mismatch",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }

            var statePath = Path.Combine(storageDirectory, "state.json");
            var sessionStore = new JsonSessionStore(statePath);
            if (!TryLoadSessionState(sessionStore, out var savedState))
            {
                Shutdown();
                return;
            }

            var assets = HoodAssets.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Hood"));
            var displays = WindowsDisplayProvider.GetDisplays();
            try
            {
                _session = HoodSession.Start(
                    assets.Definition,
                    displays,
                    new SystemRandomSource(),
                    sessionStore,
                    configuration,
                    savedState);
            }
            catch (InvalidDataException exception) when (savedState is not null)
            {
                var invalidState = new SessionStateException(
                    statePath,
                    $"The saved animation state cannot be used with the packaged Hood assets. {exception.Message} Choose Reset saved session to archive it and start fresh.",
                    exception);
                if (!TryResetSessionState(sessionStore, invalidState))
                {
                    Shutdown();
                    return;
                }

                _session = HoodSession.Start(
                    assets.Definition,
                    displays,
                    new SystemRandomSource(),
                    sessionStore,
                    configuration);
            }

            _petWindow = new HoodOverlayWindow(_session, assets, ShowSettings, RequestQuit);
            _petWindow.Closed += (_, _) => RequestQuit();
            MainWindow = _petWindow;
            _petWindow.Show();
            _trayIcon = CreateTrayIcon();
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }
        catch (SessionStateException exception)
        {
            System.Windows.MessageBox.Show(
                $"Hood could not recover its saved session. {exception.Message}",
                "Hood session recovery failed",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown(1);
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                $"Hood could not start. Check the configuration at {configurationPath}, packaged assets, and display settings, then relaunch.\n\n{exception.Message}",
                "Hood could not start",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static bool TryLoadSessionState(JsonSessionStore store, out HoodSessionSnapshot? snapshot)
    {
        try
        {
            snapshot = store.Load();
            return true;
        }
        catch (SessionStateException exception)
        {
            snapshot = null;
            return TryResetSessionState(store, exception);
        }
    }

    private static bool TryResetSessionState(JsonSessionStore store, SessionStateException exception)
    {
        var recoveryWindow = new StateRecoveryWindow(exception.StatePath, exception.Message);
        if (recoveryWindow.ShowDialog() != true)
        {
            return false;
        }

        store.ResetAfterAcknowledgement();
        return true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _trayIcon?.Dispose();
        _trayIcon = null;
        base.OnExit(e);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_session is null || _petWindow is null)
            {
                return;
            }

            try
            {
                _session.UpdateDisplays(WindowsDisplayProvider.GetDisplays());
                _petWindow.ApplyPlacement();
            }
            catch (SessionStateException exception)
            {
                System.Windows.MessageBox.Show(
                    $"Hood moved to a usable display, but the new placement was not saved. The previous state file was retained.\n\n{exception.Message}",
                    "Hood placement save failed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            catch (Exception exception)
            {
                System.Windows.MessageBox.Show(
                    $"Hood could not recover its placement after the display layout changed.\n\n{exception.Message}",
                    "Hood display recovery failed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        });
    }

    private Forms.NotifyIcon CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        var settings = new Forms.ToolStripMenuItem("Settings");
        settings.Click += (_, _) => ShowSettings();
        var quit = new Forms.ToolStripMenuItem("Quit");
        quit.Click += (_, _) => RequestQuit();
        menu.Items.Add(settings);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(quit);

        var icon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Hood",
            ContextMenuStrip = menu,
            Visible = true
        };
        icon.DoubleClick += (_, _) => ShowSettings();
        return icon;
    }

    private void ShowSettings()
    {
        if (_session is null || _petWindow is null)
        {
            return;
        }

        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        var store = new YamlConfigurationStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HoodDesktopPet",
            "config.yaml"));
        _settingsWindow = new SettingsWindow(new HoodConfigurationController(_session, store, _startupRegistration), _petWindow);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void RequestQuit()
    {
        if (_session is { QuitRequested: false })
        {
            try
            {
                _session.Quit();
            }
            catch (Exception exception)
            {
                System.Windows.MessageBox.Show(
                    $"Hood could not save the session. The app will still exit.\n\n{exception.Message}",
                    "Hood session save failed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        _trayIcon?.Dispose();
        _trayIcon = null;
        Shutdown();
    }
}

