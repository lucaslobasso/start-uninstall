// Start Uninstall: makes Start's own "Uninstall" on a Win32 app run that app's uninstaller instead of only opening
// Settings > Installed apps. No window, no polling: UI Automation events (global focus changes, Invoke_Invoked and
// WindowOpened) feed a worker thread (Detector). `StartUninstall.exe --quit` stops the running copy.
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

[assembly: System.Runtime.CompilerServices.DisableRuntimeMarshalling]
[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]

namespace StartUninstall;

static class Program
{
    const string MutexName = @"Local\StartUninstall.Singleton", QuitName = @"Local\StartUninstall.Quit";

    [MTAThread]
    static int Main(string[] args)
    {
        if (args is ["--quit"])
        {
            if (EventWaitHandle.TryOpenExisting(QuitName, out var q)) { q.Set(); q.Dispose(); return 0; }
            return 1; // not running
        }

        using var mutex = new Mutex(true, MutexName, out bool first);
        if (!first) return 0;
        using var quit = new EventWaitHandle(false, EventResetMode.ManualReset, QuitName);

        Native.CoInitializeEx(0, Native.COINIT_MULTITHREADED);
        Uia.Create();
        var uia = Uia.Instance;
        // Cached properties: the clicked menu item is gone by the time a handler could ask for them.
        var cr = uia.CreateCacheRequest();
        foreach (var id in new[] { UiaIds.Name, UiaIds.ControlType, UiaIds.ProcessId, UiaIds.AutomationId, UiaIds.ClassName, UiaIds.BoundingRectangle })
            cr.AddProperty(id);
        var handler = new Handler();
        var root = uia.GetRootElement();
        uia.AddFocusChangedEventHandler(cr, handler);
        uia.AddAutomationEventHandler(UiaIds.InvokeInvoked, root, UiaIds.Subtree, cr, handler);
        uia.AddAutomationEventHandler(UiaIds.WindowOpened, root, UiaIds.Children, cr, handler);

        new Thread(Detector.Run) { IsBackground = true, Name = "detector" }.Start();
        Log.Write($"start: pid={Environment.ProcessId} v{typeof(Program).Assembly.GetName().Version?.ToString(3)} debug={Log.Debug}" + (Environment.GetEnvironmentVariable("STARTUNINSTALL_SIMULATE") is { Length: > 0 } sim ? $" SIMULATE={sim}" : ""));

        quit.WaitOne();
        try { uia.RemoveAllEventHandlers(); } catch { }
        Log.Write("stop (--quit)");
        return 0;
    }
}

[GeneratedComClass]
sealed partial class Handler : IUIAutomationEventHandler, IUIAutomationFocusChangedEventHandler
{
    // Runs on a UIA thread: read the cache, queue, return. Never call into UIA (or (un)register handlers) from here.
    public void HandleFocusChangedEvent(IUIAutomationElement sender) => Detector.Post(Read(EvKind.Focus, sender));

    public void HandleAutomationEvent(IUIAutomationElement sender, int eventId)
    {
        if (eventId == UiaIds.InvokeInvoked) Detector.Post(Read(EvKind.Invoke, sender));
        else if (eventId == UiaIds.WindowOpened) Detector.Post(Read(EvKind.WindowOpened, sender));
    }

    static unsafe Ev Read(EvKind kind, IUIAutomationElement e)
    {
        var at = DateTime.Now;
        Native.POINT mouse;
        Native.GetCursorPos(&mouse);
        string name = "", aid = "", cls = "";
        int ct = 0, pid = 0;
        RECT rect = default;
        try { name = e.get_CachedName() ?? ""; } catch { }
        try { ct = e.get_CachedControlType(); } catch { }
        try { pid = e.get_CachedProcessId(); } catch { }
        try { aid = e.get_CachedAutomationId() ?? ""; } catch { }
        try { cls = e.get_CachedClassName() ?? ""; } catch { }
        try { rect = e.get_CachedBoundingRectangle(); } catch { }
        return new Ev(kind, at, name, ct, pid, aid, cls, rect, mouse.x, mouse.y);
    }
}
