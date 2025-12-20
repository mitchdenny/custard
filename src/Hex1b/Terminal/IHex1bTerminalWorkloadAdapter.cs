namespace Hex1b.Terminal;

/// <summary>
/// Terminal-side interface: What the Hex1bTerminal will need from any workload.
/// Raw byte streams for maximum flexibility.
/// </summary>
/// <remarks>
/// This interface represents the "terminal side" of the workload adapter,
/// designed for the future Hex1bTerminal to consume. It deals with raw bytes
/// and async streams.
/// </remarks>
public interface IHex1bTerminalWorkloadAdapter : IAsyncDisposable
{
    /// <summary>
    /// Read output FROM the workload (ANSI sequences).
    /// The terminal calls this to get data to parse and display.
    /// </summary>
    ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Write input TO the workload (encoded key/mouse events).
    /// The terminal calls this when it receives input from presentation.
    /// </summary>
    ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    
    /// <summary>
    /// Notify workload of resize.
    /// </summary>
    ValueTask ResizeAsync(int width, int height, CancellationToken ct = default);
    
    /// <summary>
    /// Workload has disconnected/exited.
    /// </summary>
    event Action? Disconnected;
}
