// Raw UIAutomationClient COM through [GeneratedComInterface] (ComWrappers source generator): no interop assembly, no
// reflection, works under Native AOT. Vtable slots that are never called are placeholders (_sN / _cN) that keep the
// layout; the slot numbers follow UIAutomationClient.h.
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace StartUninstall;

static class UiaIds
{
    // properties
    public const int BoundingRectangle = 30001, ProcessId = 30002, ControlType = 30003, Name = 30005, AutomationId = 30011, ClassName = 30012;
    // control types
    public const int ListItemType = 50007, MenuItemType = 50011, EditType = 50004;
    // events
    public const int MenuOpened = 20003, InvokeInvoked = 20009, WindowOpened = 20016;
    // patterns
    public const int ValuePattern = 10002, WindowPattern = 10009;
    // scopes
    public const int Element = 1, Children = 2, Descendants = 4, Subtree = 7;
}

[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int left, top, right, bottom;
    public readonly bool Contains(int x, int y) => x >= left && x <= right && y >= top && y <= bottom;
    public readonly bool IsEmpty => right <= left || bottom <= top;
    public override readonly string ToString() => $"({left},{top},{right},{bottom})";
}

[GeneratedComInterface, Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee")]
public partial interface IUIAutomation
{
    void _CompareElements(); void _CompareRuntimeIds();
    IUIAutomationElement GetRootElement();
    IUIAutomationElement ElementFromHandle(nint hwnd);
    void _ElementFromPoint();
    IUIAutomationElement GetFocusedElement();
    void _s6(); void _s7(); void _s8(); void _s9(); void _s10(); void _s11(); void _s12(); void _s13(); void _s14(); void _s15(); void _s16();
    IUIAutomationCacheRequest CreateCacheRequest();
    IUIAutomationCondition CreateTrueCondition();
    void _s19();
    IUIAutomationCondition CreatePropertyCondition(int propertyId, ComVariant value);
    void _s21();
    IUIAutomationCondition CreateAndCondition(IUIAutomationCondition a, IUIAutomationCondition b);
    void _s23(); void _s24(); void _s25(); void _s26(); void _s27(); void _s28();
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
    void _s4(); void _s5(); void _s6(); void _s7(); void _s8(); void _s9(); void _s10(); void _s11(); void _s12();
    nint GetCurrentPattern(int patternId); // IUnknown*, wrapped by Uia.Pattern<T>
    void _s14(); void _s15(); void _s16();
    int get_CurrentProcessId();
    int get_CurrentControlType();
    void _s19();
    [return: MarshalAs(UnmanagedType.BStr)] string get_CurrentName();
    void _s21(); void _s22(); void _s23(); void _s24(); void _s25();
    [return: MarshalAs(UnmanagedType.BStr)] string get_CurrentAutomationId();
    [return: MarshalAs(UnmanagedType.BStr)] string get_CurrentClassName();
    void _c28(); void _c29(); void _c30(); void _c31(); void _c32(); void _c33(); void _c34(); void _c35(); void _c36(); void _c37(); void _c38(); void _c39();
    RECT get_CurrentBoundingRectangle();
    void _c41(); void _c42(); void _c43(); void _c44(); void _c45(); void _c46(); void _c47(); void _c48();
    int get_CachedProcessId();
    int get_CachedControlType();
    void _c51();
    [return: MarshalAs(UnmanagedType.BStr)] string get_CachedName();
    void _c53(); void _c54(); void _c55(); void _c56(); void _c57();
    [return: MarshalAs(UnmanagedType.BStr)] string get_CachedAutomationId();
    [return: MarshalAs(UnmanagedType.BStr)] string get_CachedClassName();
    void _c60(); void _c61(); void _c62(); void _c63(); void _c64(); void _c65(); void _c66(); void _c67(); void _c68(); void _c69(); void _c70(); void _c71();
    RECT get_CachedBoundingRectangle();
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

[GeneratedComInterface, Guid("a94cd8b1-0844-4cd6-9d2d-640537ab39e9")]
public partial interface IUIAutomationValuePattern
{
    void SetValue([MarshalAs(UnmanagedType.BStr)] string value);
    [return: MarshalAs(UnmanagedType.BStr)] string get_CurrentValue();
}

[GeneratedComInterface, Guid("0faef453-9208-43ef-bbb2-3b485177864f")]
public partial interface IUIAutomationWindowPattern
{
    void Close();
}

[GeneratedComInterface, Guid("146c3c17-f12e-4e22-8c27-f894b9b79c69")]
public partial interface IUIAutomationEventHandler { void HandleAutomationEvent(IUIAutomationElement sender, int eventId); }

[GeneratedComInterface, Guid("c270f6b5-5c69-4290-9745-7a7f97169468")]
public partial interface IUIAutomationFocusChangedEventHandler { void HandleFocusChangedEvent(IUIAutomationElement sender); }

/// <summary>The one CUIAutomation instance plus small helpers. Every method may throw (elements die at any time).</summary>
static unsafe class Uia
{
    [DllImport("ole32.dll")] static extern int CoCreateInstance(Guid* clsid, nint outer, uint ctx, Guid* iid, nint* ppv);

    static readonly StrategyBasedComWrappers Wrappers = new();
    public static IUIAutomation Instance = null!;

    public static void Create()
    {
        Guid clsid = new("ff48dba4-60ef-4201-aa87-54103eef594e") /* CUIAutomation */, iid = typeof(IUIAutomation).GUID;
        nint p;
        int hr = CoCreateInstance(&clsid, 0, 1 /* CLSCTX_INPROC_SERVER */, &iid, &p);
        if (hr < 0) throw new COMException("CoCreateInstance(CUIAutomation) failed", hr);
        Instance = (IUIAutomation)Wrappers.GetOrCreateObjectForComInstance(p, CreateObjectFlags.None);
        Marshal.Release(p);
    }

    public static T? Pattern<T>(IUIAutomationElement e, int patternId) where T : class
    {
        nint p = e.GetCurrentPattern(patternId);
        if (p == 0) return null;
        try { return (T)Wrappers.GetOrCreateObjectForComInstance(p, CreateObjectFlags.None); }
        finally { Marshal.Release(p); }
    }

    public static IUIAutomationCondition Prop(int propertyId, int value)
    {
        var v = ComVariant.Create(value);
        return Instance.CreatePropertyCondition(propertyId, v);
    }

    public static IUIAutomationCondition Prop(int propertyId, string value)
    {
        var v = ComVariant.Create(value);
        try { return Instance.CreatePropertyCondition(propertyId, v); }
        finally { v.Dispose(); }
    }

    public static IEnumerable<IUIAutomationElement> Each(IUIAutomationElementArray? a)
    {
        if (a is null) yield break;
        int n = a.get_Length();
        for (int i = 0; i < n; i++) yield return a.GetElement(i);
    }
}
