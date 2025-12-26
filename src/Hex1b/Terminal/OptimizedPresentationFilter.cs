using System.Text;
using Hex1b.Theming;

namespace Hex1b.Terminal;

/// <summary>
/// A presentation filter that optimizes ANSI output by comparing terminal snapshots
/// and generating minimal updates for only the cells that changed.
/// </summary>
/// <remarks>
/// <para>
/// This filter dramatically reduces the amount of ANSI data sent to the presentation layer
/// by maintaining a snapshot of the last screen state and generating optimized sequences
/// that update only the cells that have actually changed.
/// </para>
/// <para>
/// Unlike re-parsing ANSI streams, this filter works directly with the terminal's screen buffer,
/// comparing before/after snapshots to detect changes and generating minimal ANSI sequences
/// to update only the changed cells. This trades CPU/memory for improved render performance.
/// </para>
/// <para>
/// During rapid resize operations, the filter automatically disables optimization to avoid
/// performance degradation from constant snapshot rebuilding. Optimization resumes after
/// the resize settles.
/// </para>
/// <para>
/// Benefits:
/// <list type="bullet">
///   <item>90%+ reduction in output for mostly static content</item>
///   <item>Reduced flicker and improved performance on high-latency connections</item>
///   <item>Minimal network traffic for remote terminals (WebSocket, SSH)</item>
///   <item>Better battery life on mobile devices</item>
/// </list>
/// </para>
/// </remarks>
public sealed class OptimizedPresentationFilter : IHex1bTerminalPresentationTransformFilter
{
    // Snapshot state - accessed only from TransformOutputAsync which is called sequentially
    private TerminalCell[,]? _lastSnapshot;
    private int _snapshotWidth;
    private int _snapshotHeight;
    
    // Resize debounce - use Interlocked for cross-thread visibility without locks
    private long _lastResizeTicks;
    private int _pendingWidth;
    private int _pendingHeight;
    
    /// <summary>
    /// Time to wait after a resize before resuming rendering (in milliseconds).
    /// During this window, output is suppressed entirely to avoid lag during rapid resize.
    /// </summary>
    private const int ResizeDebounceMs = 50;

    /// <inheritdoc />
    public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp)
    {
        _snapshotWidth = width;
        _snapshotHeight = height;
        _pendingWidth = width;
        _pendingHeight = height;
        _lastSnapshot = null; // Will be initialized on first write
        _lastResizeTicks = 0;
        
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnOutputAsync(ReadOnlyMemory<byte> data, TimeSpan elapsed)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnInputAsync(ReadOnlyMemory<byte> data, TimeSpan elapsed)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed)
    {
        // Just record the resize - don't do any work here
        // The snapshot will be rebuilt when we next need to optimize
        Interlocked.Exchange(ref _pendingWidth, width);
        Interlocked.Exchange(ref _pendingHeight, height);
        Interlocked.Exchange(ref _lastResizeTicks, DateTime.UtcNow.Ticks);
        
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnSessionEndAsync(TimeSpan elapsed)
    {
        _lastSnapshot = null;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> TransformOutputAsync(
        ReadOnlyMemory<byte> originalOutput,
        TerminalCell[,] screenBuffer,
        int width,
        int height,
        TimeSpan elapsed)
    {
        var now = DateTime.UtcNow.Ticks;
        var ticksSinceResize = now - Interlocked.Read(ref _lastResizeTicks);
        var msSinceResize = ticksSinceResize / TimeSpan.TicksPerMillisecond;
        
        // During resize debounce window, suppress ALL output
        // This prevents lag from sending every intermediate resize frame
        if (msSinceResize < ResizeDebounceMs)
        {
            // Invalidate snapshot so we do a full redraw after resize settles
            _lastSnapshot = null;
            return ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
        }
        
        // Throttle output rate to prevent overwhelming the terminal
        var ticksSinceLastOutput = now - _lastOutputTicks;
        var msSinceLastOutput = ticksSinceLastOutput / TimeSpan.TicksPerMillisecond;
        
        if (msSinceLastOutput < OutputThrottleMs && _lastSnapshot != null)
        {
            // Too soon since last output - skip this frame
            // (but only if we have a valid snapshot, otherwise we need to initialize)
            return ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
        }
        
        _lastOutputTicks = now;
        
        // Check if dimensions changed - if so, rebuild snapshot
        if (_lastSnapshot == null || 
            _snapshotWidth != width || 
            _snapshotHeight != height ||
            _lastSnapshot.GetLength(0) != height ||
            _lastSnapshot.GetLength(1) != width)
        {
            // First frame after resize or startup - pass through and capture snapshot
            CaptureSnapshot(screenBuffer, width, height);
            return ValueTask.FromResult(originalOutput);
        }

        // Compare current screen buffer with last snapshot to find changes
        var changes = FindChanges(screenBuffer, width, height);

        // If no changes, suppress output entirely
        if (changes.Count == 0)
        {
            return ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
        }

        // Generate optimized ANSI sequences for only the changed cells
        var optimizedOutput = GenerateOptimizedAnsi(changes);
        
        // Update our snapshot with changes only (not full copy)
        foreach (var (row, col, cell) in changes)
        {
            _lastSnapshot![row, col] = cell;
        }
        
        return ValueTask.FromResult(optimizedOutput);
    }

    private void CaptureSnapshot(TerminalCell[,] screenBuffer, int width, int height)
    {
        _snapshotWidth = width;
        _snapshotHeight = height;
        _lastSnapshot = new TerminalCell[height, width];
        
        // Use Buffer.BlockCopy for arrays of value types, but TerminalCell is a struct
        // with reference types, so we need to copy element by element
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                _lastSnapshot[y, x] = screenBuffer[y, x];
            }
        }
    }

    private List<(int Row, int Col, TerminalCell Cell)> FindChanges(
        TerminalCell[,] screenBuffer, 
        int width, 
        int height)
    {
        var changes = new List<(int Row, int Col, TerminalCell Cell)>();
        var snapshot = _lastSnapshot!;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var current = screenBuffer[y, x];
                var previous = snapshot[y, x];
                
                if (!CellsEqual(current, previous))
                {
                    changes.Add((y, x, current));
                }
            }
        }
        
        return changes;
    }

    private static bool CellsEqual(TerminalCell a, TerminalCell b)
    {
        // Fast path: check character first (most likely to differ)
        if (a.Character != b.Character) return false;
        if (a.Attributes != b.Attributes) return false;
        if (!Equals(a.Foreground, b.Foreground)) return false;
        if (!Equals(a.Background, b.Background)) return false;
        return true;
    }

    private static ReadOnlyMemory<byte> GenerateOptimizedAnsi(
        List<(int Row, int Col, TerminalCell Cell)> changes)
    {
        var sb = new StringBuilder();
        
        // Track current cursor position and SGR state to minimize escape sequences
        int currentRow = -1;
        int currentCol = -1;
        Hex1bColor? currentFg = null;
        Hex1bColor? currentBg = null;
        CellAttributes currentAttrs = CellAttributes.None;
        bool sgrInitialized = false;

        foreach (var (row, col, cell) in changes)
        {
            // Move cursor if needed
            if (currentRow != row || currentCol != col)
            {
                sb.Append($"\x1b[{row + 1};{col + 1}H");
                currentRow = row;
                currentCol = col;
            }

            // For the first cell, reset SGR state
            if (!sgrInitialized)
            {
                sb.Append("\x1b[0m");
                currentFg = null;
                currentBg = null;
                currentAttrs = CellAttributes.None;
                sgrInitialized = true;
            }

            // Apply attributes if different
            if (cell.Attributes != currentAttrs)
            {
                if ((cell.Attributes & CellAttributes.Bold) != (currentAttrs & CellAttributes.Bold))
                {
                    sb.Append((cell.Attributes & CellAttributes.Bold) != 0 ? "\x1b[1m" : "\x1b[22m");
                }
                if ((cell.Attributes & CellAttributes.Italic) != (currentAttrs & CellAttributes.Italic))
                {
                    sb.Append((cell.Attributes & CellAttributes.Italic) != 0 ? "\x1b[3m" : "\x1b[23m");
                }
                if ((cell.Attributes & CellAttributes.Underline) != (currentAttrs & CellAttributes.Underline))
                {
                    sb.Append((cell.Attributes & CellAttributes.Underline) != 0 ? "\x1b[4m" : "\x1b[24m");
                }
                currentAttrs = cell.Attributes;
            }

            // Update foreground color if needed
            if (!Equals(cell.Foreground, currentFg))
            {
                if (cell.Foreground.HasValue)
                {
                    var fg = cell.Foreground.Value;
                    sb.Append($"\x1b[38;2;{fg.R};{fg.G};{fg.B}m");
                }
                else
                {
                    sb.Append("\x1b[39m");
                }
                currentFg = cell.Foreground;
            }

            // Update background color if needed
            if (!Equals(cell.Background, currentBg))
            {
                if (cell.Background.HasValue)
                {
                    var bg = cell.Background.Value;
                    sb.Append($"\x1b[48;2;{bg.R};{bg.G};{bg.B}m");
                }
                else
                {
                    sb.Append("\x1b[49m");
                }
                currentBg = cell.Background;
            }

            // Write the character (skip empty continuation cells)
            if (!string.IsNullOrEmpty(cell.Character))
            {
                sb.Append(cell.Character);
                currentCol++;
            }
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
