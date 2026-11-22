// Feasibility spike: Start menu "Uninstall" interception via UI Automation (COM UIA3 through Interop.UIAutomationClient).
// Commands (run `Spike <cmd> [args]`):
//   start-open                 open Start (Win key via SendInput) if not open
//   start-dump [file]          dump the Start window UIA subtree
//   find <name>                list Start elements whose Name contains <name>
//   rightclick <name>          right-click the first Start ListItem/Button whose Name == <name>, then dump the context menu
//   invoke-menu <name>         invoke the MenuItem named <name> (in the open context menu)
//   watch <seconds> [file]     global event listener (MenuOpened, Invoke_Invoked, focus, window opened, menu mode)
//   settings-dump [file]       dump the Settings window
//   settings-search <text>     ValuePattern.SetValue on the Settings search box and dump the resulting list
//   key <vk>                   send one key (e.g. 0x1B Escape)
//   close-settings             close Settings via WindowPattern.Close
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Interop.UIAutomationClient;

static class P
{
    static readonly CUIAutomation8 Uia = new();
    static class Clock { public static string ElapsedMilliseconds => DateTime.Now.ToString("HH:mm:ss.fff"); }

    static int Main(string[] a)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string cmd = a.Length > 0 ? a[0] : "start-dump";
        switch (cmd)
        {
            case "start-open": OpenStart(); return 0;
            case "start-dump": { var w = StartWindow(); if (w == null) { Console.WriteLine("Start window not found"); return 1; } Dump(w, a.Length > 1 ? a[1] : null, 40); return 0; }
            case "find": Find(a[1]); return 0;
            case "rightclick": return RightClick(a[1]);
            case "rightclick-aid": return RightClick(a[1], byAid: true);
            case "invoke-menu": return InvokeMenu(a[1]);
            case "click-menu": return InvokeMenu(a[1], click: true);
            case "click-aid": return InvokeMenu(a[1], click: true, byAid: true);
            case "detect": Detect(int.Parse(a[1])); return 0;
            case "watch": Watch(int.Parse(a[1]), a.Length > 2 ? a[2] : null); return 0;
            case "settings-dump": { var s = SettingsWindow(); if (s == null) { Console.WriteLine("Settings not found"); return 1; } Dump(s, a.Length > 1 ? a[1] : null, 40); return 0; }
            case "settings-search": return SettingsSearch(a[1]);
            case "type": if (!StartIsForeground()) { Console.WriteLine("refusing to type: Start/Search is not the foreground window"); return 1; } foreach (var ch in a[1]) { var i = new INPUT[2]; i[0].type = 1; i[0].u.ki.wScan = ch; i[0].u.ki.dwFlags = 4; i[1] = i[0]; i[1].u.ki.dwFlags = 6; SendInput(2, i, Marshal.SizeOf<INPUT>()); Thread.Sleep(30); } return 0;
            case "dump-search": { var w = SearchWindow(); if (w == null) return 1; Dump(w, a.Length > 1 ? a[1] : null, 40); return 0; }
            case "at": { var pt = new tagPOINT { x = int.Parse(a[1]), y = int.Parse(a[2]) }; var e = Uia.ElementFromPoint(pt); Console.WriteLine(Describe(e) + Extra(e)); return 0; }
            case "key": if (!StartIsForeground()) { Console.WriteLine("refusing to send key: Start/Search is not the foreground window"); return 1; } Key((ushort)Convert.ToInt32(a[1], 16)); return 0;
            case "close-settings": { var s = SettingsWindow(); if (s == null) return 1; ((IUIAutomationWindowPattern)s.GetCurrentPattern(10009)).Close(); return 0; }
            case "toplevel": foreach (var e in All(Uia.GetRootElement(), TreeScope.TreeScope_Children)) Console.WriteLine(Describe(e) + $" proc={ProcName(e.CurrentProcessId)}"); return 0;
        }
        Console.WriteLine("unknown command"); return 2;
    }

    // ---------- Start ----------
    static IUIAutomationElement? StartWindow()
    {
        var root = Uia.GetRootElement();
        foreach (var e in All(root, TreeScope.TreeScope_Children))
        {
            var pn = ProcName(e.CurrentProcessId);
            if (pn.Equals("StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase) && e.CurrentName is "Start" or "Inicio") return e;
        }
        foreach (var e in All(root, TreeScope.TreeScope_Children))
            if (ProcName(e.CurrentProcessId).Equals("StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase)) return e;
        return null;
    }

    // While typing in Start, the results are shown by SearchHost in its own top-level "Search" CoreWindow (Start's window disappears).
    static IUIAutomationElement? SearchWindow()
    {
        foreach (var e in All(Uia.GetRootElement(), TreeScope.TreeScope_Children))
            if (ProcName(e.CurrentProcessId) == "SearchHost") return e;
        return null;
    }

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
    static bool StartIsForeground() { GetWindowThreadProcessId(GetForegroundWindow(), out int pid); return ProcName(pid) is "StartMenuExperienceHost" or "SearchHost"; }

    static IUIAutomationElement? StartOrSearch() => StartWindow() ?? SearchWindow();

    static void OpenStart()
    {
        var w = StartWindow();
        if (w != null && w.CurrentIsOffscreen == 0 && w.CurrentBoundingRectangle.right - w.CurrentBoundingRectangle.left > 0) { Console.WriteLine("Start already open"); return; }
        Key(0x5B); // VK_LWIN
        for (int i = 0; i < 30 && !StartIsForeground(); i++) Thread.Sleep(100);
        Thread.Sleep(300);
        Console.WriteLine(StartWindow() is { } s ? "Start: " + Describe(s) : "Start window not found after Win key");
    }

    static void Find(string text)
    {
        var w = StartOrSearch()!;
        foreach (var e in All(w, TreeScope.TreeScope_Descendants))
            if ((e.CurrentName ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)) Console.WriteLine(Describe(e) + Extra(e));
    }

    static int RightClick(string name, bool byAid = false)
    {
        var w = StartOrSearch()!;
        var cond = Uia.CreatePropertyCondition(byAid ? 30011 : 30005, name);
        var el = w.FindFirst(TreeScope.TreeScope_Descendants, cond);
        if (el == null) { Console.WriteLine("not found: " + name); return 1; }
        Console.WriteLine("target: " + Describe(el) + Extra(el));
        var r = el.CurrentBoundingRectangle;
        int x = (r.left + r.right) / 2, y = (r.top + r.bottom) / 2;
        SetCursorPos(x, y); Thread.Sleep(150);
        Mouse(0x0008); Mouse(0x0010); // right down/up
        Thread.Sleep(800);
        DumpMenus();
        var f = Uia.GetFocusedElement();
        Console.WriteLine("focused after right-click: " + Describe(f) + Extra(f));
        return 0;
    }

    static void DumpMenus()
    {
        var menuCond = Uia.CreatePropertyCondition(30003, 50009); // ControlType Menu
        var root = Uia.GetRootElement();
        foreach (var m in All(root, TreeScope.TreeScope_Children))
            if (m.CurrentControlType == 50009 || m.CurrentClassName == "Popup" || m.CurrentClassName == "#32768")
            { Console.WriteLine("top-level menu window:"); Dump(m, null, 8); }
        var w = StartOrSearch();
        if (w != null)
        {
            var ms = w.FindAll(TreeScope.TreeScope_Descendants, menuCond);
            for (int i = 0; i < ms.Length; i++) { Console.WriteLine("menu inside Start window:"); Dump(ms.GetElement(i), null, 8); }
        }
    }

    static int InvokeMenu(string name, bool click = false, bool byAid = false)
    {
        var cond = byAid ? Uia.CreatePropertyCondition(30011, name) : Uia.CreateAndCondition(Uia.CreatePropertyCondition(30005, name), Uia.CreatePropertyCondition(30003, 50011));
        var el = StartOrSearch()?.FindFirst(TreeScope.TreeScope_Descendants, cond) ?? Uia.GetRootElement().FindFirst(TreeScope.TreeScope_Descendants, cond);
        if (el == null) { Console.WriteLine("menu item not found"); return 1; }
        Console.WriteLine($"[{Clock.ElapsedMilliseconds}] invoking " + Describe(el) + Extra(el));
        if (click) { var r = el.CurrentBoundingRectangle; MoveTo((r.left + r.right) / 2 - 5, (r.top + r.bottom) / 2); Thread.Sleep(60); MoveTo((r.left + r.right) / 2, (r.top + r.bottom) / 2); Thread.Sleep(150); Mouse(0x0002); Thread.Sleep(50); Mouse(0x0004); }
        else ((IUIAutomationInvokePattern)el.GetCurrentPattern(10000)).Invoke();
        Console.WriteLine($"[{Clock.ElapsedMilliseconds}] invoked; process snapshot (uninstaller check):");
        for (int i = 0; i < 12; i++)
        {
            Thread.Sleep(500);
            var procs = Process.GetProcesses().Where(p => p.ProcessName is "SystemSettings" or "uninstall" or "Uninstall" or "msiexec" || p.ProcessName.Contains("unins", StringComparison.OrdinalIgnoreCase))
                .Select(p => $"{p.ProcessName}({p.Id})");
            Console.WriteLine($"  t+{(i + 1) * 500}ms: " + string.Join(", ", procs));
        }
        return 0;
    }

    // ---------- Settings ----------
    static IUIAutomationElement? SettingsWindow()
    {
        foreach (var e in All(Uia.GetRootElement(), TreeScope.TreeScope_Children))
        {
            var pn = ProcName(e.CurrentProcessId);
            if (pn == "SystemSettings" || (pn == "ApplicationFrameHost" && (e.CurrentName ?? "").Contains("Settings")) || (e.CurrentName ?? "") is "Settings" or "Configuración")
                return e;
        }
        return null;
    }

    static int SettingsSearch(string text)
    {
        var s = SettingsWindow();
        if (s == null) { Console.WriteLine("Settings not found"); return 1; }
        var edits = s.FindAll(TreeScope.TreeScope_Descendants, Uia.CreatePropertyCondition(30003, 50004));
        for (int i = 0; i < edits.Length; i++) Console.WriteLine("edit: " + Describe(edits.GetElement(i)) + Extra(edits.GetElement(i)));
        // the Installed apps page's own filter box (not the global "Find a setting" CommandSearchTextBox)
        var box = s.FindFirst(TreeScope.TreeScope_Descendants, Uia.CreatePropertyCondition(30011, "SystemSettings_StorageSense_AppSizesListFilter_DisplayStringValue"));
        if (box == null) { Console.WriteLine("app search box not identified"); return 1; }
        Console.WriteLine("using: " + Describe(box));
        ((IUIAutomationValuePattern)box.GetCurrentPattern(10002)).SetValue(text);
        Thread.Sleep(1500);
        var count = s.FindFirst(TreeScope.TreeScope_Descendants, Uia.CreatePropertyCondition(30011, "SystemSettings_StorageSense_TotalAppsVisible_AppSizesListTotalAppsVisibleText"));
        Console.WriteLine("count text: " + count?.CurrentName);
        var rows = s.FindAll(TreeScope.TreeScope_Descendants, Uia.CreatePropertyCondition(30003, 50026));
        for (int i = 0; i < rows.Length; i++) { var r = rows.GetElement(i); if ((r.CurrentAutomationId ?? "").EndsWith("_ListItem")) Console.WriteLine("row: " + Describe(r)); }
        return 0;
    }

    // ---------- events ----------
    sealed class H : IUIAutomationEventHandler, IUIAutomationFocusChangedEventHandler, IUIAutomationStructureChangedEventHandler
    {
        public static readonly List<string> Log = new();
        readonly string tag; public H(string tag) => this.tag = tag;
        void W(string s) { s = s.Replace("] ", "] {" + tag + "} "); lock (Log) { Log.Add(s); Console.WriteLine(s); } }
        public void HandleAutomationEvent(IUIAutomationElement sender, int eventId)
        {
            string focus = "";
            try { var f = Uia.GetFocusedElement(); focus = " | focus=" + Describe(f) + Extra(f); } catch (Exception ex) { focus = " | focus err " + ex.Message; }
            string cached = "";
            try { cached = $" cachedName=\"{sender.CachedName}\" cachedCT={CT(sender.CachedControlType)}"; } catch { }
            W($"[{Clock.ElapsedMilliseconds}] EVT {EventName(eventId)} sender={Safe(sender)}{cached}{focus}");
        }
        public void HandleFocusChangedEvent(IUIAutomationElement sender) => W($"[{Clock.ElapsedMilliseconds}] FOCUS {Safe(sender)}");
        public void HandleStructureChangedEvent(IUIAutomationElement sender, StructureChangeType changeType, int[] runtimeId)
            => W($"[{Clock.ElapsedMilliseconds}] STRUCT {changeType} {Safe(sender)}");
        static string Safe(IUIAutomationElement e) { try { return Describe(e) + Extra(e) + $" proc={ProcName(e.CurrentProcessId)}"; } catch (Exception ex) { return "<" + ex.Message + ">"; } }
    }

    static void Watch(int seconds, string? file)
    {
        var root = Uia.GetRootElement();
        var cr = Uia.CreateCacheRequest(); cr.AddProperty(30005); cr.AddProperty(30003); cr.AddProperty(30011);
        int[] evts = { 20003, 20007, 20009, 20016, 20017, 20018, 20019, 20012 };
        var hr = new H("root");
        foreach (var id in evts) Uia.AddAutomationEventHandler(id, root, TreeScope.TreeScope_Subtree, cr, hr);
        Uia.AddFocusChangedEventHandler(null!, hr);
        var start = StartWindow();
        if (start != null)
        {
            var hs = new H("start");
            foreach (var id in evts) Uia.AddAutomationEventHandler(id, start, TreeScope.TreeScope_Subtree, cr, hs);
            Console.WriteLine("also registered on Start window: " + Describe(start));
            var search = start.FindFirst(TreeScope.TreeScope_Children, Uia.CreatePropertyCondition(30005, "Search"));
            if (search != null)
            {
                var hq = new H("search");
                foreach (var id in evts) Uia.AddAutomationEventHandler(id, search, TreeScope.TreeScope_Subtree, cr, hq);
                Console.WriteLine("also registered on Search window: " + Describe(search));
            }
        }
        Console.WriteLine($"[{Clock.ElapsedMilliseconds}] watching {seconds}s");
        Thread.Sleep(seconds * 1000);
        Uia.RemoveAllEventHandlers();
        if (file != null) lock (H.Log) File.WriteAllLines(file, H.Log);
    }

    // ---------- prototype detector: event-driven, no polling ----------
    // root: focus changes (global) + Window_WindowOpened. When focus enters StartMenuExperienceHost, a worker thread
    // (re)registers Invoke_Invoked/MenuOpened on the Start window (registration from inside a UIA callback is not allowed).
    sealed class D : IUIAutomationEventHandler, IUIAutomationFocusChangedEventHandler
    {
        public string? LastTileAid, LastTileName, LastResultAid, LastResultName; public DateTime LastTileAt, MenuOpenedAt, UninstallAt;
        public readonly AutoResetEvent Wake = new(false); public volatile bool NeedRegister;
        public void HandleFocusChangedEvent(IUIAutomationElement e)
        {
            try
            {
                string cn = ""; int cpid = 0, cct = 0; try { cn = e.CachedName; cpid = e.CachedProcessId; cct = e.CachedControlType; } catch { }
                if (cct == 50011 && ProcName(cpid) == "StartMenuExperienceHost")
                {
                    Console.WriteLine($"[{Clock.ElapsedMilliseconds}] focus -> MenuItem cachedName=\"{cn}\"");
                    if (cn == "Uninstall") { UninstallAt = DateTime.Now; Console.WriteLine($"[{Clock.ElapsedMilliseconds}] DETECTED (focus) Uninstall of \"{LastTileName}\" aid=\"{LastTileAid}\""); }
                }
                var pn = ProcName(e.CurrentProcessId);
                if (pn is "StartMenuExperienceHost" or "SearchHost")
                {
                    NeedRegister = true; Wake.Set();
                    if (e.CurrentControlType == 50007) { LastTileAid = e.CurrentAutomationId; LastTileName = e.CurrentName; LastTileAt = DateTime.Now; }
                }
                else if (pn == "msedgewebview2" && e.CurrentControlType == 50007)
                {
                    // Start search results are HTML inside SearchHost's WebView2; the menu is a <list> whose items have stable AutomationIds
                    var aid = e.CurrentAutomationId; var cls = e.CurrentClassName ?? "";
                    Console.WriteLine($"[{Clock.ElapsedMilliseconds}] webview focus aid=\"{aid}\" name=\"{e.CurrentName}\" class=\"{cls}\"");
                    if (aid == "Uninstall" && cls.Contains("menuItem"))
                    {
                        Console.WriteLine($"[{Clock.ElapsedMilliseconds}] DETECTED (search) Uninstall; focused result (unreliable) = \"{LastResultName}\"");
                        // Right-click does not move focus/selection to the result; the menu opens at the click point, so hit-test
                        // the menu's top-left corner against the result rows (ElementFromPoint does not see inside the WebView).
                        var sw = Stopwatch.StartNew();
                        var docRoot = Uia.ControlViewWalker.GetParentElement(e); // the "Context Menu" list
                        var mr = docRoot.CurrentBoundingRectangle;
                        var rows = SearchWindow()?.FindAll(TreeScope.TreeScope_Descendants, Uia.CreatePropertyCondition(30003, 50007));
                        string hit = "<none>";
                        for (int i = 0; rows != null && i < rows.Length; i++)
                        {
                            var r = rows.GetElement(i); var rr = r.CurrentBoundingRectangle;
                            if ((r.CurrentClassName ?? "").Contains("suggestion") && mr.left >= rr.left && mr.left <= rr.right && mr.top >= rr.top && mr.top <= rr.bottom) { hit = $"\"{r.CurrentName}\" aid=\"{r.CurrentAutomationId}\""; break; }
                        }
                        Console.WriteLine($"[{Clock.ElapsedMilliseconds}] menu rect=({mr.left},{mr.top}) -> right-clicked result {hit} (hit-test {sw.ElapsedMilliseconds}ms)");
                    }
                    else if (cls.Contains("suggestion")) { LastResultAid = aid; LastResultName = e.CurrentName; }
                }
            }
            catch { }
        }
        public void HandleAutomationEvent(IUIAutomationElement sender, int id)
        {
            string name = ""; try { name = sender.CachedName; } catch { }
            var now = DateTime.Now;
            if (id == 20003) { MenuOpenedAt = now; Console.WriteLine($"[{Clock.ElapsedMilliseconds}] MenuOpened; right-clicked = \"{LastTileName}\" aid=\"{LastTileAid}\""); }
            else if (id == 20009)
            {
                int pid = 0; try { pid = sender.CachedProcessId; } catch { }
                var spn = ProcName(pid);
                Console.WriteLine($"[{Clock.ElapsedMilliseconds}] Invoke_Invoked cachedName=\"{name}\" cachedPid={pid} ({spn})");
                if (name is "Uninstall" && spn is "StartMenuExperienceHost" or "SearchHost") { UninstallAt = now; Console.WriteLine($"[{Clock.ElapsedMilliseconds}] DETECTED Uninstall of \"{LastTileName}\" aid=\"{LastTileAid}\" (tile focused {(now - LastTileAt).TotalMilliseconds:0}ms ago)"); }
            }
            else if (id == 20016)
            {
                string pn = ""; try { pn = ProcName(sender.CurrentProcessId); } catch { }
                if (name == "Settings" && pn == "ApplicationFrameHost" && UninstallAt != default)
                    Console.WriteLine($"[{Clock.ElapsedMilliseconds}] Settings window opened; {(now - UninstallAt).TotalMilliseconds:0}ms after Uninstall invoke, {(now - MenuOpenedAt).TotalMilliseconds:0}ms after MenuOpened");
            }
        }
    }

    static void Detect(int seconds)
    {
        var d = new D();
        var cr = Uia.CreateCacheRequest(); cr.AddProperty(30005); cr.AddProperty(30003); cr.AddProperty(30002);
        Uia.AddFocusChangedEventHandler(cr, d); // cache request: the clicked MenuFlyoutItem is already gone when the handler runs
        Uia.AddAutomationEventHandler(20016, Uia.GetRootElement(), TreeScope.TreeScope_Children, cr, d);
        // Invoke_Invoked from the Start flyout is only delivered to a root-subtree registration (not to one on the Start window)
        Uia.AddAutomationEventHandler(20009, Uia.GetRootElement(), TreeScope.TreeScope_Subtree, cr, d);
        int[]? registeredRid = null; IUIAutomationElement? registeredOn = null;
        var end = DateTime.Now.AddSeconds(seconds);
        Console.WriteLine($"[{Clock.ElapsedMilliseconds}] detector running {seconds}s");
        while (DateTime.Now < end)
        {
            d.Wake.WaitOne(500);
            if (!d.NeedRegister) continue;
            d.NeedRegister = false;
            var start = StartWindow();
            if (start == null) continue;
            var rid = (int[])start.GetRuntimeId();
            if (registeredRid != null && rid.SequenceEqual(registeredRid)) continue;
            if (registeredOn != null) { try { Uia.RemoveAutomationEventHandler(20003, registeredOn, d); } catch { } }
            var sw = Stopwatch.StartNew();
            // MenuOpened is only delivered to a registration on the Start window itself (not to the root-subtree one)
            Uia.AddAutomationEventHandler(20003, start, TreeScope.TreeScope_Subtree, cr, d);
            registeredOn = start; registeredRid = rid;
            Console.WriteLine($"[{Clock.ElapsedMilliseconds}] registered on Start window rid={string.Join(".", rid)} in {sw.ElapsedMilliseconds}ms");
        }
        Uia.RemoveAllEventHandlers();
    }

    static string EventName(int id) => id switch
    {
        20003 => "MenuOpened", 20007 => "MenuClosed", 20009 => "Invoke_Invoked", 20016 => "Window_WindowOpened", 20017 => "Window_WindowClosed",
        20018 => "MenuModeStart", 20019 => "MenuModeEnd", 20012 => "SelectionItem_ElementSelected", _ => id.ToString()
    };

    // ---------- dump helpers ----------
    static IEnumerable<IUIAutomationElement> All(IUIAutomationElement e, TreeScope scope)
    {
        var arr = e.FindAll(scope, Uia.CreateTrueCondition());
        for (int i = 0; i < arr.Length; i++) yield return arr.GetElement(i);
    }

    static void Dump(IUIAutomationElement e, string? file, int maxDepth)
    {
        var sb = new StringBuilder();
        var walker = Uia.ControlViewWalker;
        void Rec(IUIAutomationElement x, int d)
        {
            sb.Append(new string(' ', d * 2)).Append(Describe(x)).Append(Extra(x)).AppendLine();
            if (d >= maxDepth) return;
            var c = walker.GetFirstChildElement(x);
            while (c != null) { Rec(c, d + 1); c = walker.GetNextSiblingElement(c); }
        }
        Rec(e, 0);
        if (file != null) { File.WriteAllText(file, sb.ToString()); Console.WriteLine($"dumped {sb.Length} chars to {file}"); }
        else Console.Write(sb);
    }

    static string Describe(IUIAutomationElement? e)
    {
        if (e == null) return "<null>";
        try
        {
            var r = e.CurrentBoundingRectangle;
            return $"{CT(e.CurrentControlType)} name=\"{e.CurrentName}\" aid=\"{e.CurrentAutomationId}\" class=\"{e.CurrentClassName}\" fw={e.CurrentFrameworkId} pid={e.CurrentProcessId} rect=({r.left},{r.top},{r.right},{r.bottom})";
        }
        catch (Exception ex) { return "<" + ex.Message + ">"; }
    }

    static string Extra(IUIAutomationElement e)
    {
        var sb = new StringBuilder();
        void Add(string k, int id) { try { var v = e.GetCurrentPropertyValue(id); if (v is string s && s.Length > 0) sb.Append($" {k}=\"{s}\""); } catch { } }
        Add("help", 30013); Add("itemStatus", 30026); Add("fullDesc", 30159); Add("laValue", 30093); Add("laDesc", 30094); Add("itemType", 30021); Add("localizedCT", 30004);
        try
        {
            var pats = new List<string>();
            if ((bool)e.GetCurrentPropertyValue(30031)) pats.Add("Invoke");
            if ((bool)e.GetCurrentPropertyValue(30028)) pats.Add("ExpandCollapse");
            if ((bool)e.GetCurrentPropertyValue(30036)) pats.Add("SelectionItem");
            if ((bool)e.GetCurrentPropertyValue(30043)) pats.Add("Value");
            if (pats.Count > 0) sb.Append(" pats=[" + string.Join(",", pats) + "]");
        }
        catch { }
        return sb.ToString();
    }

    static string CT(int id) => id switch
    {
        50000 => "Button", 50002 => "CheckBox", 50003 => "ComboBox", 50004 => "Edit", 50005 => "Hyperlink", 50006 => "Image", 50007 => "ListItem", 50008 => "List",
        50009 => "Menu", 50010 => "MenuBar", 50011 => "MenuItem", 50013 => "RadioButton", 50014 => "ScrollBar", 50018 => "Tab", 50019 => "TabItem", 50020 => "Text",
        50021 => "ToolBar", 50022 => "ToolTip", 50023 => "Tree", 50024 => "TreeItem", 50025 => "Custom", 50026 => "Group", 50028 => "DataGrid", 50029 => "DataItem",
        50030 => "Document", 50031 => "SplitButton", 50032 => "Window", 50033 => "Pane", 50034 => "Header", 50035 => "HeaderItem", 50037 => "TitleBar", 50038 => "Separator",
        50039 => "SemanticZoom", 50040 => "AppBar", _ => id.ToString()
    };

    static readonly Dictionary<int, string> ProcCache = new();
    static string ProcName(int pid)
    {
        lock (ProcCache)
        {
            if (ProcCache.TryGetValue(pid, out var n)) return n;
            try { n = Process.GetProcessById(pid).ProcessName; } catch { n = "?"; }
            return ProcCache[pid] = n;
        }
    }

    // ---------- input ----------
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Explicit)] struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public INPUTUNION u; }
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);

    static void Key(ushort vk)
    {
        var i = new INPUT[2];
        i[0].type = 1; i[0].u.ki.wVk = vk;
        i[1].type = 1; i[1].u.ki.wVk = vk; i[1].u.ki.dwFlags = 2; // KEYUP
        SendInput(2, i, Marshal.SizeOf<INPUT>());
    }

    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
    static void MoveTo(int x, int y)
    {
        var i = new INPUT[1];
        i[0].type = 0; i[0].u.mi.dwFlags = 0x0001 | 0x8000 | 0x4000; // MOVE | ABSOLUTE | VIRTUALDESK
        int vx = GetSystemMetrics(76), vy = GetSystemMetrics(77), vw = GetSystemMetrics(78), vh = GetSystemMetrics(79);
        i[0].u.mi.dx = (int)((x - vx) * 65535L / (vw - 1)); i[0].u.mi.dy = (int)((y - vy) * 65535L / (vh - 1));
        SendInput(1, i, Marshal.SizeOf<INPUT>());
    }

    static void Mouse(uint flags)
    {
        var i = new INPUT[1];
        i[0].type = 0; i[0].u.mi.dwFlags = flags;
        SendInput(1, i, Marshal.SizeOf<INPUT>());
    }
}
