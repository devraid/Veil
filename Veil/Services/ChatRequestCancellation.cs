namespace Veil.Services;

public sealed class ChatRequestCancellation : IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _active;

    public CancellationToken StartNext()
    {
        lock (_gate)
        {
            _active?.Cancel();
            _active?.Dispose();
            _active = new CancellationTokenSource();
            return _active.Token;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _active?.Cancel();
            _active?.Dispose();
            _active = null;
        }
    }
}
