// What Start's app is on disk: AUMID -> shell:AppsFolder item -> System.Link.TargetParsingPath (the exe), the Start
// menu shortcut by name, and for MSI advertised shortcuts the ProductCode (MsiGetShortcutTarget). Raw vtable calls on
// IShellItem2, so nothing here needs marshalling or reflection.
using System.Runtime.InteropServices;

namespace StartUninstall;

sealed record ResolvedApp(string Name, string? Target, string? Shortcut, string? ProductCode);

static unsafe class AppResolver
{
    [StructLayout(LayoutKind.Sequential)] struct PROPERTYKEY { public Guid fmtid; public uint pid; }

    static readonly Guid IID_IShellItem2 = new("7e9fb0d3-919f-4307-ab2e-9b1860310c93");
    static readonly PROPERTYKEY PKEY_Link_TargetParsingPath = new() { fmtid = new("b9b4b3fc-2b51-4a42-b5d8-324146afcf25"), pid = 2 };
    static readonly PROPERTYKEY PKEY_ItemNameDisplay = new() { fmtid = new("b725f130-47ef-101a-a5f1-02608c9eebac"), pid = 10 };

    /// <summary>A Win32 tile: <paramref name="aumid"/> from its AutomationId, <paramref name="tileName"/> as shown.</summary>
    public static ResolvedApp FromAumid(string aumid, string tileName)
    {
        string? name = null, target = null;
        var item = ParseItem(@"shell:AppsFolder\" + aumid);
        if (item != 0)
        {
            try
            {
                name = GetString(item, PKEY_ItemNameDisplay);
                target = GetString(item, PKEY_Link_TargetParsingPath);
            }
            finally { Marshal.Release(item); }
        }
        target ??= KnownFolderPath(aumid); // "{6D809377-...}\Transmission\transmission-qt.exe" style AUMIDs
        if (string.IsNullOrWhiteSpace(name)) name = tileName;
        return WithShortcut(name!.Trim(), target);
    }

    /// <summary>A search result: only its display name is known.</summary>
    public static ResolvedApp FromName(string name) => WithShortcut(name.Trim(), null);

    static ResolvedApp WithShortcut(string name, string? target)
    {
        var lnk = FindShortcut(name);
        string? code = null;
        if (lnk is not null)
        {
            code = MsiProductCode(lnk);
            if (target is null)
            {
                var item = ParseItem(lnk);
                if (item != 0)
                {
                    try { target = GetString(item, PKEY_Link_TargetParsingPath); }
                    finally { Marshal.Release(item); }
                }
            }
        }
        target = target?.Trim().Trim('"');
        return new ResolvedApp(name, string.IsNullOrWhiteSpace(target) ? null : target, lnk, code);
    }

    static nint ParseItem(string path)
    {
        nint p = 0;
        Guid iid = IID_IShellItem2;
        fixed (char* s = path)
        {
            int hr = Native.SHCreateItemFromParsingName(s, 0, &iid, &p);
            return hr < 0 ? 0 : p;
        }
    }

    // IShellItem2::GetString is vtable slot 3 (IUnknown) + 5 (IShellItem) + 9.
    static string? GetString(nint item, PROPERTYKEY key)
    {
        var vtbl = *(nint**)item;
        var fn = (delegate* unmanaged[Stdcall]<nint, PROPERTYKEY*, char**, int>)vtbl[3 + 5 + 9];
        char* s = null;
        int hr = fn(item, &key, &s);
        if (hr < 0 || s == null) return null;
        try { var v = new string(s); return v.Length == 0 ? null : v; }
        finally { Native.CoTaskMemFree((nint)s); }
    }

    static string? KnownFolderPath(string aumid)
    {
        if (aumid.Length > 2 && aumid[1] == ':') return aumid; // a plain path
        if (aumid.Length < 39 || aumid[0] != '{' || aumid[37] != '}' || !Guid.TryParse(aumid[..38], out var id)) return null;
        nint p = 0;
        if (Native.SHGetKnownFolderPath(&id, 0, 0, &p) < 0 || p == 0) return null;
        try { return Path.Combine(new string((char*)p), aumid[38..].TrimStart('\\')); }
        finally { Native.CoTaskMemFree(p); }
    }

    /// <summary>The Start menu shortcut named like the app (per-user folder first, then all users).</summary>
    static string? FindShortcut(string name)
    {
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Programs), Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms) })
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
            try
            {
                foreach (var f in Directory.EnumerateFiles(root, "*.lnk", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 4 }))
                    if (string.Equals(Path.GetFileNameWithoutExtension(f), name, StringComparison.OrdinalIgnoreCase)) return f;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return null;
    }

    static string? MsiProductCode(string lnk)
    {
        char* code = stackalloc char[39], feature = stackalloc char[39], comp = stackalloc char[39];
        code[0] = '\0';
        fixed (char* p = lnk)
        {
            uint r;
            try { r = Native.MsiGetShortcutTargetW(p, code, feature, comp); }
            catch (DllNotFoundException) { return null; }
            if (r != 0 || code[0] == '\0') return null;
        }
        return new string(code);
    }
}
