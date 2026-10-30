using System.Net.Http;
using System.Windows;
using ClaudeUsageWidget.Core;

namespace ClaudeUsageWidget;

public partial class App : Application
{
    private const string MutexName = @"Local\ClaudeUsageWidget";
    private const string ShowEventName = @"Local\ClaudeUsageWidget.Show";

    private readonly SettingsStore _store = new(SettingsStore.DefaultPath);
    private readonly HttpClient _http = new();
    private readonly HotkeyManager _hotkey = new();
    private AppSettings _settings = new();
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showWait;
    private UsageMonitor? _monitor;
    private WidgetWindow? _widget;
    private TrayIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A second launch just asks the running instance to show its widget.
        _instanceMutex = new Mutex(true, MutexName, out var isFirst);
        if (!isFirst)
        {
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var existing))
            {
                existing.Set();
                existing.Dispose();
            }
            _instanceMutex.Dispose();
            _instanceMutex = null;
            Shutdown();
            return;
        }
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent,
            (_, _) => Dispatcher.BeginInvoke(() => SetWidgetVisible(true)), null, Timeout.Infinite, executeOnlyOnce: false);

        _settings = _store.Load();

        var client = new UsageClient(_http, CredentialsReader.DefaultPath, TimeProvider.System);
        _monitor = new UsageMonitor(client, TimeSpan.FromSeconds(_settings.PollSeconds), TimeProvider.System);

        _widget = new WidgetWindow();
        _widget.SetSavedPosition(_settings.Left, _settings.Top);
        _widget.Unlocked = _settings.PositionUnlocked;
        _widget.PositionChanged += (left, top) =>
        {
            _settings.Left = left;
            _settings.Top = top;
            _store.Save(_settings);
        };
        // ShutdownMode is explicit (the app lives in the tray), so a closed widget must end the app itself.
        _widget.Closed += (_, _) => Shutdown();

        _tray = new TrayIcon(
            toggle: () => SetWidgetVisible(!_widget.IsVisible),
            isVisible: () => _widget.IsVisible,
            refresh: _monitor.RefreshNow,
            getUnlocked: () => _widget.Unlocked,
            setUnlocked: SetUnlocked,
            getAutostart: Autostart.IsEnabled,
            setAutostart: SetAutostart,
            quit: Shutdown);

        _widget.Apply(_monitor.Current);
        _tray.Apply(_monitor.Current);
        _monitor.StateChanged += _widget.Apply;
        _monitor.StateChanged += _tray.Apply;

        RegisterHotkey();
        _monitor.Start();

        if (_settings.WidgetVisible)
            _widget.Show();
    }

    private void RegisterHotkey()
    {
        var text = _settings.Hotkey;
        if (!HotkeyParser.TryParse(text, out var mods, out var vk))
        {
            text = AppSettings.DefaultHotkey;
            HotkeyParser.TryParse(text, out mods, out vk);
        }

        if (!_hotkey.Register(_widget!.Handle, mods, vk, () => SetWidgetVisible(!_widget.IsVisible)))
            _tray!.ShowBalloon($"Hotkey {text} is in use by another app. Use the tray icon to show or hide the widget.");
    }

    private void SetWidgetVisible(bool visible)
    {
        if (_widget is null || _widget.IsVisible == visible)
            return;

        if (visible)
        {
            _widget.Show();
            _monitor!.RefreshNow();
        }
        else
        {
            _widget.Hide();
        }

        _settings.WidgetVisible = visible;
        _store.Save(_settings);
    }

    private void SetUnlocked(bool unlocked)
    {
        _widget!.Unlocked = unlocked;
        _settings.PositionUnlocked = unlocked;
        _store.Save(_settings);
    }

    private void SetAutostart(bool enabled)
    {
        Autostart.Set(enabled);
        _settings.StartWithWindows = enabled;
        _store.Save(_settings);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey.Dispose();
        _tray?.Dispose();
        _monitor?.Dispose();
        _showWait?.Unregister(null);
        _showEvent?.Dispose();
        _http.Dispose();
        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
