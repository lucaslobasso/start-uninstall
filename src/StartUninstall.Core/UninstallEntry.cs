namespace StartUninstall;

public enum UninstallHive { LocalMachine, CurrentUser }

/// <summary>One Add/Remove Programs registry entry.</summary>
public sealed record UninstallEntry(
    string KeyPath,
    UninstallHive Hive,
    int View,
    string? DisplayName,
    string? UninstallString,
    string? QuietUninstallString = null,
    string? InstallLocation = null,
    string? DisplayIcon = null,
    string? Publisher = null,
    bool WindowsInstaller = false,
    string? ProductCode = null,
    bool SystemComponent = false,
    string? ParentKeyName = null)
{
    /// <summary>Lower is better: HKLM-64, HKLM-32, HKCU-64, HKCU-32.</summary>
    internal int Priority => (Hive == UninstallHive.LocalMachine ? 0 : 2) + (View == 64 ? 0 : 1);
}
