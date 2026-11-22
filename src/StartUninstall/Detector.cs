// The decision chain. UIA callbacks only copy their cached properties into an Ev and queue it; one worker thread
// (MTA) reads the queue, keeps the little state below and does everything slow (shell, registry, Settings).
//
// Start (pinned or All apps): when the context menu opens, its first item gets focus. The right-clicked tile
// (AutomationId tile-W~ / W~ <AUMID>) is the one that got focus just before, or else the tile under the mouse.
// Clicking "Uninstall" focuses that item and raises Invoke_Invoked (matched against StartText.UninstallNames); if
// neither is recognised, Settings opening within 1.5 s of the menu closing is the fallback. Packaged (P~) tiles are
// left alone. Search: the result list lives in SearchHost's WebView2; its menu item has AutomationId "Uninstall"; the
// right-clicked row is found by hit-testing the menu's top-left corner (the menu opens where the click was).
// In every case Settings must come to the front within 2.5 s, or nothing happens.
using System.Collections.Concurrent;
using System.Diagnostics;

namespace StartUninstall;

enum EvKind { Focus, Invoke, WindowOpened }

/// <summary>One UIA event, with the cached properties and the mouse position at the moment it was raised.</summary>
sealed record Ev(EvKind Kind, DateTime At, string Name, int ControlType, int Pid, string Aid, string Cls, RECT Rect, int MouseX, int MouseY);

static class Detector
{
    static readonly ConcurrentQueue<Ev> Queue = new();
    static readonly AutoResetEvent Wake = new(false);

    public static void Post(Ev e) { Queue.Enqueue(e); Wake.Set(); }

    // ---- state (worker thread only) ----
    // The right-clicked tile: Start gives it focus when its window already has keyboard focus (keyboard use, or
    // Start opened by the Win key from a normal app). Otherwise only the menu takes focus; the tile is then the one
    // under the mouse when the menu opened (the cursor is still where the right-click was).
    static StartTile focusTile;
    static string focusTileName = "";
    static DateTime focusTileAt;
    static bool menuOpen;                    // a Start menu item had focus and nothing else has had it since
    static StartTile menuTile;
    static string menuTileName = "", menuTileHow = "";
    static DateTime menuOpenedAt, menuClosedAt;
    static DateTime lastJobEnd;              // events from before this moment are stale (a job blocks the worker)

    static readonly TimeSpan MaxMenuTime = TimeSpan.FromSeconds(60); // menu opened -> item clicked
    static readonly TimeSpan TileFocusToMenu = TimeSpan.FromSeconds(2);
    static readonly TimeSpan MenuClosedToSettings = TimeSpan.FromMilliseconds(1500);
    const int SettingsTimeoutMs = 2500, FilterBoxTimeoutMs = 4000;
    // Tests only: STARTUNINSTALL_SIMULATE=nofocus (ignore tile focus: the mouse hit test must find the tile) and/or
    // nolang (the menu's item names are not known: only the Settings fallback can detect the click).
    static readonly string Simulate = Environment.GetEnvironmentVariable("STARTUNINSTALL_SIMULATE") ?? "";
    static readonly bool SimNoFocus = Simulate.Contains("nofocus"), SimNoLang = Simulate.Contains("nolang");
    static bool IsUninstall(string name) => !SimNoLang && StartText.IsUninstall(name);
    static readonly int CloseSettleMs = int.TryParse(Environment.GetEnvironmentVariable("STARTUNINSTALL_SETTLE_MS"), out var ms) ? ms : 800;

    public static void Run()
    {
        Native.CoInitializeEx(0, Native.COINIT_MULTITHREADED);
        while (true)
        {
            Wake.WaitOne();
            while (Queue.TryDequeue(out var e))
            {
                try { Handle(e); }
                catch (Exception ex) { Log.Write($"error: {ex.GetType().Name}: {ex.Message}"); Done(); }
            }
        }
    }

    static void Handle(Ev e)
    {
        if (e.At <= lastJobEnd) return;
        var proc = Procs.Name(e.Pid);
        switch (e.Kind)
        {
            case EvKind.Focus:
                if (Log.Debug && proc is Procs.Start or Procs.Search or Procs.WebView or Procs.Frame or Procs.Settings)
                    Log.Write($"  focus {proc} ct={e.ControlType} name=\"{e.Name}\" aid=\"{e.Aid}\" class=\"{e.Cls}\" rect={e.Rect} mouse=({e.MouseX},{e.MouseY})");
                if (proc == Procs.Start) StartFocus(e);
                else
                {
                    CloseMenu(e.At);
                    if (proc == Procs.WebView && e.ControlType == UiaIds.ListItemType && e.Aid == "Uninstall" && e.Cls.Contains("menuItem"))
                        SearchUninstall();
                }
                break;

            case EvKind.Invoke:
                if (proc != Procs.Start) return;
                if (Log.Debug) Log.Write($"  invoke {proc} ct={e.ControlType} \"{e.Name}\" menuOpen={menuOpen}");
                if (menuOpen && IsUninstall(e.Name)) StartUninstall(e, "Invoke_Invoked", false);
                break;

            case EvKind.WindowOpened:
                if (Log.Debug) Log.Write($"  window opened {proc} \"{e.Name}\" menuOpen={menuOpen}");
                // The click on the menu item was not recognised (another display language, or Start raised nothing):
                // a window opening while a Win32 tile's menu is open or within 1.5 s after it closed. Acted on only if
                // it is Settings showing Installed apps.
                if (proc == Procs.Frame && menuTile.Kind != TileKind.None && (menuOpen || e.At - menuClosedAt <= MenuClosedToSettings))
                    StartUninstall(e, $"fallback: a window opened {(menuOpen ? "with the menu open" : $"{(e.At - menuClosedAt).TotalMilliseconds:0} ms after the menu closed")}", true);
                break;
        }
    }

    static void StartFocus(Ev e)
    {
        if (e.ControlType == UiaIds.ListItemType)
        {
            CloseMenu(e.At);
            var t = StartTile.Parse(e.Aid);
            if (t.Kind == TileKind.None || SimNoFocus) return;
            focusTile = t; focusTileName = e.Name; focusTileAt = e.At;
            return;
        }
        if (e.ControlType != UiaIds.MenuItemType) { CloseMenu(e.At); return; }

        if (!menuOpen)
        {
            menuOpen = true;
            menuOpenedAt = e.At;
            if (focusTile.Kind != TileKind.None && e.At - focusTileAt <= TileFocusToMenu)
                (menuTile, menuTileName, menuTileHow) = (focusTile, focusTileName, "tile focus");
            else
            {
                var sw = Stopwatch.StartNew();
                (menuTile, menuTileName) = TileAt(e.MouseX, e.MouseY);
                menuTileHow = $"mouse at ({e.MouseX},{e.MouseY}), hit test {sw.ElapsedMilliseconds} ms";
            }
            focusTile = default;
            if (Log.Debug) Log.Write($"  menu opened on {(menuTile.Kind == TileKind.None ? "no app tile" : $"\"{menuTileName}\"")} ({menuTileHow})");
        }
        if (IsUninstall(e.Name)) StartUninstall(e, "focus", false);
    }

    static void CloseMenu(DateTime at)
    {
        if (!menuOpen) return;
        menuOpen = false;
        menuClosedAt = at;
    }

    /// <summary>The Start app tile under a screen point.</summary>
    static (StartTile, string) TileAt(int x, int y)
    {
        int pid = Procs.FirstPid(Procs.Start);
        if (pid <= 0) return default;
        var start = Uia.Instance.GetRootElement().FindFirst(UiaIds.Children, Uia.Prop(UiaIds.ProcessId, pid));
        if (start is null) return default;
        foreach (var item in Uia.Each(start.FindAll(UiaIds.Descendants, Uia.Prop(UiaIds.ControlType, UiaIds.ListItemType))))
        {
            try
            {
                if (!item.get_CurrentBoundingRectangle().Contains(x, y)) continue;
                var t = StartTile.Parse(item.get_CurrentAutomationId());
                if (t.Kind != TileKind.None) return (t, item.get_CurrentName() ?? "");
            }
            catch { }
        }
        return default;
    }

    static void StartUninstall(Ev e, string via, bool fallback)
    {
        var tile = menuTile; var name = menuTileName;
        if (tile.Kind == TileKind.None) { Log.Write($"Uninstall ({via}) but the right-clicked tile is unknown ({menuTileHow}): nothing done"); Done(); return; }
        if (e.At - menuOpenedAt > MaxMenuTime) { Log.Write($"ignored ({via}): the menu opened {(e.At - menuOpenedAt).TotalSeconds:0} s earlier"); Done(); return; }
        if (tile.Kind == TileKind.Packaged)
        {
            Log.Write($"Uninstall on packaged app \"{name}\" ({tile.Aumid}) via {via}: left to Windows");
            Done();
            return;
        }
        Log.Write($"DETECTED Uninstall on Start tile \"{name}\" aumid={tile.Aumid} via {via}; tile from {menuTileHow}; menu opened {(e.At - menuOpenedAt).TotalMilliseconds:0} ms earlier");
        var sw = Stopwatch.StartNew();
        Act(AppResolver.FromAumid(tile.Aumid, StartText.TileName(name)), sw, confirmPage: fallback);
    }

    static void SearchUninstall()
    {
        var sw = Stopwatch.StartNew();
        Log.Write("DETECTED Uninstall in Start search");
        var name = SearchHitTest(out var how);
        if (name is null)
        {
            Log.Write($"search: could not tell which result was right-clicked ({how}); Settings is left to the user");
            Done();
            return;
        }
        Log.Write($"search: right-clicked result \"{name}\" ({how}, {sw.ElapsedMilliseconds} ms)");
        Act(AppResolver.FromName(name), sw);
    }

    /// <summary>The search result under the context menu's top-left corner (where the right-click was).</summary>
    static string? SearchHitTest(out string how)
    {
        how = "";
        int pid = Procs.FirstPid("SearchHost");
        var root = Uia.Instance.GetRootElement();
        var search = pid > 0 ? root.FindFirst(UiaIds.Children, Uia.Prop(UiaIds.ProcessId, pid)) : null;
        if (search is null) { how = "Search window gone"; return null; }
        var menu = search.FindFirst(UiaIds.Descendants, Uia.Prop(UiaIds.ClassName, "contextMenu"));
        if (menu is null) { how = "context menu gone"; return null; }
        var mr = menu.get_CurrentBoundingRectangle();
        if (mr.IsEmpty) { how = "context menu has no position"; return null; }
        foreach (var row in Uia.Each(search.FindAll(UiaIds.Descendants, Uia.Prop(UiaIds.ControlType, UiaIds.ListItemType))))
        {
            string cls;
            try { cls = row.get_CurrentClassName() ?? ""; } catch { continue; }
            if (!cls.Contains("suggestion")) continue;
            var rr = row.get_CurrentBoundingRectangle();
            if (!rr.Contains(mr.left, mr.top)) continue;
            how = $"menu at ({mr.left},{mr.top}) in row {rr} aid=\"{row.get_CurrentAutomationId()}\"";
            return StartText.SearchResultName(row.get_CurrentAutomationId(), row.get_CurrentName());
        }
        how = $"no result row under the menu at ({mr.left},{mr.top})";
        return null;
    }

    /// <param name="confirmPage">Act only once Settings shows Installed apps (when the click itself was not seen).</param>
    static void Act(ResolvedApp app, Stopwatch sw, bool confirmPage = false)
    {
        try
        {
            Log.Write($"resolved: name=\"{app.Name}\" target=\"{app.Target}\" shortcut=\"{app.Shortcut}\" productCode={app.ProductCode ?? "-"} ({sw.ElapsedMilliseconds} ms)");
            var match = new UninstallMatcher(new RegistryUninstallSource()).Match(app.Name, app.Target, app.ProductCode);
            UninstallCommand? cmd = null;
            if (match.Entry is { } entry && match.Kind is MatchKind.Exact or MatchKind.Single)
            {
                var c = UninstallCommand.Build(entry);
                if (Path.IsPathRooted(c.FileName) && !File.Exists(c.FileName))
                    Log.Write($"match: {match.Kind} {entry.KeyPath}, but its uninstaller is missing: {c.FileName}");
                else
                {
                    cmd = c;
                    Log.Write($"match: {match.Kind} \"{entry.DisplayName}\" {entry.KeyPath} -> {c.FileName} {c.Arguments}");
                }
            }
            else
                Log.Write($"match: {match.Kind}" + (match.Candidates.Count > 0 ? " among " + string.Join(", ", match.Candidates.Select(c => $"\"{c.DisplayName}\"")) : ""));

            var frame = SettingsWindow.WaitForeground(SettingsTimeoutMs);
            if (frame is not { } f) { Log.Write($"Settings did not come to the front within {SettingsTimeoutMs} ms: nothing done"); return; }
            Log.Write($"Settings in front ({sw.ElapsedMilliseconds} ms)");
            IUIAutomationElement? box = null;
            if (confirmPage)
            {
                box = SettingsWindow.WaitFilterBox(f, FilterBoxTimeoutMs);
                if (box is null) { Log.Write("not the Installed apps page: nothing done"); return; }
                Log.Write($"Installed apps page confirmed ({sw.ElapsedMilliseconds} ms)");
            }

            if (cmd is { } run)
            {
                // Closed at once, focus lands on Start's hidden window (it is still closing); a short settle lets it
                // return to the app that was in front before Start.
                Thread.Sleep(CloseSettleMs);
                Log.Write($"Settings close: {(SettingsWindow.Close(f) ? "ok" : "failed")} ({sw.ElapsedMilliseconds} ms)");
                try
                {
                    using var p = Process.Start(new ProcessStartInfo(run.FileName, run.Arguments) { UseShellExecute = true });
                    Log.Write($"started uninstaller pid={p?.Id.ToString() ?? "?"} ({sw.ElapsedMilliseconds} ms)");
                }
                catch (Exception ex) { Log.Write($"uninstaller failed to start: {ex.Message}"); }
                // Start sends Settings a second activation ~1.5 s after the click: close it again if it comes back.
                var watch = Stopwatch.StartNew();
                while (watch.ElapsedMilliseconds < 2500)
                {
                    Thread.Sleep(150);
                    if (watch.ElapsedMilliseconds > 500 && SettingsWindow.Foreground() is { } again) // the closed one may still be in front briefly
                        Log.Write($"Settings came back: close {(SettingsWindow.Close(again) ? "ok" : "failed")} ({sw.ElapsedMilliseconds} ms)");
                }
            }
            else
            {
                box ??= SettingsWindow.WaitFilterBox(f, FilterBoxTimeoutMs);
                if (box is null) { Log.Write($"Installed apps filter box not found within {FilterBoxTimeoutMs} ms"); return; }
                var value = SettingsWindow.Filter(box, app.Name);
                Log.Write($"Settings left open, filtered to \"{value}\" ({sw.ElapsedMilliseconds} ms)");
            }
        }
        finally { Done(); }
    }

    static void Done()
    {
        lastJobEnd = DateTime.Now;
        menuOpen = false;
        menuTile = default;
        focusTile = default;
    }
}

/// <summary>Process names by pid, cached for a minute (pids get reused).</summary>
static class Procs
{
    public const string Start = "StartMenuExperienceHost", Search = "SearchHost", WebView = "msedgewebview2",
        Frame = "ApplicationFrameHost", Settings = "SystemSettings";

    static readonly Dictionary<int, (string Name, long At)> Cache = new();

    public static string Name(int pid)
    {
        if (pid <= 0) return "";
        long now = Environment.TickCount64;
        if (Cache.TryGetValue(pid, out var c) && now - c.At < 60_000) return c.Name;
        string n;
        try { using var p = Process.GetProcessById(pid); n = p.ProcessName; } catch { n = "?"; }
        if (Cache.Count > 256) Cache.Clear();
        Cache[pid] = (n, now);
        return n;
    }

    public static int FirstPid(string name)
    {
        var ps = Process.GetProcessesByName(name);
        int pid = ps.Length > 0 ? ps[0].Id : 0;
        foreach (var p in ps) p.Dispose();
        return pid;
    }
}
