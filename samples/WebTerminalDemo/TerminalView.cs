namespace WebTerminalDemo;

internal sealed class TerminalView(TerminalInstance instance, Action onClosed) : IDisposable
{
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposed;

    public TerminalInstance Instance { get; } = instance;
    public Task Completion => _completion.Task;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        Instance.CloseView(this);
        onClosed();
        _completion.TrySetResult();
    }
}
