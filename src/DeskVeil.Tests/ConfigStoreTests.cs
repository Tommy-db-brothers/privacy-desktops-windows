using System.Text.Json;
using DeskVeil.Config;
using DeskVeil.Desktops;

namespace DeskVeil.Tests;

public sealed class ConfigStoreTests : IDisposable
{
    private static readonly Guid s_work = Guid.Parse("8b6c0e3a-1d2f-4e5a-9b7c-0123456789ab");
    private static readonly Guid s_private = Guid.Parse("f00dbabe-0000-4000-8000-00000000cafe");

    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Missing_file_yields_defaults()
    {
        var store = ConfigStore.Load(_dir.File("config.json"));

        Assert.Empty(store.Config.Desktops);
        Assert.True(store.Config.RelockOnLeave);
        Assert.Equal(DeskVeilConfig.CurrentVersion, store.Config.Version);
        Assert.Null(store.LastBackupPath);
    }

    [Fact]
    public void Save_then_load_round_trips()
    {
        var path = _dir.File("config.json");
        var store = ConfigStore.Load(path);
        store.Config.RelockOnLeave = false;
        store.Config.SetLocked(s_work, locked: true, name: "Werk");
        store.Config.SetLocked(s_private, locked: false, name: "Privé");
        store.Save();

        var reloaded = ConfigStore.Load(path).Config;

        Assert.False(reloaded.RelockOnLeave);
        Assert.Equal(2, reloaded.Desktops.Count);
        Assert.True(reloaded.IsLocked(s_work));
        Assert.False(reloaded.IsLocked(s_private));
        Assert.Equal("Werk", reloaded.Desktops[s_work].LastKnownName);
        Assert.Equal("Privé", reloaded.Desktops[s_private].LastKnownName);
    }

    [Fact]
    public void Save_creates_missing_directory_overwrites_and_leaves_no_temp_file()
    {
        var path = Path.Combine(_dir.Path, "nested", "config.json");
        var store = ConfigStore.Load(path);
        store.Config.SetLocked(s_work, true);
        store.Save();
        store.Config.SetLocked(s_work, false);
        store.Save();

        Assert.False(ConfigStore.Load(path).Config.IsLocked(s_work));
        Assert.Equal(["config.json"], Directory.GetFiles(Path.GetDirectoryName(path)!).Select(Path.GetFileName));
    }

    [Fact]
    public void Json_is_keyed_by_desktop_guid()
    {
        var path = _dir.File("config.json");
        var store = ConfigStore.Load(path);
        store.Config.SetLocked(s_work, true, "Werk");
        store.Save();

        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var keys = json.RootElement.GetProperty("desktops").EnumerateObject().Select(p => p.Name).ToList();

        var key = Assert.Single(keys);
        Assert.Equal(s_work, Guid.Parse(key));
    }

    [Fact]
    public void Lock_follows_the_guid_when_desktops_are_reordered_and_renamed()
    {
        var config = new DeskVeilConfig();
        config.Reconcile([new DesktopInfo(s_work, 0, "Werk"), new DesktopInfo(s_private, 1, "Privé")]);
        config.SetLocked(s_private, true);

        // The user moves "Privé" to the front and renames both desktops.
        var changed = config.Reconcile([new DesktopInfo(s_private, 0, "Thuis"), new DesktopInfo(s_work, 1, "Kantoor")]);

        Assert.True(changed);
        Assert.True(config.IsLocked(s_private));
        Assert.False(config.IsLocked(s_work));
        Assert.Equal("Thuis", config.Desktops[s_private].LastKnownName);
    }

    [Fact]
    public void Reconcile_removes_deleted_and_adds_new_desktops()
    {
        var config = new DeskVeilConfig();
        config.SetLocked(s_work, true, "Werk");
        var added = Guid.NewGuid();

        var changed = config.Reconcile([new DesktopInfo(s_private, 0, "Privé"), new DesktopInfo(added, 1, "Bureaublad 2")]);

        Assert.True(changed);
        Assert.False(config.Desktops.ContainsKey(s_work));
        Assert.False(config.IsLocked(s_private));
        Assert.False(config.IsLocked(added));
        Assert.Equal(2, config.Desktops.Count);
        Assert.False(config.Reconcile([new DesktopInfo(s_private, 0, "Privé"), new DesktopInfo(added, 1, "Bureaublad 2")]));
    }

    [Fact]
    public void Reconcile_with_an_empty_list_keeps_existing_locks()
    {
        var config = new DeskVeilConfig();
        config.SetLocked(s_work, true);

        Assert.False(config.Reconcile([]));
        Assert.True(config.IsLocked(s_work));
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("null")]
    [InlineData("{ \"desktops\": { \"not-a-guid\": { \"locked\": true } } }")]
    public void Corrupt_json_is_backed_up_and_replaced_by_an_empty_config(string content)
    {
        var path = _dir.File("config.json");
        File.WriteAllText(path, content);
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 27, 21, 30, 0, TimeSpan.Zero));

        var store = ConfigStore.Load(path, clock);

        Assert.Empty(store.Config.Desktops);
        Assert.True(store.Config.RelockOnLeave);
        Assert.NotNull(store.LastBackupPath);
        Assert.Equal(path + ".corrupt-20261027-213000.bak", store.LastBackupPath);
        Assert.Equal(content, File.ReadAllText(store.LastBackupPath!));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void A_second_corrupt_file_in_the_same_second_gets_its_own_backup()
    {
        var path = _dir.File("config.json");
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 27, 21, 30, 0, TimeSpan.Zero));
        File.WriteAllText(path, "{");
        var first = ConfigStore.Load(path, clock).LastBackupPath;
        File.WriteAllText(path, "[");
        var second = ConfigStore.Load(path, clock).LastBackupPath;

        Assert.NotEqual(first, second);
        Assert.Equal("{", File.ReadAllText(first!));
        Assert.Equal("[", File.ReadAllText(second!));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
