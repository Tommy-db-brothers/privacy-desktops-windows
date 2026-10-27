using DeskVeil.Diagnostics;

namespace DeskVeil.Tests;

public sealed class SettingsAuditReportTests
{
    [Fact]
    public void Absent_values_mean_the_windows_default_which_does_not_leak()
    {
        var report = SettingsAuditReport.FromRegistryValues(null, null);

        Assert.False(report.Leaks);
    }

    [Fact]
    public void Value_one_means_only_the_current_desktop()
    {
        Assert.False(SettingsAuditReport.FromRegistryValues(1, 1).Leaks);
    }

    [Theory]
    [InlineData(0, 1, true, false)]
    [InlineData(1, 0, false, true)]
    [InlineData(0, 0, true, true)]
    public void Value_zero_shows_all_desktops_and_leaks(int taskbar, int altTab, bool taskbarLeaks, bool altTabLeaks)
    {
        var report = SettingsAuditReport.FromRegistryValues(taskbar, altTab);

        Assert.True(report.Leaks);
        Assert.Equal(taskbarLeaks, report.TaskbarShowsAllDesktops);
        Assert.Equal(altTabLeaks, report.AltTabShowsAllDesktops);
        Assert.Contains("Let op", report.Describe());
    }

    [Fact]
    public void Unexpected_value_types_are_not_reported_as_leaks()
    {
        Assert.False(SettingsAuditReport.FromRegistryValues("0", new byte[] { 0 }).Leaks);
    }
}
