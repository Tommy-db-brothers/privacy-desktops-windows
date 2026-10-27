using System.Diagnostics;

namespace DeskVeil.Diagnostics;

/// <summary>Writes to the debug output (OutputDebugString via the default trace listener);
/// view it with a debugger or Sysinternals DebugView.</summary>
internal static class Log
{
    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}: {exception}");

    private static void Write(string level, string message) =>
        Trace.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [DeskVeil] {level} {message}");
}
