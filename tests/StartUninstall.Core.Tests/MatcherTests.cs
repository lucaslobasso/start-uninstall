using StartUninstall;
using Xunit;

namespace StartUninstall.Core.Tests;

public sealed class FakeSource(IEnumerable<UninstallEntry> entries) : IUninstallSource
{
    public IReadOnlyList<UninstallEntry> GetEntries() => entries.Where(EntryFilter.IsListed).ToList();
}

internal static class Fx
{
    public static UninstallEntry E(
        string? name, string? uninstall = "\"C:\\x\\unins000.exe\"", UninstallHive hive = UninstallHive.LocalMachine, int view = 64,
        string? loc = null, string? icon = null, string? key = null, bool msi = false, bool sys = false, string? parent = null,
        string? quiet = null)
    {
        key ??= name ?? "k";
        var guid = key.StartsWith('{') ? key : null;
        return new UninstallEntry(
            (hive == UninstallHive.LocalMachine ? "HKLM" : "HKCU") + "\\...\\Uninstall\\" + key, hive, view, name, uninstall,
            quiet, loc, icon, null, msi, guid, sys, parent);
    }

    public static readonly UninstallEntry SevenZip = E("7-Zip 24.08 (x64)", "\"C:\\Program Files\\7-Zip\\Uninstall.exe\"",
        loc: "C:\\Program Files\\7-Zip\\", icon: "C:\\Program Files\\7-Zip\\7zFM.exe", key: "7-Zip");
    public static readonly UninstallEntry Notepad = E("Notepad++ (64-bit x64)", "\"C:\\Program Files\\Notepad++\\uninstall.exe\"",
        loc: "C:\\Program Files\\Notepad++", icon: "C:\\Program Files\\Notepad++\\notepad++.exe,0", key: "Notepad++");
    public static readonly UninstallEntry VsCode = E("Microsoft Visual Studio Code (User)",
        "\"C:\\Users\\me\\AppData\\Local\\Programs\\Microsoft VS Code\\unins000.exe\"", UninstallHive.CurrentUser,
        loc: "C:\\Users\\me\\AppData\\Local\\Programs\\Microsoft VS Code\\",
        icon: "C:\\Users\\me\\AppData\\Local\\Programs\\Microsoft VS Code\\Code.exe", key: "{771FD6B0-FA20-440A-A002-3B3BAC16DC50}_is1");
    public static readonly UninstallEntry Git = E("Git", "\"C:\\Program Files\\Git\\unins000.exe\"", loc: "C:\\Program Files\\Git\\",
        icon: "C:\\Program Files\\Git\\git-cmd.exe", key: "Git_is1");
    public static readonly UninstallEntry PyLauncher = E("Python Launcher", "MsiExec.exe /I{A1B2C3D4-0000-4000-8000-000000000001}",
        key: "{A1B2C3D4-0000-4000-8000-000000000001}", msi: true);
    public static readonly UninstallEntry Python = E("Python 3.12.1 (64-bit)", "\"C:\\Users\\me\\AppData\\Local\\Package Cache\\{abc}\\python-3.12.1-amd64.exe\"  /uninstall",
        UninstallHive.CurrentUser, key: "{abc}");
    public static readonly UninstallEntry Steam = E("Steam", "\"C:\\Program Files (x86)\\Steam\\uninstall.exe\"", view: 32,
        loc: null, icon: "C:\\Program Files (x86)\\Steam\\steam.exe", key: "Steam");
    public static readonly UninstallEntry Discord = E("Discord", "\"C:\\Users\\me\\AppData\\Local\\Discord\\Update.exe\" --uninstall -s",
        UninstallHive.CurrentUser, icon: "C:\\Users\\me\\AppData\\Local\\Discord\\app.ico", key: "Discord");

    public static UninstallEntry[] All => new[] { SevenZip, Notepad, VsCode, Git, PyLauncher, Python, Steam, Discord };
}

public class MatcherTests
{
    private static UninstallMatcher M(params UninstallEntry[] extra) =>
        new(new FakeSource(Fx.All.Concat(extra)));

    [Fact]
    public void ExactCaseInsensitive()
    {
        var r = M().Match("git");
        Assert.Equal(MatchKind.Exact, r.Kind);
        Assert.Same(Fx.Git, r.Entry);
    }

    [Theory]
    [InlineData("7-Zip")]
    [InlineData("7-Zip 24.08")]
    [InlineData("7-zip 24.08 (x64)")]
    [InlineData("7 Zip")]
    public void VersionAndArchStrippedSevenZip(string start)
    {
        var r = M().Match(start);
        Assert.True(r.Kind is MatchKind.Exact or MatchKind.Single);
        Assert.Same(Fx.SevenZip, r.Entry);
    }

    [Fact]
    public void NotepadPlusPlusNotConfusedWithNotepad()
    {
        var r = M(Fx.E("Notepad", key: "np")).Match("Notepad++");
        Assert.Same(Fx.Notepad, r.Entry);
    }

    [Fact]
    public void PythonVersionIgnored()
    {
        var r = M().Match("Python 3.11.5 (64-bit)");
        Assert.Equal(MatchKind.Single, r.Kind);
        Assert.Same(Fx.Python, r.Entry);
    }

    [Fact]
    public void PythonVsLauncherByNormalizedName()
    {
        // "Python" alone is a word-prefix of both "Python" (version stripped) and "Python Launcher": exact normalized wins
        var r = M().Match("Python");
        Assert.Same(Fx.Python, r.Entry);
    }

    [Fact]
    public void VsCodeStartNameWithoutUserSuffix()
    {
        var r = M().Match("Visual Studio Code");
        // "visual studio code" is not a prefix of "microsoft visual studio code user": needs the shortcut
        Assert.Equal(MatchKind.None, r.Kind);
        var r2 = M().Match("Visual Studio Code", @"C:\Users\me\AppData\Local\Programs\Microsoft VS Code\Code.exe");
        Assert.Same(Fx.VsCode, r2.Entry);
    }

    [Fact]
    public void VsCodeNamePrefixMatch()
    {
        var r = M().Match("Microsoft Visual Studio Code");
        Assert.Equal(MatchKind.Single, r.Kind);
        Assert.Same(Fx.VsCode, r.Entry);
    }

    [Fact]
    public void TrademarkSymbolsAndPunctuation()
    {
        var e = Fx.E("Acme\u2122 Studio\u00AE 2024 - Pro");
        var m = new UninstallMatcher(new[] { e });
        Assert.Same(e, m.Match("Acme Studio 2024 Pro").Entry);
        Assert.Same(e, m.Match("ACME STUDIO  2024   PRO").Entry);
    }

    [Fact]
    public void NumbersThatAreNotVersionsArePreserved()
    {
        var a = Fx.E("Visual Studio 2019", key: "a");
        var b = Fx.E("Visual Studio 2022", key: "b");
        var m = new UninstallMatcher(new[] { a, b });
        Assert.Same(b, m.Match("Visual Studio 2022").Entry);
    }

    [Fact]
    public void X86Stripped()
    {
        var e = Fx.E("Foo Bar (x86)");
        Assert.Same(e, new UninstallMatcher(new[] { e }).Match("Foo Bar").Entry);
    }

    [Fact]
    public void ShorterDisplayNameIsPrefixOfStartName()
    {
        // entry "Steam" vs start tile "Steam Client Bootstrapper"
        var r = M().Match("Steam Client");
        Assert.Same(Fx.Steam, r.Entry);
    }

    [Fact]
    public void PrefixNeedsWordBoundary()
    {
        var m = new UninstallMatcher(new[] { Fx.E("Gitkraken") });
        Assert.Equal(MatchKind.None, m.Match("Git Extras").Kind);
        Assert.Equal(MatchKind.None, m.Match("Gitk").Kind);
    }

    [Fact]
    public void VeryShortPrefixesDoNotMatch()
    {
        var m = new UninstallMatcher(new[] { Fx.E("Go Tools Suite") });
        Assert.Equal(MatchKind.None, m.Match("Go").Kind);
    }

    [Fact]
    public void AmbiguousPrefix()
    {
        var a = Fx.E("Adobe Reader", key: "a");
        var b = Fx.E("Adobe Acrobat", key: "b");
        var r = new UninstallMatcher(new[] { a, b }).Match("Adobe");
        Assert.Equal(MatchKind.Ambiguous, r.Kind);
        Assert.Null(r.Entry);
        Assert.Equal(2, r.Candidates.Count);
    }

    [Fact]
    public void AmbiguousResolvedByShortcut()
    {
        var a = Fx.E("Adobe Reader", loc: @"C:\Program Files\Adobe\Reader", key: "a");
        var b = Fx.E("Adobe Acrobat", loc: @"C:\Program Files\Adobe\Acrobat", key: "b");
        var r = new UninstallMatcher(new[] { a, b }).Match("Adobe", @"C:\Program Files\Adobe\Acrobat\acrobat.exe");
        Assert.Same(b, r.Entry);
    }

    [Fact]
    public void NoMatch()
    {
        var r = M().Match("Totally Unknown App");
        Assert.Equal(MatchKind.None, r.Kind);
        Assert.Null(r.Entry);
        Assert.Empty(r.Candidates);
        Assert.Equal(MatchKind.None, M().Match("  ").Kind);
    }

    [Fact]
    public void DedupesIdenticalAcrossViews()
    {
        var hklm = Fx.E("Same App", "\"C:\\a\\u.exe\"", UninstallHive.LocalMachine, 64);
        var wow = Fx.E("Same App", "\"C:\\a\\u.exe\"", UninstallHive.LocalMachine, 32);
        var r = new UninstallMatcher(new[] { wow, hklm }).Match("Same App");
        Assert.Equal(MatchKind.Exact, r.Kind);
        Assert.Same(hklm, r.Entry);
        Assert.Single(r.Candidates);
    }

    [Fact]
    public void TieBreakPrefersHklm64OverHklm32OverHkcu()
    {
        var u = Fx.E("Dup", "\"C:\\u\\1.exe\"", UninstallHive.CurrentUser, 64);
        var w = Fx.E("Dup", "\"C:\\u\\2.exe\"", UninstallHive.LocalMachine, 32);
        var h = Fx.E("Dup", "\"C:\\u\\3.exe\"", UninstallHive.LocalMachine, 64);
        Assert.Same(h, new UninstallMatcher(new[] { u, w, h }).Match("dup").Entry);
        Assert.Same(w, new UninstallMatcher(new[] { u, w }).Match("dup").Entry);
        var r = new UninstallMatcher(new[] { u, w, h }).Match("Dup");
        Assert.Equal(MatchKind.Exact, r.Kind);
        Assert.Equal(3, r.Candidates.Count);
    }

    [Fact]
    public void TieBreakNotAppliedForDifferentNames()
    {
        var a = Fx.E("Foo Pro", "\"C:\\a.exe\"", UninstallHive.LocalMachine, 64);
        var b = Fx.E("Foo Lite", "\"C:\\b.exe\"", UninstallHive.CurrentUser, 64);
        Assert.Equal(MatchKind.Ambiguous, new UninstallMatcher(new[] { a, b }).Match("Foo").Kind);
    }

    [Fact]
    public void NormalizedTieBreakAcrossDifferentlyDecoratedNames()
    {
        var a = Fx.E("Foo 1.0", "\"C:\\a.exe\"", UninstallHive.CurrentUser, 64);
        var b = Fx.E("Foo 2.0", "\"C:\\b.exe\"", UninstallHive.LocalMachine, 64);
        // names differ textually => not "identical" => ambiguous
        Assert.Equal(MatchKind.Ambiguous, new UninstallMatcher(new[] { a, b }).Match("Foo").Kind);
    }

    // ---- shortcut-only matching ----

    [Fact]
    public void ShortcutInstallLocationParent()
    {
        var r = M().Match("Some Renamed Tile", @"C:\Program Files\Git\cmd\git-gui.exe");
        Assert.Equal(MatchKind.Single, r.Kind);
        Assert.Same(Fx.Git, r.Entry);
    }

    [Fact]
    public void ShortcutDisplayIconSameFile()
    {
        var r = M().Match("Zip Manager", @"C:\Program Files\7-Zip\7zFM.exe");
        Assert.Same(Fx.SevenZip, r.Entry);
    }

    [Fact]
    public void ShortcutDisplayIconWithIndexAndQuotes()
    {
        var e = Fx.E("Quoted Icon App", icon: "\"C:\\Tools\\Q App\\q.exe\",2");
        var r = new UninstallMatcher(new[] { e }).Match("Nope", @"c:\tools\q app\q.exe");
        Assert.Same(e, r.Entry);
    }

    [Fact]
    public void ShortcutSharesIconDirectory()
    {
        // Discord: icon is app.ico in the app dir, shortcut targets Update.exe in the same dir
        var r = M().Match("Chat Thing", @"C:\Users\me\AppData\Local\Discord\Update.exe");
        Assert.Same(Fx.Discord, r.Entry);
    }

    [Fact]
    public void ShortcutInstallLocationDeepestWins()
    {
        var outer = Fx.E("Suite", loc: @"C:\Apps\Suite", key: "o");
        var inner = Fx.E("Suite Editor", loc: @"C:\Apps\Suite\Editor", key: "i");
        var r = new UninstallMatcher(new[] { outer, inner }).Match("Unknown", @"C:\Apps\Suite\Editor\ed.exe");
        Assert.Same(inner, r.Entry);
    }

    [Fact]
    public void ShortcutBroadLocationsIgnored()
    {
        var bad1 = Fx.E("Bad1", loc: @"C:\", key: "b1");
        var bad2 = Fx.E("Bad2", loc: @"C:\Program Files", key: "b2");
        var bad3 = Fx.E("Bad3", icon: @"C:\Windows\Installer\{x}\icon.ico", key: "b3");
        var m = new UninstallMatcher(new[] { bad1, bad2, bad3 });
        Assert.Equal(MatchKind.None, m.Match("Q", @"C:\Program Files\Other\o.exe").Kind);
        Assert.Equal(MatchKind.None, m.Match("Q", @"C:\Windows\Installer\{x}\other.exe").Kind);
    }

    [Fact]
    public void ShortcutTwoEqualCandidatesIsAmbiguous()
    {
        var a = Fx.E("A", loc: @"C:\Shared\App", key: "a");
        var b = Fx.E("B", loc: @"C:\Shared\App", key: "b");
        var r = new UninstallMatcher(new[] { a, b }).Match("Z", @"C:\Shared\App\x.exe");
        Assert.Equal(MatchKind.Ambiguous, r.Kind);
    }

    [Fact]
    public void ShortcutIgnoredWhenNameMatchesUniquely()
    {
        var r = M().Match("Git", @"C:\Program Files\7-Zip\7zFM.exe");
        Assert.Same(Fx.Git, r.Entry);
    }

    [Fact]
    public void LocationPrefixIsNotParentDir()
    {
        var e = Fx.E("Tool", loc: @"C:\Apps\Tool", key: "t");
        Assert.Equal(MatchKind.None, new UninstallMatcher(new[] { e }).Match("Q", @"C:\Apps\ToolBox\x.exe").Kind);
    }

    // ---- filtering through the fake source ----

    [Fact]
    public void SourceFilterSkipsHiddenEntries()
    {
        var src = new FakeSource(new[]
        {
            Fx.E("Hidden", sys: true), Fx.E("Update KB1", parent: "Windows"), Fx.E(null), Fx.E("No Uninstall", uninstall: null),
            Fx.E("Blank", uninstall: "  "), Fx.E("Real"),
        });
        var list = src.GetEntries();
        Assert.Single(list);
        Assert.Equal("Real", list[0].DisplayName);
    }
}
