# Start Uninstall

[![Platform](https://img.shields.io/badge/platform-Windows%2011-lightgrey.svg)](#requirements)
[![Native AOT](https://img.shields.io/badge/.NET-Native%20AOT-512bd4.svg)](#light-by-design)

Right-click an app in Windows 11's Start menu, click **Uninstall**, and that app's own uninstaller runs. Without this helper, Start's **Uninstall** on a desktop (Win32) app only opens **Settings > Apps > Installed apps** and leaves you to find the app in a list of a hundred others.

There is no new menu and nothing to learn: you keep using Windows' own **Uninstall** item. The helper is a tiny background exe with no window that notices the click, works out which app it was for, closes the Settings page that Windows opened and starts the uninstaller. If it can't tell for sure which uninstaller belongs to the app, it leaves Settings open, already filtered to the app's name.

**[Requirements](#requirements) · [Install](#install) · [How it works](#how-it-works) · [Limits](#limits) · [Troubleshooting](#troubleshooting) · [Uninstall](#uninstall)**

## What you get

- **Start, pinned or All apps:** right-click a desktop app → **Uninstall** → its uninstaller starts (with the UAC prompt if it needs one). Settings flashes for under a second and closes.
- **Start search:** the same from a search result.
- **Not sure which app it is?** Settings stays open on Installed apps with the app's name typed into its filter, so the right entry is one click away.
- **Store (packaged) apps:** untouched. Windows already uninstalls those from Start itself.

### Light by design

- **Tiny.** A ~2.7 MB native exe (.NET Native AOT, raw COM UI Automation, no UI framework, no window): about 9 MB private memory.
- **Idle means idle.** No polling: it listens to UI Automation events (focus changes, invokes, windows opening) and only does work after an **Uninstall** click.
- **Never guesses silently.** An uninstaller is started only for a single, clear match in the registry's uninstall list, and only after Settings actually opened (that is how Windows answers the click). Every decision is written to a log.

## Requirements

- Windows 11 (built and tested on build 26300, English display language; see [Limits](#limits)).
- Nothing else for the one-line install: the release is a self-contained native exe.
- To build from source: the [.NET 11 SDK](https://dotnet.microsoft.com/download) and the Visual Studio **Desktop development with C++** workload (Native AOT uses the MSVC linker; Build Tools are enough).

## Install

**One line** (PowerShell, no SDK or git needed):

```powershell
irm https://raw.githubusercontent.com/lucaslobasso/start-uninstall/main/bootstrap.ps1 | iex
```

It downloads `StartUninstall.exe` from the latest release, checks its SHA-256, installs it to `%LOCALAPPDATA%\Programs\StartUninstall` and starts it with Windows. Read [`bootstrap.ps1`](bootstrap.ps1) first if you like; it's short. Without autostart:

```powershell
& ([scriptblock]::Create((irm https://raw.githubusercontent.com/lucaslobasso/start-uninstall/main/bootstrap.ps1))) -NoAutostart
```

**From source** (contributors): needs the .NET 11 SDK and the C++ workload above.

```powershell
git clone https://github.com/lucaslobasso/start-uninstall
cd start-uninstall
.\install.ps1                 # builds, installs, starts with Windows; -NoAutostart
```

Either way it also adds a **Start Uninstall** Start menu shortcut, which starts the helper again if you stopped it (starting it twice does nothing: only one copy runs).

## How it works

1. **Which app.** When Start's context menu opens, its first item takes focus. The tile you right-clicked is the one that got focus just before, or else the tile under the mouse pointer. Its UI Automation id says what it is: `W~<AppUserModelID>` (All apps) or `tile-W~…` (pinned) for desktop apps, `P~…` for Store apps, which are left alone. In Start search, the result under the menu's corner (the menu opens where you clicked) gives the app's name.
2. **The click.** Clicking **Uninstall** focuses that menu item and raises an *Invoked* event; the item's name is matched against "Uninstall" in a few display languages. If neither is recognised, Settings opening on Installed apps within 1.5 s of the menu closing counts as the click.
3. **Which uninstaller.** The app id is resolved through `shell:AppsFolder` to the shortcut's target exe; MSI advertised shortcuts also give their ProductCode. The app's name, target and ProductCode are matched against the uninstall entries in the registry (HKLM and HKCU, 64 and 32 bit), by ProductCode, then by name (ignoring versions, architecture tags and ™), then by install folder or icon.
4. **Act.** Nothing happens unless Settings comes to the front within 2.5 s. With one clear match, Settings is closed and the entry's `UninstallString` is started (never the silent `QuietUninstallString`; MSI `/I` becomes `/X`). Otherwise Settings stays open and its filter box gets the app's name.

The log is `%LOCALAPPDATA%\StartUninstall\log.txt` (it rolls over at 1 MB):

```text
DETECTED Uninstall on Start tile "Fake Uninstall Test, New" aumid=C:\…\FakeUninstallTest.exe via focus; tile from tile focus; menu opened 1539 ms earlier
resolved: name="Fake Uninstall Test" target="C:\…\FakeUninstallTest.exe" shortcut="…\Start Menu\Programs\Fake Uninstall Test.lnk" productCode=- (45 ms)
match: Exact "Fake Uninstall Test" HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\FakeUninstallTest -> C:\…\FakeRemover.exe /uninstall FakeUninstallTest
Settings in front (404 ms)
Settings close: ok (1207 ms)
started uninstaller pid=18952 (1256 ms)
```

## Limits

- **It depends on undocumented UI.** Start's automation ids (`W~`, `tile-W~`, `P~`), the search results' WebView and the Installed apps filter box id are not a public API. A Windows update can change them; the log then shows no `DETECTED` line and Windows behaves as before (Settings opens).
- **Display languages.** "Uninstall" is known in English, Spanish, Portuguese, German, French, Italian, Polish, Dutch, Swedish, Turkish, Japanese, Korean, Chinese and Russian, but only English was tested. Other languages rely on the Settings fallback.
- **Search** finds the app by name only (no app id), so it relies more on name matching; when a name is ambiguous, Settings stays open with the filter instead.
- **Settings already open:** if Settings was open before, Windows reuses that window, and the helper closes it too when it starts an uninstaller.
- **Uninstallers are the app's own.** Some ask questions, some need admin rights (UAC), some do nothing useful; the helper only starts them.

## Troubleshooting

- **Nothing happens, Settings opens as before.** Check that `StartUninstall.exe` is running (Task Manager → Details) and read the log. Set `STARTUNINSTALL_DEBUG=1` before starting it to also log every focus change in Start, Search and Settings.
- **Settings stays open with a filter.** The app's registry entry has a different name and no shortcut target or icon pointing at it, or there are several candidates. The log's `match:` line lists them.

## Uninstall

```powershell
& ([scriptblock]::Create((irm https://raw.githubusercontent.com/lucaslobasso/start-uninstall/main/bootstrap.ps1))) -Uninstall
```

From a clone: `.\uninstall.ps1`. Add `-Purge` to either to also delete the log.

## Contributing

```powershell
dotnet test                                                            # matcher, command builder, Start text
dotnet publish src/StartUninstall/StartUninstall.csproj -c Release -o out   # Native AOT build
```

See [CLAUDE.md](CLAUDE.md) for the layout, the end-to-end test and the test switches. `spike/` holds the original feasibility spike and its findings, kept for reference.
