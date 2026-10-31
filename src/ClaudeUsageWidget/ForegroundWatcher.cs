using System.Runtime.InteropServices;

namespace ClaudeUsageWidget;

/// <summary>Calls back on the UI thread whenever another window becomes the foreground window.</summary>
public sealed partial class ForegroundWatcher : IDisposable
{
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    private delegate void WinEventProc(nint hook, uint evt, nint hwnd, int idObject, int idChild, uint thread, uint time);

    private readonly WinEventProc _proc; // keeps the callback alive while the hook is installed
    private nint _hook;

    // Must be created on a thread with a message loop (the WPF UI thread); events arrive there.
    public ForegroundWatcher(Action onForegroundChanged)
    {
        _proc = (_, _, _, _, _, _, _) => onForegroundChanged();
        _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, 0,
            Marshal.GetFunctionPointerForDelegate(_proc), 0, 0, WINEVENT_OUTOFCONTEXT);
    }

    public void Dispose()
    {
        if (_hook == 0)
            return;
        UnhookWinEvent(_hook);
        _hook = 0;
    }

    [LibraryImport("user32.dll")]
    private static partial nint SetWinEventHook(uint eventMin, uint eventMax, nint hmod, nint proc, uint pid, uint thread, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWinEvent(nint hook);
}
