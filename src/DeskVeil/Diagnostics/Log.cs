using System.Diagnostics;

namespace DeskVeil.Diagnostics;

/// <summary>
/// Writes to the debug output (OutputDebugString via the default trace listener, visible in a debugger
/// or Sysinternals DebugView) and, once <see cref="EnableFileLogging"/> ran, to a rotating log file in
/// %LOCALAPPDATA%\DeskVeil\logs (5 files of at most 1 MB).
/// </summary>
internal static class Log
{
    private static RotatingFileLog? s_file;

    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskVeil", "logs");

    public static string? FileDirectory => s_file?.Directory;

    public static void EnableFileLogging(string? directory = null)
    {
        var previous = Interlocked.Exchange(ref s_file, new RotatingFileLog(directory ?? DefaultDirectory));
        previous?.Dispose();
    }

    public static void DisableFileLogging() => Interlocked.Exchange(ref s_file, null)?.Dispose();

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}: {exception}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level,-5} {message}";
        Trace.WriteLine($"[DeskVeil] {line}");
        s_file?.Write(line);
    }
}
