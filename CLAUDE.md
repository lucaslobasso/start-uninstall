# start-uninstall

Native AOT C# (net11.0, raw COM UI Automation via `[GeneratedComInterface]`, no WinForms/WPF, no interop assembly).
A windowless helper that reacts to Windows 11 Start's own "Uninstall" item (we never add a menu of our own: the user
keeps Windows' item). Built and verified on build 26300.

- `src/StartUninstall.Core/`: platform-neutral logic with tests: `UninstallMatcher` (ProductCode > name > shortcut
  target/install folder/icon), `UninstallCommand` (UninstallString only, MSI /I -> /X), `RegistryUninstallSource`,
  `StartText` (tile ids `W~`/`tile-W~`/`P~`, search row names, localized "Uninstall").
- `src/StartUninstall/`: the exe. `Program.cs` (mutex, UIA handlers that only read the cache and queue),
  `Detector.cs` (worker thread: the whole decision chain), `Uia.cs` (vtable-exact COM interfaces; placeholder `_sN`
  slots keep the layout, numbers follow UIAutomationClient.h), `AppResolver.cs` (AUMID -> shell:AppsFolder
  `System.Link.TargetParsingPath`, shortcut, `MsiGetShortcutTarget`), `SettingsWindow.cs`.
- `spike/`: the feasibility spike and FINDINGS.md, kept for reference; not in the solution.

## Build / test
- `dotnet test` (from the repo root; the slnx has Core, the exe and the tests).
- `dotnet publish src/StartUninstall/StartUninstall.csproj -c Release -o out`. Native AOT needs the VS "Desktop
  development with C++" workload, and its linker setup needs `vswhere.exe` on PATH:
  `C:\Program Files (x86)\Microsoft Visual Studio\Installer` (install.ps1 adds it). Keep the build warning-free
  (`TreatWarningsAsErrors`).
- `.\install.ps1` installs to `%LOCALAPPDATA%\Programs\StartUninstall` + HKCU Run `StartUninstall` + Start menu
  shortcut and starts it; `.\uninstall.ps1 [-Purge]`. Keep them PowerShell 5.1-compatible.
- `nuget.config` clears machine feeds on purpose.

## Testing without touching an installed copy
- `STARTUNINSTALL_DIR=<folder>` moves the log; `STARTUNINSTALL_DEBUG=1` logs every focus change in Start, Search,
  Settings, plus invokes and windows opening. Only one copy runs at a time (one mutex): stop the installed one first
  (`StartUninstall.exe --quit`).
- `STARTUNINSTALL_SIMULATE=nofocus` ignores tile focus (forces the mouse hit test), `nolang` treats every menu item
  name as unknown (forces the Settings fallback); comma-separate both. `STARTUNINSTALL_SETTLE_MS` (default 800) is the
  pause before closing Settings.
- End-to-end test (done for 0.1.0, 20 runs): a throwaway app in a temp folder (exe + Start menu shortcut in
  `%APPDATA%\Microsoft\Windows\Start Menu\Programs` + HKCU Uninstall key whose UninstallString is a tiny exe that
  appends a timestamp to a marker file), plus a second app with no matching key (expects Settings left open and
  filtered). Drive Start with real input: SendInput Win, real mouse moves (SendInput absolute, not SetCursorPos: Start
  only reacts to real pointer moves) and clicks at the UIA rects. Never send keys unless StartMenuExperienceHost or
  SearchHost is the foreground window. Remove the shortcuts, keys and files afterwards. Never uninstall a real app.

## Gotchas (build 26300)
- Start's foreground window is often SearchHost (its search box) while StartMenuExperienceHost shows the tiles.
- Right-clicking a tile gives it focus only when Start's window has keyboard focus; otherwise only the menu's first
  item gets focus. So the tile is taken from a focus event less than 2 s before the menu, else from a hit test of the
  mouse position (captured in the UIA callback) against Start's ListItems (~140 ms).
- After the click, focus returns to the tile *before* Settings opens: the fallback is "Settings within 1.5 s of the
  menu closing", not "while the menu is open".
- XAML flyout rects from UIA can be stale by ~50 px while the menu animates; test drivers must hit-test before
  clicking.
- Search result ids wrap the typed match in private-use marks: `PP\uE000Fake App\uE001` (StartText strips them).
  Chromium builds its UIA tree lazily: a property search can miss what a full walk finds.
- Closing Settings at once leaves focus on Start's hidden window; a 800 ms settle returns it to the previous app.
- Start sends Settings a second "window opened" ~0.1-1.5 s after the first; events from before a job ended are dropped.
- Never (un)register UIA handlers or call UIA inside a callback.
