using System.Text;

namespace DeskVeil.Diagnostics;

/// <summary>
/// Appends lines to <c>deskveil.log</c>. Before a line would push the file past <c>maxBytes</c>, the file is
/// rotated: deskveil.log → deskveil.1.log → … → deskveil.(maxFiles-1).log, and the oldest is dropped, so at
/// most <c>maxFiles</c> files exist. Failures to write are swallowed: logging must never take the app down.
/// </summary>
internal sealed class RotatingFileLog : IDisposable
{
    public const string FileName = "deskveil.log";

    private static readonly Encoding s_encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly object _gate = new();
    private readonly long _maxBytes;
    private readonly int _maxFiles;
    private FileStream? _stream;

    public RotatingFileLog(string directory, long maxBytes = 1024 * 1024, int maxFiles = 5)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFiles, 1);
        Directory = directory;
        _maxBytes = maxBytes;
        _maxFiles = maxFiles;
    }

    public string Directory { get; }

    public string CurrentPath => Path.Combine(Directory, FileName);

    public void Write(string line)
    {
        var bytes = s_encoding.GetBytes(line + Environment.NewLine);
        lock (_gate)
        {
            try
            {
                _stream ??= Open();
                if (_stream.Length > 0 && _stream.Length + bytes.Length > _maxBytes)
                {
                    _stream.Dispose();
                    _stream = null;
                    Rotate();
                    _stream = Open();
                }
                _stream.Write(bytes);
                _stream.Flush();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _stream?.Dispose();
                _stream = null;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _stream?.Dispose();
            _stream = null;
        }
    }

    private FileStream Open()
    {
        System.IO.Directory.CreateDirectory(Directory);
        return new FileStream(CurrentPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
    }

    private void Rotate()
    {
        if (_maxFiles == 1)
        {
            File.Delete(CurrentPath);
            return;
        }

        File.Delete(ArchivePath(_maxFiles - 1));
        for (var n = _maxFiles - 2; n >= 1; n--)
        {
            if (File.Exists(ArchivePath(n)))
            {
                File.Move(ArchivePath(n), ArchivePath(n + 1));
            }
        }
        File.Move(CurrentPath, ArchivePath(1));
    }

    private string ArchivePath(int number) => Path.Combine(Directory, $"deskveil.{number}.log");
}
