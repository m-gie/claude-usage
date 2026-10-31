# Claude Usage Widget

A small always-on-top overlay for Windows that shows how much of your Claude **5-hour session limit** you've used, when it resets, and — at your current pace — when you'll hit it.

- Ring gauge with the session %, colour-coded (blue < 70%, orange 70–89%, red 90%+)
- Reset time, countdown, and a `full ~HH:MM` prediction when you're on track to hit the limit before the reset
- Click-through: it never takes focus or blocks the windows underneath
- Tray icon with the usage in its tooltip; click to show/hide the widget
- Global hotkey to show/hide (default `Ctrl+Alt+U`)

## Requirements

- Windows 10/11
- .NET 10 SDK (to build) or .NET 10 Desktop Runtime (to run a framework-dependent build)
- Claude Code installed and logged in with a Claude subscription

## How it gets the data

The widget reads Claude Code's login token from `%USERPROFILE%\.claude\.credentials.json` and asks `https://api.anthropic.com/api/oauth/usage` — the same endpoint behind Claude Code's `/usage` screen — about once a minute.

- The token is only read. The widget never refreshes it, never writes the file, and never logs it.
- Claude Code renews the token while you use it. If it has expired, the widget greys out and shows "Open Claude Code to refresh".
- The endpoint is undocumented and could change without notice. If it does, the widget greys out and keeps the last known values.

## Build and run

```
dotnet test
dotnet run --project src/ClaudeUsageWidget
```

Publish a single exe:

```
dotnet publish src/ClaudeUsageWidget -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

The output lands in `src/ClaudeUsageWidget/bin/Release/net10.0-windows/win-x64/publish/`.

## Using it

- **Move the widget:** tray menu → *Unlock position*, drag it, then lock it again.
- **Tray icon hidden?** New tray icons start in the overflow (^) menu. Drag it onto the taskbar to keep it visible.
- **Start with Windows:** tray menu → *Start with Windows*.
- Launching the app again just shows the running widget.

## Settings

`%APPDATA%\ClaudeUsageWidget\settings.json` — read at startup:

| Key | Default | Notes |
|---|---|---|
| `Hotkey` | `Ctrl+Alt+U` | Modifiers `Ctrl`, `Alt`, `Shift`, `Win` plus one key `A`–`Z`, `0`–`9` or `F1`–`F24` |
| `PollSeconds` | `180` | Minimum 120. The usage endpoint rate-limits frequent polling |
| `Left`, `Top` | – | Widget position; saved when you drag it |
| `WidgetVisible` | `true` | |
| `PositionUnlocked` | `false` | |
| `AlwaysShowEstimate` | `false` | Managed from the tray menu. Also shows the estimated limit hit when it lands after the reset (green), or *estimating…* while there's no pace yet |
| `StartWithWindows` | `false` | Managed from the tray menu |

## Troubleshooting

`%LOCALAPPDATA%\ClaudeUsageWidget\widget.log` records every fetch (status, HTTP code, `Retry-After`, next poll, error body), start and exit with the reason, refreshes, show/hide and what triggered it, second launches, sleep/resume, lock/unlock, display changes, Explorer restarts and unhandled exceptions. A `state:` line is written whenever what Windows reports about the widget changes: window visible or cloaked, its position and whether that is on a monitor, and whether the tray icon is still there. A `started` line with no `exiting` line before it means the previous run was killed or crashed. The log rotates to `widget.log.1` at 1 MB. The token is never logged.

When the API rate-limits the widget (HTTP 429) or errors (5xx), it keeps the last reading, shows *Rate limited · updated HH:mm* or *Server error · updated HH:mm*, and backs off (doubling up to 10 min, or longer if the server sends `Retry-After`). The endpoint rate-limits even requests seconds apart, so *Refresh now*, showing the widget and resume from sleep are skipped within a minute of the last fetch and for the whole backoff.
