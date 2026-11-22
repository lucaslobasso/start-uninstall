using System.Text.RegularExpressions;

namespace StartUninstall;

public readonly record struct UninstallCommand(string FileName, string Arguments)
{
    private static readonly Regex MsiInstall = new(@"(?<![\w/])/[iI](?=\s*\{)", RegexOptions.Compiled);
    private static readonly Regex Executable = new(@"\.(?:exe|bat|cmd|com)(?=\s|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Builds the command from UninstallString only (never QuietUninstallString).</summary>
    public static UninstallCommand Build(UninstallEntry entry, Func<string, bool>? fileExists = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var raw = entry.UninstallString?.Trim();
        if (string.IsNullOrEmpty(raw)) throw new ArgumentException("Entry has no UninstallString.", nameof(entry));
        fileExists ??= File.Exists;

        var (file, args) = Split(raw, fileExists);
        if (IsMsiExec(file)) args = MsiInstall.Replace(args, "/X");
        return new UninstallCommand(file, args.Trim());
    }

    private static bool IsMsiExec(string file)
    {
        var f = file.Replace('/', '\\');
        var n = f[(f.LastIndexOf('\\') + 1)..];
        return n.Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase) || n.Equals("msiexec", StringComparison.OrdinalIgnoreCase);
    }

    private static (string file, string args) Split(string s, Func<string, bool> exists)
    {
        if (s[0] == '"')
        {
            var end = s.IndexOf('"', 1);
            if (end < 0) return Split(s[1..].TrimStart(), exists); // unterminated quote
            var inner = s[1..end].Trim();
            var rest = s[(end + 1)..].TrimStart();
            // "C:\x\un.exe /S": the whole command quoted
            if (rest.Length == 0 && Executable.IsMatch(inner) && !exists(Expand(inner)))
            {
                var (f, a) = Split(inner, exists);
                if (exists(f)) return (f, a);
            }
            return (Expand(inner), rest);
        }

        string? firstGuess = null;
        foreach (Match m in Executable.Matches(s))
        {
            var end = m.Index + m.Length;
            firstGuess ??= s[..end];
            var file = Expand(s[..end]);
            if (exists(file)) return (file, s[end..].TrimStart());
        }
        if (firstGuess is not null) return (Expand(firstGuess), s[firstGuess.Length..].TrimStart());

        var sp = s.IndexOfAny(new[] { ' ', '\t' });
        return sp < 0 ? (Expand(s), "") : (Expand(s[..sp]), s[(sp + 1)..].TrimStart());
    }

    private static string Expand(string s) => Environment.ExpandEnvironmentVariables(s);
}
