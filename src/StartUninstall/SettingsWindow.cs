// Settings > Apps > Installed apps, as Start's "Uninstall" opens it: ApplicationFrameHost's "ApplicationFrameWindow"
// hosting SystemSettings.exe. Found by the foreground window (Start brings it to the front), never by its localized
// title alone; the Installed apps page is recognised by its filter box's AutomationId.
using System.Diagnostics;

namespace StartUninstall;

static class SettingsWindow
{
    public const string FilterBoxId = "SystemSettings_StorageSense_AppSizesListFilter_DisplayStringValue";
    static readonly string[] Titles = { "Settings", "Configuración", "Einstellungen", "Paramètres", "Impostazioni", "Configurações", "Instellingen", "Ustawienia", "Inställningar", "Ayarlar", "設定", "설정", "设置", "Параметры" };

    public readonly record struct Frame(nint Hwnd, IUIAutomationElement Element);

    /// <summary>The foreground window, if it is the Settings app's frame.</summary>
    public static Frame? Foreground()
    {
        var h = Native.GetForegroundWindow();
        if (h == 0 || Native.ClassName(h) != "ApplicationFrameWindow") return null;
        try
        {
            var e = Uia.Instance.ElementFromHandle(h);
            return IsSettings(e) ? new Frame(h, e) : null;
        }
        catch { return null; }
    }

    static bool IsSettings(IUIAutomationElement frame)
    {
        var title = frame.get_CurrentName() ?? "";
        foreach (var t in Titles) if (title == t) return true;
        // any language: the hosted CoreWindow belongs to SystemSettings.exe
        foreach (var p in Process.GetProcessesByName("SystemSettings"))
        {
            using (p)
            {
                if (frame.FindFirst(UiaIds.Children, Uia.Prop(UiaIds.ProcessId, p.Id)) is not null) return true;
            }
        }
        return false;
    }

    /// <summary>Waits until Settings is the foreground window.</summary>
    public static Frame? WaitForeground(int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (true)
        {
            if (Foreground() is { } f) return f;
            if (sw.ElapsedMilliseconds >= timeoutMs) return null;
            Thread.Sleep(100);
        }
    }

    public static IUIAutomationElement? WaitFilterBox(Frame f, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        var cond = Uia.Prop(UiaIds.AutomationId, FilterBoxId);
        while (true)
        {
            try
            {
                if (Native.IsWindow(f.Hwnd) == 0) return null;
                if (f.Element.FindFirst(UiaIds.Descendants, cond) is { } box) return box;
            }
            catch { }
            if (sw.ElapsedMilliseconds >= timeoutMs) return null;
            Thread.Sleep(150);
        }
    }

    public static bool Close(Frame f)
    {
        try
        {
            if (Uia.Pattern<IUIAutomationWindowPattern>(f.Element, UiaIds.WindowPattern) is { } wp) { wp.Close(); return true; }
        }
        catch { }
        return Native.PostMessageW(f.Hwnd, Native.WM_CLOSE, 0, 0) != 0;
    }

    /// <summary>Types <paramref name="text"/> into the Installed apps filter; returns the box's value afterwards.</summary>
    public static string? Filter(IUIAutomationElement box, string text)
    {
        var vp = Uia.Pattern<IUIAutomationValuePattern>(box, UiaIds.ValuePattern);
        if (vp is null) return null;
        vp.SetValue(text);
        try { return vp.get_CurrentValue(); } catch { return null; }
    }
}
