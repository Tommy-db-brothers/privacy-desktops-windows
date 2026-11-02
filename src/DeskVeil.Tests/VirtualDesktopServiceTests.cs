using DeskVeil.Desktops;

namespace DeskVeil.Tests;

public sealed class VirtualDesktopServiceTests
{
    private readonly FakeApi _api = new();
    private readonly FakeListener _listener = new();
    private readonly VirtualDesktopService _service;

    public VirtualDesktopServiceTests()
    {
        _service = new VirtualDesktopService(_api, _ => _listener);
    }

    [Fact]
    public void Start_registers_the_listener_window_for_the_switch_message()
    {
        _service.Start();

        Assert.Equal([$"register {FakeListener.Hwnd} {VirtualDesktopService.DesktopSwitchedMessage}"], _api.Calls);
    }

    [Fact]
    public void Start_throws_and_disposes_the_listener_when_registration_fails()
    {
        _api.RegisterResult = -1;

        Assert.Throws<InvalidOperationException>(_service.Start);
        Assert.True(_listener.Disposed);
    }

    [Fact]
    public void Reconnect_unregisters_and_registers_the_same_window_again()
    {
        _service.Start();
        _api.Calls.Clear();

        _service.Reconnect();

        Assert.Equal(
            [$"unregister {FakeListener.Hwnd}", $"register {FakeListener.Hwnd} {VirtualDesktopService.DesktopSwitchedMessage}"],
            _api.Calls);
    }

    [Fact]
    public void Reconnect_before_start_starts()
    {
        _service.Reconnect();

        Assert.Single(_api.Calls, c => c.StartsWith("register", StringComparison.Ordinal));
    }

    [Fact]
    public void Reconnect_throws_when_registering_again_fails()
    {
        _service.Start();
        _api.RegisterResult = -1;

        Assert.Throws<InvalidOperationException>(_service.Reconnect);
    }

    [Fact]
    public void After_reconnect_switches_report_the_desktop_read_back_at_reconnect_as_previous()
    {
        _service.Start();
        _api.Current = 2;
        _service.Reconnect();
        DesktopChangedEventArgs? args = null;
        _service.CurrentDesktopChanged += (_, e) => args = e;

        _api.Current = 0;
        _listener.Raise(2, 0);

        Assert.Equal(_api.Ids[2], args!.PreviousId);
        Assert.Equal(_api.Ids[0], args.CurrentId);
    }

    [Fact]
    public void Switch_messages_report_guids_read_back_instead_of_the_posted_numbers()
    {
        _service.Start();
        DesktopChangedEventArgs? args = null;
        _service.CurrentDesktopChanged += (_, e) => args = e;

        _api.Current = 1;
        _listener.Raise(7, 9);

        Assert.Equal(_api.Ids[0], args!.PreviousId);
        Assert.Equal(_api.Ids[1], args.CurrentId);
    }

    [Fact]
    public void Desktops_get_a_default_name_when_unnamed()
    {
        _api.Names[1] = "Werk";

        var desktops = _service.GetDesktops();

        Assert.Equal(["Bureaublad 1", "Werk", "Bureaublad 3"], desktops.Select(d => d.Name));
        Assert.Equal(_api.Ids, desktops.Select(d => d.Id));
        Assert.Equal([0, 1, 2], desktops.Select(d => d.Index));
    }

    [Fact]
    public void A_desktop_vanishing_during_enumeration_yields_an_empty_list()
    {
        _api.Ids[2] = Guid.Empty;

        Assert.Empty(_service.GetDesktops());
    }

    [Fact]
    public void SwitchTo_goes_to_the_current_index_of_the_guid()
    {
        Assert.True(_service.SwitchTo(_api.Ids[2]));
        Assert.False(_service.SwitchTo(Guid.NewGuid()));
        Assert.Equal(["goto 2"], _api.Calls);
    }

    [Fact]
    public void Dispose_unregisters_and_disposes_the_listener()
    {
        _service.Start();
        _service.Dispose();

        Assert.Equal($"unregister {FakeListener.Hwnd}", _api.Calls[^1]);
        Assert.True(_listener.Disposed);
    }

    [Fact]
    public void Unsupported_builds_are_refused_without_touching_the_dll()
    {
        var called = false;

        var message = VirtualDesktopService.CheckAvailability(new WindowsBuild(22631, 4460, "23H2"), () =>
        {
            called = true;
            return 3;
        });

        Assert.False(called);
        Assert.Contains("23H2 (build 22631.4460)", message);
    }

    [Fact]
    public void A_missing_dll_is_reported_with_the_build()
    {
        var message = VirtualDesktopService.CheckAvailability(new WindowsBuild(26100, 4000, "24H2"), () => throw new DllNotFoundException());

        Assert.Contains("DllNotFoundException", message);
        Assert.Contains("26100.4000", message);
    }

    [Fact]
    public void A_working_dll_on_a_supported_build_is_available()
    {
        Assert.Null(VirtualDesktopService.CheckAvailability(new WindowsBuild(26200, 6899, "25H2"), () => 2));
        Assert.NotNull(VirtualDesktopService.CheckAvailability(new WindowsBuild(26200, 6899, "25H2"), () => -1));
    }

    [Theory]
    [InlineData(22631, 9999, false)]
    [InlineData(26100, 2604, false)]
    [InlineData(26100, 2605, true)]
    [InlineData(26200, 0, true)]
    public void Minimum_build_is_24H2_with_revision_2605(int build, int revision, bool supported)
    {
        Assert.Equal(supported, new WindowsBuild(build, revision, null).IsSupported);
    }

    private sealed class FakeApi : IVirtualDesktopApi
    {
        public List<Guid> Ids { get; } = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

        public Dictionary<int, string> Names { get; } = [];

        public int Current { get; set; }

        public int RegisterResult { get; set; } = 1;

        public List<string> Calls { get; } = [];

        public int GetCurrentDesktopNumber() => Current;

        public int GetDesktopCount() => Ids.Count;

        public Guid GetDesktopIdByNumber(int number) => number >= 0 && number < Ids.Count ? Ids[number] : Guid.Empty;

        public int GoToDesktopNumber(int number)
        {
            Calls.Add($"goto {number}");
            return 1;
        }

        public string? GetDesktopName(int number) => Names.GetValueOrDefault(number, string.Empty);

        public int RegisterPostMessageHook(nint listenerHwnd, uint messageId)
        {
            Calls.Add($"register {listenerHwnd} {messageId}");
            return RegisterResult;
        }

        public void UnregisterPostMessageHook(nint listenerHwnd) => Calls.Add($"unregister {listenerHwnd}");
    }

    private sealed class FakeListener : IDesktopSwitchListener
    {
        public const nint Hwnd = 0x4242;

        public event EventHandler<(int OldNumber, int NewNumber)>? DesktopSwitched;

        public nint Handle => Hwnd;

        public bool Disposed { get; private set; }

        public void Raise(int oldNumber, int newNumber) => DesktopSwitched?.Invoke(this, (oldNumber, newNumber));

        public void Dispose() => Disposed = true;
    }
}
