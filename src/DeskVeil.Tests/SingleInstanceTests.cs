namespace DeskVeil.Tests;

public sealed class SingleInstanceTests
{
    private static string UniqueName() => $@"Local\DeskVeil.Tests.{Guid.NewGuid():N}";

    [Fact]
    public void The_first_instance_owns_the_mutex_and_a_second_one_does_not()
    {
        var name = UniqueName();
        using var first = new SingleInstance(name);
        using var second = new SingleInstance(name);

        Assert.True(first.IsFirstInstance);
        Assert.False(second.IsFirstInstance);
    }

    [Fact]
    public void After_the_first_instance_exits_a_new_start_is_first_again()
    {
        var name = UniqueName();
        new SingleInstance(name).Dispose();

        using var next = new SingleInstance(name);

        Assert.True(next.IsFirstInstance);
    }

    [Fact]
    public void A_second_instance_exiting_does_not_release_the_first()
    {
        var name = UniqueName();
        using var first = new SingleInstance(name);
        new SingleInstance(name).Dispose();

        using var third = new SingleInstance(name);

        Assert.False(third.IsFirstInstance);
    }

    [Fact]
    public void The_installer_and_the_app_agree_on_the_mutex_name()
    {
        // installer/DeskVeil.iss is copied next to the test assembly by DeskVeil.Tests.csproj.
        var script = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "DeskVeil.iss"));

        Assert.Contains($"'{SingleInstance.DefaultMutexName}'", script);
        Assert.Contains($"'{ControlWindow.Title}'", script);
    }
}
