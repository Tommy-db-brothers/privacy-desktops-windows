using System.Text.Json;
using DeskVeil.Diagnostics;

namespace DeskVeil.Config;

/// <summary>Stores <see cref="DeskVeilConfig"/> as JSON, by default in %APPDATA%\DeskVeil\config.json.</summary>
public sealed class ConfigStore : IConfigStore
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly TimeProvider _time;

    private ConfigStore(string path, TimeProvider time)
    {
        FilePath = path;
        _time = time;
        Config = new DeskVeilConfig();
    }

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskVeil", "config.json");

    public string FilePath { get; }

    public DeskVeilConfig Config { get; private set; }

    /// <summary>Set when the last load found an unreadable file and moved it aside.</summary>
    public string? LastBackupPath { get; private set; }

    /// <summary>
    /// Loads the configuration. A missing file yields defaults. A corrupt file is moved to
    /// <c>config.json.corrupt-&lt;timestamp&gt;.bak</c> and replaced by an empty configuration.
    /// </summary>
    public static ConfigStore Load(string? path = null, TimeProvider? time = null)
    {
        var store = new ConfigStore(path ?? DefaultPath, time ?? TimeProvider.System);
        store.Reload();
        return store;
    }

    public void Reload()
    {
        LastBackupPath = null;
        if (!File.Exists(FilePath))
        {
            Config = new DeskVeilConfig();
            return;
        }

        try
        {
            using var stream = File.OpenRead(FilePath);
            var loaded = JsonSerializer.Deserialize<DeskVeilConfig>(stream, s_jsonOptions)
                ?? throw new JsonException("The configuration file contains null.");
            loaded.Desktops ??= [];
            Config = loaded;
        }
        catch (JsonException ex)
        {
            Log.Warn($"Config file '{FilePath}' is not valid: {ex.Message}");
            LastBackupPath = MoveAside();
            Config = new DeskVeilConfig();
        }
    }

    /// <summary>Writes atomically: serialize to a temporary file, flush it to disk, then replace the original.</summary>
    public void Save()
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(FilePath))!;
        Directory.CreateDirectory(directory);

        var tempPath = FilePath + ".tmp";
        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, Config, s_jsonOptions);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(FilePath))
        {
            File.Replace(tempPath, FilePath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, FilePath);
        }
    }

    private string MoveAside()
    {
        var stamp = _time.GetLocalNow().ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var backup = $"{FilePath}.corrupt-{stamp}.bak";
        for (var n = 2; File.Exists(backup); n++)
        {
            backup = $"{FilePath}.corrupt-{stamp}-{n}.bak";
        }
        File.Move(FilePath, backup);
        Log.Warn($"Corrupt config moved to '{backup}'; starting with an empty configuration.");
        return backup;
    }
}
