using System.Runtime.Versioning;
using Microsoft.Win32;

namespace StartUninstall;

/// <summary>Reads HKLM (64 and 32 bit views) and HKCU (both views) uninstall keys.</summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryUninstallSource : IUninstallSource
{
    public const string UninstallPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    public IReadOnlyList<UninstallEntry> GetEntries()
    {
        var list = new List<UninstallEntry>();
        Read(list, RegistryHive.LocalMachine, RegistryView.Registry64, UninstallHive.LocalMachine, 64);
        Read(list, RegistryHive.LocalMachine, RegistryView.Registry32, UninstallHive.LocalMachine, 32);
        Read(list, RegistryHive.CurrentUser, RegistryView.Registry64, UninstallHive.CurrentUser, 64);
        Read(list, RegistryHive.CurrentUser, RegistryView.Registry32, UninstallHive.CurrentUser, 32);
        return list;
    }

    private static bool Benign(Exception ex) =>
        ex is System.Security.SecurityException or UnauthorizedAccessException or IOException;

    private static void Read(List<UninstallEntry> into, RegistryHive hive, RegistryView view, UninstallHive h, int v)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var root = baseKey.OpenSubKey(UninstallPath);
            if (root is null) return;
            var prefix = (h == UninstallHive.LocalMachine ? "HKLM" : "HKCU") + "\\" + UninstallPath + "\\";
            foreach (var name in root.GetSubKeyNames())
            {
                try
                {
                    using var key = root.OpenSubKey(name);
                    if (key is null) continue;
                    var e = FromKey(key, name, prefix + name, h, v);
                    if (e is not null) into.Add(e);
                }
                catch (Exception ex) when (Benign(ex))
                {
                    // unreadable key: skip it
                }
            }
        }
        catch (Exception ex) when (Benign(ex))
        {
        }
    }

    private static string? Str(RegistryKey k, string n) => k.GetValue(n) switch
    {
        string s when !string.IsNullOrWhiteSpace(s) => s.Trim(),
        _ => null,
    };

    private static bool Flag(RegistryKey k, string n) => k.GetValue(n) switch
    {
        int i => i == 1,
        string s => s.Trim() == "1",
        _ => false,
    };

    private static UninstallEntry? FromKey(RegistryKey key, string name, string path, UninstallHive h, int v)
    {
        var entry = new UninstallEntry(
            path, h, v,
            Str(key, "DisplayName"),
            Str(key, "UninstallString"),
            Str(key, "QuietUninstallString"),
            Str(key, "InstallLocation"),
            Str(key, "DisplayIcon"),
            Str(key, "Publisher"),
            Flag(key, "WindowsInstaller"),
            IsGuid(name) ? name : null,
            Flag(key, "SystemComponent"),
            Str(key, "ParentKeyName"));
        return EntryFilter.IsListed(entry) ? entry : null;
    }

    internal static bool IsGuid(string s) =>
        s.Length == 38 && s[0] == '{' && s[^1] == '}' && Guid.TryParse(s, out _);
}

/// <summary>The "is this a real app" rule, shared by sources and tests.</summary>
public static class EntryFilter
{
    public static bool IsListed(UninstallEntry e) =>
        !e.SystemComponent
        && string.IsNullOrEmpty(e.ParentKeyName)
        && !string.IsNullOrWhiteSpace(e.DisplayName)
        && !string.IsNullOrWhiteSpace(e.UninstallString);
}
