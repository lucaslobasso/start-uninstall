// Append-only decision log: %LOCALAPPDATA%\StartUninstall\log.txt (STARTUNINSTALL_DIR moves it), rotated at 1 MB.
namespace StartUninstall;

static class Log
{
    public static readonly string Dir = Environment.GetEnvironmentVariable("STARTUNINSTALL_DIR") is { Length: > 0 } d
        ? d
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StartUninstall");
    static readonly string File_ = Path.Combine(Dir, "log.txt");
    static readonly object Gate = new();

    /// <summary>STARTUNINSTALL_DEBUG=1 also logs every focus change in Start, Search and Settings.</summary>
    public static readonly bool Debug = Environment.GetEnvironmentVariable("STARTUNINSTALL_DEBUG") == "1";

    public static void Write(string line)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var fi = new FileInfo(File_);
                if (fi.Exists && fi.Length > 1024 * 1024) File.Move(File_, Path.Combine(Dir, "log.old.txt"), true);
                File.AppendAllText(File_, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
