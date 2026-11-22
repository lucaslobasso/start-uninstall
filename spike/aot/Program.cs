// AOT probe: global focus + Invoke_Invoked (root subtree, with cache request) + MenuOpened (Start window) through raw COM UIA
// under Native AOT. Vtable slots that are never called are declared as placeholder methods (_sN) to keep the layout.
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

[assembly: System.Runtime.CompilerServices.DisableRuntimeMarshalling]

static unsafe class Program
{
    [DllImport("ole32.dll")] static extern int CoInitializeEx(IntPtr r, uint coinit);
    [DllImport("ole32.dll")] static extern int CoCreateInstance(Guid* clsid, IntPtr outer, uint ctx, Guid* iid, IntPtr* ppv);

    internal static IUIAutomation Uia = null!;
    internal static string Now => DateTime.Now.ToString("HH:mm:ss.fff");

    static int Main(string[] args)
    {
        int seconds = args.Length > 0 ? int.Parse(args[0]) : 30;
        CoInitializeEx(IntPtr.Zero, 0 /* COINIT_MULTITHREADED */);
        Guid clsid = new("ff48dba4-60ef-4201-aa87-54103eef594e") /* CUIAutomation */, iid = typeof(IUIAutomation).GUID;
        IntPtr p; int hr = CoCreateInstance(&clsid, IntPtr.Zero, 1 /* INPROC */, &iid, &p);
        if (hr < 0) { Console.WriteLine($"CoCreateInstance 0x{hr:X8}"); return 1; }
        var cw = new StrategyBasedComWrappers();
        Uia = (IUIAutomation)cw.GetOrCreateObjectForComInstance(p, CreateObjectFlags.None);
        Marshal.Release(p);

        var root = Uia.GetRootElement();
        Console.WriteLine($"[{Now}] AOT probe up; root=\"{root.get_CurrentName()}\" focused=\"{Uia.GetFocusedElement().get_CurrentName()}\"");
        var cr = Uia.CreateCacheRequest(); cr.AddProperty(30005 /* Name */); cr.AddProperty(30002 /* ProcessId */); cr.AddProperty(30003 /* ControlType */);
        var h = new Handler();
        Uia.AddFocusChangedEventHandler(cr, h); // cached: the clicked MenuFlyoutItem is gone before the handler runs
        Uia.AddAutomationEventHandler(20009 /* Invoke_Invoked */, root, 7 /* Subtree */, cr, h);

        // MenuOpened is only delivered when registered on the Start window element (top-level child of StartMenuExperienceHost).
        var startPid = System.Diagnostics.Process.GetProcessesByName("StartMenuExperienceHost").FirstOrDefault()?.Id ?? -1;
        var start = root.FindFirst(2 /* Children */, Uia.CreatePropertyCondition(30002, ComVariant.Create(startPid)));
        if (start != null)
        {
            Uia.AddAutomationEventHandler(20003 /* MenuOpened */, start, 7, cr, h);
            Console.WriteLine($"[{Now}] registered MenuOpened on Start window \"{start.get_CurrentName()}\" class={start.get_CurrentClassName()}");
        }
        else Console.WriteLine($"[{Now}] Start not open: MenuOpened not registered (the real app registers on first focus into Start)");
        Thread.Sleep(seconds * 1000);
        Uia.RemoveAllEventHandlers();
        return 0;
    }
}

[GeneratedComClass]
sealed partial class Handler : IUIAutomationEventHandler, IUIAutomationFocusChangedEventHandler
{
    string? lastTile;
    public void HandleFocusChangedEvent(IUIAutomationElement sender)
    {
        try
        {
            if (sender.get_CachedControlType() == 50011 && sender.get_CachedName() == "Uninstall") Console.WriteLine($"[{Program.Now}] focus -> cached MenuItem \"Uninstall\" (pid {sender.get_CachedProcessId()}) => DETECTED Uninstall of {lastTile}");
            var ct = sender.get_CurrentControlType();
            var aid = sender.get_CurrentAutomationId();
            Console.WriteLine($"[{Program.Now}] FOCUS ct={ct} name=\"{sender.get_CurrentName()}\" aid=\"{aid}\" pid={sender.get_CurrentProcessId()}");
            if (ct == 50007 && aid is not null && (aid.StartsWith("W~") || aid.StartsWith("tile-W~") || aid.StartsWith("P~") || aid.StartsWith("tile-P~"))) lastTile = aid;
        }
        catch (Exception ex) { Console.WriteLine("focus err " + ex.Message); }
    }

    public void HandleAutomationEvent(IUIAutomationElement sender, int eventId)
    {
        string name = ""; try { name = sender.get_CachedName(); } catch { }
        Console.WriteLine($"[{Program.Now}] EVT {eventId} cachedName=\"{name}\"" + (eventId == 20009 && name == "Uninstall" ? $" => DETECTED Uninstall of {lastTile}" : ""));
    }
}

[StructLayout(LayoutKind.Sequential)] public struct RECT { public int left, top, right, bottom; }

[GeneratedComInterface, Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee")]
public partial interface IUIAutomation
{
    void _CompareElements(); void _CompareRuntimeIds();
    IUIAutomationElement GetRootElement();
    void _ElementFromHandle(); void _ElementFromPoint();
    IUIAutomationElement GetFocusedElement();
    void _s6(); void _s7(); void _s8(); void _s9(); void _s10(); void _s11(); void _s12(); void _s13(); void _s14(); void _s15(); void _s16();
    IUIAutomationCacheRequest CreateCacheRequest();
    void _s18(); void _s19();
    IUIAutomationCondition CreatePropertyCondition(int propertyId, ComVariant value);
    void _s21(); void _s22(); void _s23(); void _s24(); void _s25(); void _s26(); void _s27(); void _s28();
    void AddAutomationEventHandler(int eventId, IUIAutomationElement element, int scope, IUIAutomationCacheRequest? cacheRequest, IUIAutomationEventHandler handler);
    void RemoveAutomationEventHandler(int eventId, IUIAutomationElement element, IUIAutomationEventHandler handler);
    void _s31(); void _s32(); void _s33(); void _s34(); void _s35();
    void AddFocusChangedEventHandler(IUIAutomationCacheRequest? cacheRequest, IUIAutomationFocusChangedEventHandler handler);
    void RemoveFocusChangedEventHandler(IUIAutomationFocusChangedEventHandler handler);
    void RemoveAllEventHandlers();
}

[GeneratedComInterface, Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e")]
public partial interface IUIAutomationElement
{
    void _SetFocus(); void _GetRuntimeId();
    IUIAutomationElement? FindFirst(int scope, IUIAutomationCondition condition);
    IUIAutomationElementArray FindAll(int scope, IUIAutomationCondition condition);
    void _s4(); void _s5(); void _s6(); void _s7(); void _s8(); void _s9(); void _s10(); void _s11(); void _s12(); void _s13(); void _s14(); void _s15(); void _s16();
    int get_CurrentProcessId();
    int get_CurrentControlType();
    [return: MarshalAs(UnmanagedType.BStr)] string get_CurrentLocalizedControlType();
    [return: MarshalAs(UnmanagedType.BStr)] string get_CurrentName();
    void _s21(); void _s22(); void _s23(); void _s24(); void _s25();
    [return: MarshalAs(UnmanagedType.BStr)] string get_CurrentAutomationId();
    [return: MarshalAs(UnmanagedType.BStr)] string get_CurrentClassName();
    void _c28(); void _c29(); void _c30(); void _c31(); void _c32(); void _c33(); void _c34(); void _c35(); void _c36(); void _c37(); void _c38(); void _c39();
    RECT get_CurrentBoundingRectangle();
    void _c41(); void _c42(); void _c43(); void _c44(); void _c45(); void _c46(); void _c47(); void _c48();
    int get_CachedProcessId();
    int get_CachedControlType();
    [return: MarshalAs(UnmanagedType.BStr)] string get_CachedLocalizedControlType();
    [return: MarshalAs(UnmanagedType.BStr)] string get_CachedName();
}

[GeneratedComInterface, Guid("14314595-b4bc-4055-95f2-58f2e42c9855")]
public partial interface IUIAutomationElementArray
{
    int get_Length();
    IUIAutomationElement GetElement(int index);
}

[GeneratedComInterface, Guid("352ffba8-0973-437c-a61f-f64cafd81df9")]
public partial interface IUIAutomationCondition { }

[GeneratedComInterface, Guid("b32a92b5-bc25-4078-9c08-d7ee95c48e03")]
public partial interface IUIAutomationCacheRequest
{
    void AddProperty(int propertyId);
}

[GeneratedComInterface, Guid("146c3c17-f12e-4e22-8c27-f894b9b79c69")]
public partial interface IUIAutomationEventHandler { void HandleAutomationEvent(IUIAutomationElement sender, int eventId); }

[GeneratedComInterface, Guid("c270f6b5-5c69-4290-9745-7a7f97169468")]
public partial interface IUIAutomationFocusChangedEventHandler { void HandleFocusChangedEvent(IUIAutomationElement sender); }
