namespace DeskVeil;

/// <summary>
/// Named mutex that marks the running instance. The installer and uninstaller check the same name
/// (<c>CheckForMutexes</c> in installer/DeskVeil.iss) to wait until DeskVeil has exited.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    public const string DefaultMutexName = @"Local\DeskVeil.SingleInstance";

    private readonly Mutex _mutex;

    public SingleInstance(string mutexName = DefaultMutexName)
    {
        _mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        IsFirstInstance = createdNew;
    }

    public bool IsFirstInstance { get; }

    public void Dispose()
    {
        if (IsFirstInstance)
        {
            _mutex.ReleaseMutex();
        }
        _mutex.Dispose();
    }
}
