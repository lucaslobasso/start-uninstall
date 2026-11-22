namespace StartUninstall;

public interface IUninstallSource
{
    /// <summary>Entries that are real, user-visible apps (already filtered).</summary>
    IReadOnlyList<UninstallEntry> GetEntries();
}
