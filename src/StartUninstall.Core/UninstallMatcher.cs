using System.Text.RegularExpressions;

namespace StartUninstall;

public enum MatchKind { None, Exact, Single, Ambiguous }

public sealed record MatchResult(MatchKind Kind, UninstallEntry? Entry, IReadOnlyList<UninstallEntry> Candidates)
{
    public static readonly MatchResult NoMatch = new(MatchKind.None, null, Array.Empty<UninstallEntry>());
}

public sealed class UninstallMatcher
{
    private readonly List<UninstallEntry> _entries;

    public UninstallMatcher(IEnumerable<UninstallEntry> entries) => _entries = entries.ToList();

    public UninstallMatcher(IUninstallSource source) : this(source.GetEntries()) { }

    public MatchResult Match(string startDisplayName, string? shortcutTargetPath = null)
    {
        if (string.IsNullOrWhiteSpace(startDisplayName)) return MatchResult.NoMatch;
        var name = startDisplayName.Trim();
        var named = _entries.Where(e => !string.IsNullOrWhiteSpace(e.DisplayName)).ToList();

        var exact = named.Where(e => string.Equals(e.DisplayName!.Trim(), name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count > 0) return Resolve(exact, MatchKind.Exact, shortcutTargetPath);

        var norm = Normalize(name);
        if (norm.Length > 0)
        {
            var normalized = named.Where(e => Normalize(e.DisplayName!) == norm).ToList();
            if (normalized.Count > 0) return Resolve(normalized, MatchKind.Single, shortcutTargetPath);

            var prefix = named.Where(e => IsPrefixMatch(norm, Normalize(e.DisplayName!))).ToList();
            if (prefix.Count > 0) return Resolve(prefix, MatchKind.Single, shortcutTargetPath);
        }

        if (!string.IsNullOrWhiteSpace(shortcutTargetPath))
        {
            var byPath = ByShortcut(named, shortcutTargetPath!);
            if (byPath.Count > 0) return Resolve(byPath, MatchKind.Single, null);
        }
        return MatchResult.NoMatch;
    }

    private static MatchResult Resolve(List<UninstallEntry> hits, MatchKind kind, string? target)
    {
        var d = Dedupe(hits);
        if (d.Count > 1 && !string.IsNullOrWhiteSpace(target))
        {
            var narrowed = Dedupe(ByShortcut(d, target!));
            if (narrowed.Count > 0) d = narrowed;
        }
        if (d.Count == 1) return new MatchResult(kind, d[0], d);

        // Tie-break by hive/view only when every candidate has the identical name.
        var names = d.Select(e => e.DisplayName!.Trim().ToLowerInvariant()).Distinct().Count();
        if (names == 1)
        {
            var best = d.OrderBy(e => e.Priority).ToList();
            if (best[0].Priority < best[1].Priority) return new MatchResult(kind, best[0], d);
        }
        return new MatchResult(MatchKind.Ambiguous, null, d);
    }

    /// <summary>Collapses the same app registered in several views (same name + uninstall command).</summary>
    private static List<UninstallEntry> Dedupe(IEnumerable<UninstallEntry> items)
    {
        var map = new Dictionary<string, UninstallEntry>();
        foreach (var e in items.OrderBy(e => e.Priority))
        {
            var key = e.DisplayName!.Trim().ToLowerInvariant() + "|" + (e.UninstallString ?? "").Trim().ToLowerInvariant();
            map.TryAdd(key, e);
        }
        return map.Values.ToList();
    }

    // ---- name normalization -------------------------------------------------

    private static readonly Regex ArchParen = new(@"[\(\[][^\)\]]*\b(?:x64|x86|amd64|64-?bit|32-?bit)\b[^\)\]]*[\)\]]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ArchWord = new(@"\b(?:x64|x86|amd64|64-?bit|32-?bit)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Version = new(@"\bv?\d+(?:\.\d+)+(?:[-+.]?(?:beta|alpha|rc|preview)\d*)?\b|\bv\d+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex VersionWord = new(@"\bversion\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NonAlnum = new(@"[^\p{L}\p{N}]+", RegexOptions.Compiled);

    internal static string Normalize(string s)
    {
        s = s.Replace("™", "").Replace("®", "").Replace("©", "");
        s = ArchParen.Replace(s, " ");
        s = ArchWord.Replace(s, " ");
        s = Version.Replace(s, " ");
        s = VersionWord.Replace(s, " ");
        s = s.Replace("+", "plus"); // keep "Notepad++" distinct from "Notepad"
        return NonAlnum.Replace(s, " ").Trim().ToLowerInvariant();
    }

    private static bool IsPrefixMatch(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return false;
        var (shorter, longer) = a.Length <= b.Length ? (a, b) : (b, a);
        if (shorter.Length < 3) return false;
        return longer.StartsWith(shorter, StringComparison.Ordinal)
            && (longer.Length == shorter.Length || longer[shorter.Length] == ' ');
    }

    // ---- shortcut-target matching ------------------------------------------

    private static readonly string[] BroadDirs =
    {
        "program files", "program files (x86)", "programdata", "windows", "users", "system32", "syswow64",
        "windowsapps", "installer", "appdata", "local", "roaming", "temp", "common files", "programs",
    };

    private static List<UninstallEntry> ByShortcut(IEnumerable<UninstallEntry> items, string target)
    {
        var t = Clean(target);
        if (t is null) return new();
        var scored = new List<(UninstallEntry e, int score)>();
        foreach (var e in items)
        {
            var score = 0;
            var icon = IconPath(e.DisplayIcon);
            if (icon is not null && PathEq(icon, t)) score = 300;
            var loc = Clean(e.InstallLocation);
            if (loc is not null && !IsBroad(loc) && IsUnder(t, loc)) score = Math.Max(score, 200 + Depth(loc));
            if (score == 0 && icon is not null)
            {
                var dir = Path.GetDirectoryName(icon);
                var tdir = Path.GetDirectoryName(t);
                if (dir is not null && tdir is not null && !IsBroad(dir) && PathEq(dir, tdir)) score = 100;
            }
            if (score > 0) scored.Add((e, score));
        }
        if (scored.Count == 0) return new();
        var max = scored.Max(x => x.score);
        return scored.Where(x => x.score == max).Select(x => x.e).ToList();
    }

    private static string? Clean(string? p)
    {
        if (string.IsNullOrWhiteSpace(p)) return null;
        p = Environment.ExpandEnvironmentVariables(p.Trim().Trim('"')).Replace('/', '\\').TrimEnd('\\');
        return p.Length == 0 ? null : p;
    }

    internal static string? IconPath(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon)) return null;
        var s = icon.Trim();
        if (s.StartsWith('"'))
        {
            var end = s.IndexOf('"', 1);
            s = end > 0 ? s[1..end] : s[1..];
        }
        else
        {
            var comma = s.LastIndexOf(',');
            if (comma > 0 && int.TryParse(s[(comma + 1)..].Trim(), out _)) s = s[..comma];
        }
        return Clean(s);
    }

    private static bool PathEq(string a, string b) =>
        string.Equals(a.TrimEnd('\\'), b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

    private static bool IsUnder(string path, string dir) =>
        path.StartsWith(dir + "\\", StringComparison.OrdinalIgnoreCase);

    private static int Depth(string p) => p.Split('\\', StringSplitOptions.RemoveEmptyEntries).Length;

    private static bool IsBroad(string dir)
    {
        var parts = dir.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1) return true; // drive root
        if (parts[1].Equals("windows", StringComparison.OrdinalIgnoreCase)) return true; // anything under C:Windows
        return BroadDirs.Contains(parts[^1].ToLowerInvariant());
    }
}
