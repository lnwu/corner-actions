namespace CornerActions;

/// <summary>
/// Keeps a second instance from running. Per-session (<c>Local\</c>), so different
/// signed-in users each get their own tray icon.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\CornerActions.SingleInstance";

    private readonly Mutex _mutex;

    internal SingleInstance()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsFirst = createdNew;
    }

    internal bool IsFirst { get; }

    public void Dispose()
    {
        if (IsFirst)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The mutex was already released.
            }
        }

        _mutex.Dispose();
    }
}
