using System.Text;
using System.Threading.Channels;
using Hex1b.Input;

namespace Hex1b.Terminal;

/// <summary>
/// Adapter that bridges Hex1bApp to the current IHex1bTerminal implementation.
/// This allows us to refactor Hex1bApp internals without changing the terminal.
/// </summary>
/// <remarks>
/// This is a transitional adapter for Phase 1 of the terminal architecture migration.
/// It wraps the legacy <see cref="IHex1bTerminal"/> interface and exposes the new
/// <see cref="IHex1bAppTerminalWorkloadAdapter"/> interface that Hex1bApp uses internally.
/// 
/// Once the new Hex1bTerminal is implemented, this class can be replaced with
/// an adapter that works with the new terminal architecture.
/// </remarks>
public class LegacyHex1bAppTerminalWorkloadAdapter : IHex1bAppTerminalWorkloadAdapter, IDisposable
{
    private readonly IHex1bTerminal _terminal;
    private readonly bool _ownsTerminal;
    private readonly bool _enableMouse;
    private bool _disposed;
    
    /// <summary>
    /// Creates a new legacy adapter wrapping the specified terminal.
    /// </summary>
    /// <param name="terminal">The legacy terminal to wrap.</param>
    /// <param name="ownsTerminal">Whether this adapter owns (and should dispose) the terminal.</param>
    /// <param name="enableMouse">Whether mouse support is enabled.</param>
    public LegacyHex1bAppTerminalWorkloadAdapter(
        IHex1bTerminal terminal,
        bool ownsTerminal = true,
        bool enableMouse = false)
    {
        _terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
        _ownsTerminal = ownsTerminal;
        _enableMouse = enableMouse;
    }
    
    // === IHex1bAppTerminalWorkloadAdapter (app-side) ===
    
    /// <inheritdoc />
    public void Write(string text)
    {
        _terminal.Write(text);
    }
    
    /// <inheritdoc />
    public void Write(ReadOnlySpan<byte> data)
    {
        _terminal.Write(Encoding.UTF8.GetString(data));
    }
    
    /// <inheritdoc />
    public void Flush()
    {
        // Current terminal doesn't have explicit flush
    }
    
    /// <inheritdoc />
    public ChannelReader<Hex1bEvent> InputEvents => _terminal.InputEvents;
    
    /// <inheritdoc />
    public int Width => _terminal.Width;
    
    /// <inheritdoc />
    public int Height => _terminal.Height;
    
    /// <inheritdoc />
    public TerminalCapabilities Capabilities => new()
    {
        SupportsMouse = _enableMouse,
        SupportsTrueColor = true,
        Supports256Colors = true,
        SupportsAlternateScreen = true,
        SupportsBracketedPaste = false
    };
    
    /// <inheritdoc />
    public void EnterTuiMode()
    {
        _terminal.EnterAlternateScreen();
    }
    
    /// <inheritdoc />
    public void ExitTuiMode()
    {
        _terminal.ExitAlternateScreen();
    }
    
    /// <inheritdoc />
    public void Clear()
    {
        _terminal.Clear();
    }
    
    /// <inheritdoc />
    public void SetCursorPosition(int left, int top)
    {
        _terminal.SetCursorPosition(left, top);
    }
    
    // === IHex1bTerminalWorkloadAdapter (terminal-side) ===
    // These are no-ops for the legacy adapter - input/output flows through the terminal directly
    
    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
        // Not used in legacy mode - output goes directly to terminal
        return new ValueTask<ReadOnlyMemory<byte>>(ReadOnlyMemory<byte>.Empty);
    }
    
    /// <inheritdoc />
    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        // Not used in legacy mode - input comes through InputEvents channel
        return ValueTask.CompletedTask;
    }
    
    /// <inheritdoc />
    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        // Not used in legacy mode - resize events flow through InputEvents
        return ValueTask.CompletedTask;
    }
    
    /// <inheritdoc />
    public event Action? Disconnected;
    
    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        Disconnected?.Invoke();
        
        if (_ownsTerminal && _terminal is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
    
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        
        Disconnected?.Invoke();
        
        if (_ownsTerminal)
        {
            if (_terminal is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (_terminal is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
