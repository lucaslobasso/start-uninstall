using System.Runtime.InteropServices;

namespace StartUninstall;

static unsafe partial class Native
{
    [LibraryImport("ole32.dll")] public static partial int CoInitializeEx(nint reserved, uint coinit);
    public const uint COINIT_MULTITHREADED = 0;

    [LibraryImport("ole32.dll")] public static partial void CoTaskMemFree(nint p);

    [LibraryImport("user32.dll")] public static partial nint GetForegroundWindow();
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x, y; }
    [LibraryImport("user32.dll")] public static partial int GetCursorPos(POINT* p);
    [LibraryImport("user32.dll")] public static partial uint GetWindowThreadProcessId(nint hwnd, int* pid);
    [LibraryImport("user32.dll")] public static partial int GetClassNameW(nint hwnd, char* buf, int max);
    [LibraryImport("user32.dll")] public static partial int PostMessageW(nint hwnd, uint msg, nint w, nint l);
    [LibraryImport("user32.dll")] public static partial int IsWindow(nint hwnd);
    public const uint WM_CLOSE = 0x0010;

    [LibraryImport("shell32.dll")] public static partial int SHCreateItemFromParsingName(char* path, nint bindCtx, Guid* riid, nint* ppv);
    [LibraryImport("shell32.dll")] public static partial int SHGetKnownFolderPath(Guid* id, uint flags, nint token, nint* path);

    [LibraryImport("msi.dll")] public static partial uint MsiGetShortcutTargetW(char* shortcut, char* productCode, char* featureId, char* componentCode);

    public static string ClassName(nint hwnd)
    {
        char* buf = stackalloc char[256];
        int n = GetClassNameW(hwnd, buf, 256);
        return n > 0 ? new string(buf, 0, n) : "";
    }

    public static int WindowPid(nint hwnd)
    {
        int pid = 0;
        GetWindowThreadProcessId(hwnd, &pid);
        return pid;
    }
}
