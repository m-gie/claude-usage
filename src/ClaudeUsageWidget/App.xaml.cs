using System.Net.Http;
using System.Windows;
using ClaudeUsageWidget.Core;

namespace ClaudeUsageWidget;

public partial class App : Application
{
    private readonly SettingsStore _store = new(SettingsStore.DefaultPath);
    private readonly HttpClient _http = new();
    private AppSettings _settings = new();
    private UsageMonitor? _monitor;
    private WidgetWindow? _widget;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
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
        _widget.Apply(_monitor.Current);
        _monitor.StateChanged += _widget.Apply;
        _monitor.Start();

        if (_settings.WidgetVisible)
            _widget.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitor?.Dispose();
        _http.Dispose();
        base.OnExit(e);
    }
}
