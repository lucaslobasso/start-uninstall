# Spike findings: catching Start's "Uninstall" (build 26300)

Verdict: feasible, no polling, Native AOT works via raw COM UIA.

1. **Start host**: `StartMenuExperienceHost.exe`, top-level `Window "Start"` (`Windows.UI.Core.CoreWindow`), in the UIA tree only while open; element (RuntimeId 42.131766) persists. Typing switches to top-level "Search" (`SearchHost.exe`, results in WebView2 / `msedgewebview2.exe`).
2. **App identity**: items are `ListItem`/`GridViewItem`; AutomationId `tile-W~<AUMID>` (pinned) / `W~<AUMID>` (All apps); `P~` = packaged. AUMID → `shell:AppsFolder` `System.Link.TargetParsingPath` → Uninstall key by DisplayIcon / InstallLocation / UninstallString folder; MSI advertised shortcuts → `MsiGetShortcutTarget` ProductCode. Search results only `aid=PP<display name>`.
3. **Menus**: Start `MenuFlyout`/`MenuFlyoutItem`, Uninstall = `Name="Uninstall"`, empty AutomationId (localized!). Search: `List class=contextMenu`, `ListItem aid=Uninstall` (stable). Start tiles take focus on right-click (~80–100 ms before menu); search rows don't → hit-test menu top-left vs result rows.
4. **Uninstall opens only Settings** (`SystemSettings.exe`, `ApplicationFrameHost` `ApplicationFrameWindow "Settings"`, breadcrumb "Installed apps"), unfiltered. Filter box `SystemSettings_StorageSense_AppSizesListFilter_DisplayStringValue`; `ValuePattern.SetValue` filters (~1.5 s). Rows `<UninstallKey>_ListItem`. Settings WindowOpened 240–330 ms after click, duplicate 1.1–1.7 s later (dedupe). `WindowPattern.Close()` closes it.
5. **Events**: global FocusChanged *with cache request* (Name, ControlType, ProcessId, AutomationId) → cached Name "Uninstall" from StartMenuExperienceHost, 8/8, ~300 ms. Invoke_Invoked only on root/subtree, not fully reliable, not raised by UIA Invoke(). MenuOpened only on the Start window. Search menu: focus on `aid=Uninstall` in msedgewebview2 ~250 ms. Never (un)register handlers inside a UIA callback.
6. **Native AOT**: `[GeneratedComInterface]` client with `DisableRuntimeMarshalling` + `ComVariant`; 3/3 detections. Linking needs VS Installer dir (vswhere) on PATH.

Risks: localized "Uninstall" name, weaker search path, Invoke secondary, undocumented IDs on an Insider build.
