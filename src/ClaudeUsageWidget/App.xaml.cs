using System.Net.Http;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using ClaudeUsageWidget.Core;
using Microsoft.Win32;

namespace ClaudeUsageWidget;

public partial class App : Application
{
    private const string MutexName = @"Local\ClaudeUsageWidget";
    private const string ShowEventName = @"Local\ClaudeUsageWidget.Show";
    private static readonly TimeSpan HealthCheckInterval = TimeSpan.FromSeconds(15);

    private readonly SettingsStore _store = new(SettingsStore.DefaultPath);
    private readonly HttpClient _http = new();
    private readonly FileLog _log = new(FileLog.DefaultPath, maxBytes: 1024 * 1024, TimeProvider.System, TimeZoneInfo.Local);
    private readonly HotkeyManager _hotkey = new();
    private readonly uint _taskbarCreated = NativeMethods.RegisterWindowMessage("TaskbarCreated");
    private AppSettings _settings = new();
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showWait;
    private UsageMonitor? _monitor;
    private WidgetWindow? _widget;
    private TrayIcon? _tray;
    private DispatcherTimer? _healthCheck;
    private string? _lastHealth;
    private string _exitReason = "shutdown";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A second launch just asks the running instance to show its widget.
        _instanceMutex = new Mutex(true, MutexName, out var isFirst);
        if (!isFirst)
        {
            var signalled = EventWaitHandle.TryOpenExisting(ShowEventName, out var existing);
            if (signalled)
            {
                existing!.Set();
                existing.Dispose();
            }
            _log.Write($"second launch (pid {Environment.ProcessId}): "
                       + (signalled ? "asked the running widget to show" : "running widget has no show event") + ", quitting");
            _instanceMutex.Dispose();
            _instanceMutex = null;
            _exitReason = "";
            Shutdown();
            return;
        }
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent,
            (_, _) => Dispatcher.BeginInvoke(() => SetWidgetVisible(true, "second launch")), null, Timeout.Infinite, executeOnlyOnce: false);

        DispatcherUnhandledException += (_, args) => _log.Write($"unhandled UI exception {args.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, args) => _log.Write($"unhandled exception {args.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, args) => _log.Write($"unobserved task exception {args.Exception}");

        _settings = _store.Load();
        _log.Write($"started v{typeof(App).Assembly.GetName().Version} (pid {Environment.ProcessId}), polling every {_settings.PollSeconds}s");

        var client = new UsageClient(_http, CredentialsReader.DefaultPath, TimeProvider.System);
        _monitor = new UsageMonitor(client, TimeSpan.FromSeconds(_settings.PollSeconds), TimeProvider.System, _log)
        {
            AlwaysShowEstimate = _settings.AlwaysShowEstimate,
        };

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
        _widget.Closed += (_, _) =>
        {
            _exitReason = "widget window closed";
            Shutdown();
        };

        _tray = new TrayIcon(
            toggle: () => SetWidgetVisible(!_widget.IsVisible, "tray"),
            isVisible: () => _widget.IsVisible,
            refresh: () => _monitor.RefreshNow("tray"),
            getUnlocked: () => _widget.Unlocked,
            setUnlocked: SetUnlocked,
            getAutostart: Autostart.IsEnabled,
            setAutostart: SetAutostart,
            getAlwaysEstimate: () => _monitor.AlwaysShowEstimate,
            setAlwaysEstimate: SetAlwaysShowEstimate,
            quit: () =>
            {
                _exitReason = "quit from tray";
                Shutdown();
            });

        _widget.Apply(_monitor.Current);
        _tray.Apply(_monitor.Current);
        _monitor.StateChanged += _widget.Apply;
        _monitor.StateChanged += _tray.Apply;

        RegisterHotkey();
        HwndSource.FromHwnd(_widget.Handle).AddHook(WatchShellMessages);
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.SessionEnding += OnSessionEnding;
        _monitor.Start();

        if (_settings.WidgetVisible)
            _widget.Show();

        // Log what Windows says the user can see, whenever it changes, so a vanished widget leaves a trace.
        _healthCheck = new DispatcherTimer(HealthCheckInterval, DispatcherPriority.Background, (_, _) => LogHealthIfChanged(), Dispatcher);
        _healthCheck.Start();
        LogHealthIfChanged();
    }

    private string Health() =>
        $"{WindowProbe.Describe(_widget!.Handle)} wpf-visible={_widget.IsVisible} {_tray!.Probe()}";

    private void LogHealthIfChanged()
    {
        var health = Health();
        if (health == _lastHealth)
            return;
        _lastHealth = health;
        _log.Write($"state: {health}");
    }

    private nint WatchShellMessages(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        // Explorer broadcasts this after it restarts; NotifyIcon re-adds itself, and the log shows when it happened.
        if (msg == _taskbarCreated)
            _log.Write("taskbar recreated (Explorer restarted)");
        return 0;
    }

    // SystemEvents handlers run on their own thread: log there, probe the window on the dispatcher.
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        _log.Write($"power: {e.Mode}");
        if (e.Mode == PowerModes.Resume)
            Dispatcher.BeginInvoke(() => _monitor?.OnResume());
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        _log.Write("display settings changed");
        Dispatcher.BeginInvoke(LogHealthIfChanged);
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e) => _log.Write($"session: {e.Reason}");

    private void OnSessionEnding(object sender, SessionEndingEventArgs e)
    {
        _exitReason = $"Windows session ending ({e.Reason})";
        _log.Write(_exitReason);
    }

    private void RegisterHotkey()
    {
        var text = _settings.Hotkey;
        if (!HotkeyParser.TryParse(text, out var mods, out var vk))
        {
            text = AppSettings.DefaultHotkey;
            HotkeyParser.TryParse(text, out mods, out vk);
        }

        if (!_hotkey.Register(_widget!.Handle, mods, vk, () => SetWidgetVisible(!_widget.IsVisible, "hotkey")))
        {
            _log.Write($"hotkey {text} is in use by another app");
            _tray!.ShowBalloon($"Hotkey {text} is in use by another app. Use the tray icon to show or hide the widget.");
        }
    }

    private void SetWidgetVisible(bool visible, string source)
    {
        if (_widget is null)
            return;
        if (_widget.IsVisible == visible)
        {
            _log.Write($"{(visible ? "show" : "hide")} ({source}) ignored, already {(visible ? "visible" : "hidden")}: {Health()}");
            return;
        }

        _log.Write($"{(visible ? "show" : "hide")} ({source})");
        if (visible)
        {
            _widget.Show();
            _monitor!.RefreshNow("show");
        }
        else
        {
            _widget.Hide();
        }

        _settings.WidgetVisible = visible;
        _store.Save(_settings);
        LogHealthIfChanged();
    }

    private void SetUnlocked(bool unlocked)
    {
        _widget!.Unlocked = unlocked;
        _settings.PositionUnlocked = unlocked;
        _store.Save(_settings);
    }

    private void SetAlwaysShowEstimate(bool enabled)
    {
        _monitor!.AlwaysShowEstimate = enabled;
        _settings.AlwaysShowEstimate = enabled;
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
        _healthCheck?.Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.SessionEnding -= OnSessionEnding;
        _hotkey.Dispose();
        _tray?.Dispose();
        _monitor?.Dispose();
        _showWait?.Unregister(null);
        _showEvent?.Dispose();
        _http.Dispose();
        if (_exitReason.Length > 0)
            _log.Write($"exiting: {_exitReason} (code {e.ApplicationExitCode})");
        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
