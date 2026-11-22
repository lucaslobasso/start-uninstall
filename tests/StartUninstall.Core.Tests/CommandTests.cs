using System.Runtime.Versioning;
using StartUninstall;
using Xunit;
using Xunit.Abstractions;

namespace StartUninstall.Core.Tests;

public class CommandTests
{
    private static UninstallCommand B(string s, Func<string, bool>? exists = null, bool msi = false, string? quiet = null) =>
        UninstallCommand.Build(Fx.E("x", s, msi: msi, quiet: quiet), exists ?? (_ => false));

    [Fact]
    public void QuotedExeNoArgs()
    {
        var c = B("\"C:\\Program Files\\7-Zip\\Uninstall.exe\"");
        Assert.Equal(@"C:\Program Files\7-Zip\Uninstall.exe", c.FileName);
        Assert.Equal("", c.Arguments);
    }

    [Fact]
    public void QuotedExeWithArgs()
    {
        var c = B("\"C:\\Users\\me\\AppData\\Local\\Discord\\Update.exe\" --uninstall -s");
        Assert.Equal(@"C:\Users\me\AppData\Local\Discord\Update.exe", c.FileName);
        Assert.Equal("--uninstall -s", c.Arguments);
    }

    [Fact]
    public void UnquotedNoSpaces()
    {
        var c = B(@"C:\Tools\un.exe /S");
        Assert.Equal(@"C:\Tools\un.exe", c.FileName);
        Assert.Equal("/S", c.Arguments);
    }

    [Fact]
    public void UnquotedPathWithSpacesProbesExistingFile()
    {
        var real = @"C:\Program Files\My App\uninstall.exe";
        var c = B(@"C:\Program Files\My App\uninstall.exe /remove all", p => p == real);
        Assert.Equal(real, c.FileName);
        Assert.Equal("/remove all", c.Arguments);
    }

    [Fact]
    public void UnquotedProbingPicksLongestThatExists()
    {
        // a decoy "C:\Program Files\App.exe" must not win when only the longer path exists
        var real = @"C:\Program Files\App.exe Tools\u.exe";
        var c = B(@"C:\Program Files\App.exe Tools\u.exe -x", p => p == real);
        Assert.Equal(real, c.FileName);
        Assert.Equal("-x", c.Arguments);
    }

    [Fact]
    public void UnquotedNothingExistsFallsBackToFirstExe()
    {
        var c = B(@"C:\Program Files\Gone\unins000.exe /SILENT");
        Assert.Equal(@"C:\Program Files\Gone\unins000.exe", c.FileName);
        Assert.Equal("/SILENT", c.Arguments);
    }

    [Fact]
    public void UnquotedExeProbingIsCaseInsensitiveOnExtension()
    {
        var c = B(@"C:\A B\U.EXE /x");
        Assert.Equal(@"C:\A B\U.EXE", c.FileName);
        Assert.Equal("/x", c.Arguments);
    }

    [Fact]
    public void EntireCommandQuotedAsOneString()
    {
        var c = B("\"C:\\Tools\\un.exe /S\"", p => p == @"C:\Tools\un.exe");
        Assert.Equal(@"C:\Tools\un.exe", c.FileName);
        Assert.Equal("/S", c.Arguments);
    }

    [Fact]
    public void UnterminatedQuote()
    {
        var c = B("\"C:\\Tools\\un.exe /S");
        Assert.Equal(@"C:\Tools\un.exe", c.FileName);
        Assert.Equal("/S", c.Arguments);
    }

    [Fact]
    public void RunDll32()
    {
        var c = B(@"rundll32.exe advpack.dll,LaunchINFSection C:\Windows\INF\app.inf,Uninstall");
        Assert.Equal("rundll32.exe", c.FileName);
        Assert.Equal(@"advpack.dll,LaunchINFSection C:\Windows\INF\app.inf,Uninstall", c.Arguments);
    }

    [Fact]
    public void RunDll32FullPathQuotedArgs()
    {
        var c = B("C:\\Windows\\System32\\rundll32.exe \"C:\\Program Files\\X\\x.dll\",Uninstall /q");
        Assert.Equal(@"C:\Windows\System32\rundll32.exe", c.FileName);
        Assert.Equal("\"C:\\Program Files\\X\\x.dll\",Uninstall /q", c.Arguments);
    }

    [Fact]
    public void RunDll32NoExtension()
    {
        var c = B("rundll32 shell32.dll,Control_RunDLL appwiz.cpl");
        Assert.Equal("rundll32", c.FileName);
        Assert.Equal("shell32.dll,Control_RunDLL appwiz.cpl", c.Arguments);
    }

    [Theory]
    [InlineData("MsiExec.exe /I{A1B2C3D4-0000-4000-8000-000000000001}", "/X{A1B2C3D4-0000-4000-8000-000000000001}")]
    [InlineData("MsiExec.exe /X{A1B2C3D4-0000-4000-8000-000000000001}", "/X{A1B2C3D4-0000-4000-8000-000000000001}")]
    [InlineData("msiexec /i {A1B2C3D4-0000-4000-8000-000000000001}", "/X {A1B2C3D4-0000-4000-8000-000000000001}")]
    [InlineData("MsiExec.exe /i{A1B2C3D4-0000-4000-8000-000000000001} /qb", "/X{A1B2C3D4-0000-4000-8000-000000000001} /qb")]
    [InlineData("\"C:\\Windows\\System32\\msiexec.exe\" /I{A1B2C3D4-0000-4000-8000-000000000001}", "/X{A1B2C3D4-0000-4000-8000-000000000001}")]
    public void MsiInstallBecomesUninstall(string input, string expectedArgs)
    {
        var c = B(input, msi: true);
        Assert.Contains("msiexec", c.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expectedArgs, c.Arguments);
    }

    [Fact]
    public void MsiPackageNameWithIOptionIsLeftAlone()
    {
        var c = B("MsiExec.exe /X{A1B2C3D4-0000-4000-8000-000000000001} /norestart");
        Assert.Equal("/X{A1B2C3D4-0000-4000-8000-000000000001} /norestart", c.Arguments);
    }

    [Fact]
    public void NonMsiExeIsNotRewritten()
    {
        var c = B(@"C:\a\setup.exe /I{A1B2C3D4-0000-4000-8000-000000000001}");
        Assert.Equal("/I{A1B2C3D4-0000-4000-8000-000000000001}", c.Arguments);
    }

    [Fact]
    public void NeverUsesQuietUninstallString()
    {
        var c = B("\"C:\\a\\un.exe\"", quiet: "\"C:\\a\\un.exe\" /VERYSILENT");
        Assert.Equal("", c.Arguments);
    }

    [Fact]
    public void ExpandsEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable("SU_TEST_DIR", @"C:\Expanded");
        var c = B("\"%SU_TEST_DIR%\\un.exe\" /S");
        Assert.Equal(@"C:\Expanded\un.exe", c.FileName);
    }

    [Fact]
    public void BatchFile()
    {
        var c = B(@"C:\a\remove.cmd all");
        Assert.Equal(@"C:\a\remove.cmd", c.FileName);
        Assert.Equal("all", c.Arguments);
    }

    [Fact]
    public void CmdCWithoutExtensionPath()
    {
        var c = B(@"cmd /c ""C:\a\remove.bat""");
        Assert.Equal("cmd", c.FileName);
        Assert.Equal(@"/c ""C:\a\remove.bat""", c.Arguments);
    }

    [Fact]
    public void MsiFileNameTokenUnquoted()
    {
        var c = B(@"C:\a\app.msi");
        Assert.Equal(@"C:\a\app.msi", c.FileName);
    }

    [Fact]
    public void EmptyThrows()
    {
        Assert.Throws<ArgumentException>(() => UninstallCommand.Build(Fx.E("x", uninstall: " ")));
    }

    [Fact]
    public void AllSampleEntriesBuild()
    {
        foreach (var e in Fx.All)
        {
            var c = UninstallCommand.Build(e, _ => false);
            Assert.False(string.IsNullOrEmpty(c.FileName));
        }
        var py = UninstallCommand.Build(Fx.PyLauncher, _ => false);
        Assert.Equal("MsiExec.exe", py.FileName);
        Assert.Equal("/X{A1B2C3D4-0000-4000-8000-000000000001}", py.Arguments);
    }
}

public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("START_UNINSTALL_LIVE") != "1")
            Skip = "Set START_UNINSTALL_LIVE=1 to run against the real registry.";
    }
}

[SupportedOSPlatform("windows")]
public class LiveRegistryTests(ITestOutputHelper output)
{
    [LiveFact]
    public void RealRegistryEntriesAllBuildCommands()
    {
        var entries = new RegistryUninstallSource().GetEntries();
        Assert.True(entries.Count > 0);
        int threw = 0, missing = 0;
        foreach (var e in entries)
        {
            try
            {
                var c = UninstallCommand.Build(e);
                var onPath = File.Exists(c.FileName)
                    || (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';').Any(d => d.Length > 0 && File.Exists(Path.Combine(d, c.FileName)))
                    || File.Exists(Path.Combine(Environment.SystemDirectory, c.FileName))
                    || File.Exists(Path.Combine(Environment.SystemDirectory, c.FileName + ".exe"));
                if (!onPath) { missing++; output.WriteLine($"UNRESOLVED: {e.DisplayName} => [{c.FileName}] [{c.Arguments}] from {e.UninstallString}"); }
            }
            catch (Exception ex)
            {
                threw++;
                output.WriteLine($"THREW: {e.DisplayName}: {ex.Message}");
            }
        }
        output.WriteLine($"LIVE STATS: total={entries.Count} threw={threw} fileNotFound={missing}");
        Assert.Equal(0, threw);
    }
}
