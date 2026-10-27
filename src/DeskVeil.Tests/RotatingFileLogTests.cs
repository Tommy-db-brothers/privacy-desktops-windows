using DeskVeil.Diagnostics;

namespace DeskVeil.Tests;

public sealed class RotatingFileLogTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Writes_lines_to_deskveil_log()
    {
        using (var log = new RotatingFileLog(_dir.Path))
        {
            log.Write("eerste");
            log.Write("tweede");
        }

        Assert.Equal(["eerste", "tweede"], File.ReadAllLines(_dir.File(RotatingFileLog.FileName)));
    }

    [Fact]
    public void Creates_the_directory()
    {
        var nested = Path.Combine(_dir.Path, "a", "logs");
        using var log = new RotatingFileLog(nested);

        log.Write("regel");

        Assert.True(File.Exists(Path.Combine(nested, RotatingFileLog.FileName)));
    }

    [Fact]
    public void Rotates_before_exceeding_the_size_and_keeps_at_most_max_files()
    {
        using (var log = new RotatingFileLog(_dir.Path, maxBytes: 100, maxFiles: 3))
        {
            for (var i = 0; i < 50; i++)
            {
                log.Write($"regel {i:D3} ......................");
            }
        }

        var files = Directory.GetFiles(_dir.Path).Select(Path.GetFileName).Order().ToList();
        Assert.Equal(["deskveil.1.log", "deskveil.2.log", "deskveil.log"], files);
        Assert.All(Directory.GetFiles(_dir.Path), f => Assert.True(new FileInfo(f).Length <= 100));

        // Newest lines are in deskveil.log, older ones in the numbered archives, without gaps.
        var lines = new[] { "deskveil.2.log", "deskveil.1.log", "deskveil.log" }
            .SelectMany(f => File.ReadAllLines(_dir.File(f)))
            .Select(l => int.Parse(l.Substring(6, 3)))
            .ToList();
        Assert.Equal(49, lines[^1]);
        Assert.Equal(Enumerable.Range(lines[0], lines.Count), lines);
    }

    [Fact]
    public void A_single_line_larger_than_the_limit_is_still_written()
    {
        using (var log = new RotatingFileLog(_dir.Path, maxBytes: 10, maxFiles: 2))
        {
            log.Write(new string('x', 50));
            log.Write(new string('y', 50));
        }

        Assert.StartsWith("y", File.ReadAllText(_dir.File("deskveil.log")));
        Assert.StartsWith("x", File.ReadAllText(_dir.File("deskveil.1.log")));
    }

    [Fact]
    public void With_one_file_the_log_is_truncated_on_rotation()
    {
        using (var log = new RotatingFileLog(_dir.Path, maxBytes: 20, maxFiles: 1))
        {
            log.Write("aaaaaaaaaaaaaaa");
            log.Write("bbbbbbbbbbbbbbb");
        }

        Assert.Equal(["bbbbbbbbbbbbbbb"], File.ReadAllLines(_dir.File("deskveil.log")));
        Assert.Single(Directory.GetFiles(_dir.Path));
    }
}
