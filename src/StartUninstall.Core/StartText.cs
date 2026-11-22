namespace StartUninstall;

public enum TileKind { None, Win32, Packaged }

/// <summary>What a Start list item's AutomationId says about the app behind it.</summary>
public readonly record struct StartTile(TileKind Kind, string Aumid)
{
    /// <summary>
    /// <c>tile-W~&lt;AUMID&gt;</c> (pinned) and <c>W~&lt;AUMID&gt;</c> (All apps) are Win32 apps, <c>P~</c> / <c>tile-P~</c>
    /// packaged ones. Anything else (folders, headers, recommended files) is <see cref="TileKind.None"/>.
    /// </summary>
    public static StartTile Parse(string? automationId)
    {
        if (string.IsNullOrEmpty(automationId)) return default;
        var s = automationId.StartsWith("tile-", StringComparison.Ordinal) ? automationId[5..] : automationId;
        if (s.Length > 2 && s[1] == '~')
        {
            if (s[0] == 'W') return new StartTile(TileKind.Win32, s[2..]);
            if (s[0] == 'P') return new StartTile(TileKind.Packaged, s[2..]);
        }
        return default;
    }
}

public static class StartText
{
    /// <summary>
    /// The Start context menu's "Uninstall" in the display languages we know. Other languages still work through the
    /// fallback (a menu item on a Win32 tile followed by Settings' Installed apps).
    /// </summary>
    public static readonly string[] UninstallNames =
    {
        "Uninstall",        // en
        "Desinstalar",      // es, pt
        "Deinstallieren",   // de
        "Désinstaller",     // fr
        "Disinstalla",      // it
        "Odinstaluj",       // pl
        "Verwijderen",      // nl
        "Avinstallera",     // sv
        "Kaldır",           // tr
        "アンインストール",   // ja
        "제거",              // ko
        "卸载",              // zh-Hans
        "解除安裝",          // zh-Hant
        "Удалить",          // ru
    };

    /// <summary>A Start tile's accessible name without the ", New" badge of recently installed apps (English only).</summary>
    public static string TileName(string? name)
    {
        var n = (name ?? "").Trim();
        return n.EndsWith(", New", StringComparison.Ordinal) ? n[..^5].TrimEnd() : n;
    }

    public static bool IsUninstall(string? menuItemName)
    {
        if (string.IsNullOrWhiteSpace(menuItemName)) return false;
        var n = menuItemName.Trim().Replace("&", "");
        foreach (var u in UninstallNames)
            if (string.Equals(n, u, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// The app name of a Start search result row: AutomationId <c>PP&lt;name&gt;</c> for apps, otherwise the first
    /// comma-separated part of the accessible name (" Notepad++, App, Press right to view options").
    /// Returns null for rows that are files (their AutomationId is a path).
    /// </summary>
    public static string? SearchResultName(string? automationId, string? name)
    {
        // Search wraps the part that matched the typed text in private-use marks: "PP\uE000Fake App\uE001".
        automationId = StripPrivateUse(automationId);
        name = StripPrivateUse(name);
        if (!string.IsNullOrEmpty(automationId))
        {
            if (automationId.StartsWith("PP", StringComparison.Ordinal) && automationId.Length > 2) return automationId[2..].Trim();
            if (automationId.Contains('\\') || automationId.Contains('/')) return null; // a file or folder
        }
        if (string.IsNullOrWhiteSpace(name)) return null;
        var comma = name.IndexOf(", ", StringComparison.Ordinal);
        var first = (comma > 0 ? name[..comma] : name).Trim();
        return first.Length > 0 ? first : null;
    }

    private static string? StripPrivateUse(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s) if (c < '\uE000' || c > '\uF8FF') sb.Append(c);
        return sb.ToString();
    }
}
