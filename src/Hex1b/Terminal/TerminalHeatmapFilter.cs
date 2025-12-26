using System.Text;

namespace Hex1b.Terminal;

/// <summary>
/// A presentation adapter wrapper that visualizes terminal cell update frequency as a heatmap.
/// </summary>
/// <remarks>
/// <para>
/// This adapter wraps another presentation adapter and can intercept terminal output.
/// When enabled, it replaces the normal terminal display with a heatmap showing
/// update "heat" - cells that are updated more frequently appear hotter (different colors/blocks).
/// </para>
/// <para>
/// The heatmap tracks updates over a sliding time window (configurable, default 2 seconds).
/// This allows you to see which parts of your terminal UI are being redrawn most frequently,
/// which is useful for:
/// <list type="bullet">
///   <item>Performance analysis - identifying unnecessary redraws</item>
///   <item>Render optimization - finding hotspots in your UI</item>
///   <item>Debugging - visualizing update patterns</item>
/// </list>
/// </para>
/// <example>
/// <code>
/// var console = new ConsolePresentationAdapter();
/// var heatmap = new TerminalHeatmapFilter(console);
/// var workload = new Hex1bAppWorkloadAdapter(heatmap.Capabilities);
/// var terminal = new Hex1bTerminal(heatmap, workload);
/// 
/// // Later, toggle the heatmap on/off (e.g., from a keyboard binding)
/// heatmap.Enable();  // Show heatmap
/// heatmap.Disable(); // Show normal output
/// </code>
/// </example>
/// </remarks>
public sealed class TerminalHeatmapFilter : IHex1bTerminalPresentationAdapter
{
    private readonly IHex1bTerminalPresentationAdapter _innerAdapter;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _heatmapWindow;
    
    // Heatmap data: tracks timestamps of recent updates for each cell
    private List<DateTimeOffset>[,] _cellUpdateHistory;
    
    // Previous screen buffer state (to detect changes by parsing output)
    private TerminalCell[,] _previousBuffer;
    
    // Current screen buffer (parsed from output)
    private TerminalCell[,] _currentBuffer;
    
    // Enabled state
    private bool _enabled;
    
    // Lock for thread safety
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new terminal heatmap filter that wraps another presentation adapter.
    /// </summary>
    /// <param name="innerAdapter">The underlying presentation adapter to wrap.</param>
    /// <param name="heatmapWindow">Time window for tracking updates (default 2 seconds).</param>
    /// <param name="timeProvider">Time provider for timestamp comparisons (default system time).</param>
    public TerminalHeatmapFilter(
        IHex1bTerminalPresentationAdapter innerAdapter,
        TimeSpan? heatmapWindow = null,
        TimeProvider? timeProvider = null)
    {
        _innerAdapter = innerAdapter ?? throw new ArgumentNullException(nameof(innerAdapter));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _heatmapWindow = heatmapWindow ?? TimeSpan.FromSeconds(2);
        
        // Initialize heatmap data structures
        var height = innerAdapter.Height;
        var width = innerAdapter.Width;
        _cellUpdateHistory = new List<DateTimeOffset>[height, width];
        _previousBuffer = new TerminalCell[height, width];
        _currentBuffer = new TerminalCell[height, width];
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                _cellUpdateHistory[y, x] = new List<DateTimeOffset>();
                _previousBuffer[y, x] = TerminalCell.Empty;
                _currentBuffer[y, x] = TerminalCell.Empty;
            }
        }
        
        // Forward the Resized event
        _innerAdapter.Resized += (w, h) =>
        {
            ResizeHeatmap(w, h);
            Resized?.Invoke(w, h);
        };
        
        // Forward the Disconnected event  
        _innerAdapter.Disconnected += () => Disconnected?.Invoke();
    }

    /// <summary>
    /// Gets whether the heatmap is currently enabled.
    /// </summary>
    public bool IsEnabled
    {
        get
        {
            lock (_lock)
            {
                return _enabled;
            }
        }
    }

    /// <summary>
    /// Enables the heatmap display, replacing normal terminal output with the heatmap visualization.
    /// </summary>
    public async void Enable()
    {
        lock (_lock)
        {
            if (_enabled) return;
            _enabled = true;
        }
        
        // Re-render the screen with heatmap
        await RenderHeatmapAsync();
    }

    /// <summary>
    /// Disables the heatmap display, restoring normal terminal output.
    /// </summary>
    public async void Disable()
    {
        lock (_lock)
        {
            if (!_enabled) return;
            _enabled = false;
        }
        
        // Re-render the screen with actual content
        await RenderActualContentAsync();
    }

    // IHex1bTerminalPresentationAdapter implementation
    
    /// <inheritdoc />
    public async ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        bool enabled;
        lock (_lock)
        {
            enabled = _enabled;
        }
        
        if (!enabled)
        {
            // Pass through when disabled
            await _innerAdapter.WriteOutputAsync(data, ct);
            return;
        }
        
        // When enabled, parse the output to track changes and show heatmap instead
        ParseAndTrackOutput(data);
        await RenderHeatmapAsync(ct);
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        return _innerAdapter.ReadInputAsync(ct);
    }

    /// <inheritdoc />
    public int Width => _innerAdapter.Width;

    /// <inheritdoc />
    public int Height => _innerAdapter.Height;

    /// <inheritdoc />
    public TerminalCapabilities Capabilities => _innerAdapter.Capabilities;

    /// <inheritdoc />
    public event Action<int, int>? Resized;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken ct = default)
    {
        return _innerAdapter.FlushAsync(ct);
    }

    /// <inheritdoc />
    public ValueTask EnterTuiModeAsync(CancellationToken ct = default)
    {
        return _innerAdapter.EnterTuiModeAsync(ct);
    }

    /// <inheritdoc />
    public ValueTask ExitTuiModeAsync(CancellationToken ct = default)
    {
        return _innerAdapter.ExitTuiModeAsync(ct);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return _innerAdapter.DisposeAsync();
    }

    // Private implementation methods

    private void ResizeHeatmap(int newWidth, int newHeight)
    {
        lock (_lock)
        {
            var newCellUpdateHistory = new List<DateTimeOffset>[newHeight, newWidth];
            var newPreviousBuffer = new TerminalCell[newHeight, newWidth];
            var newCurrentBuffer = new TerminalCell[newHeight, newWidth];
            
            var copyHeight = Math.Min(_currentBuffer.GetLength(0), newHeight);
            var copyWidth = Math.Min(_currentBuffer.GetLength(1), newWidth);
            
            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    if (y < copyHeight && x < copyWidth)
                    {
                        newCellUpdateHistory[y, x] = _cellUpdateHistory[y, x];
                        newPreviousBuffer[y, x] = _previousBuffer[y, x];
                        newCurrentBuffer[y, x] = _currentBuffer[y, x];
                    }
                    else
                    {
                        newCellUpdateHistory[y, x] = new List<DateTimeOffset>();
                        newPreviousBuffer[y, x] = TerminalCell.Empty;
                        newCurrentBuffer[y, x] = TerminalCell.Empty;
                    }
                }
            }
            
            _cellUpdateHistory = newCellUpdateHistory;
            _previousBuffer = newPreviousBuffer;
            _currentBuffer = newCurrentBuffer;
        }
    }

    private void ParseAndTrackOutput(ReadOnlyMemory<byte> data)
    {
        // Simple approach: Parse ANSI output and track which cells are written to
        // For a complete implementation, we'd need a full ANSI parser
        // For now, we'll use a simpler approach: just compare with previous buffer
        
        var text = Encoding.UTF8.GetString(data.Span);
        var now = _timeProvider.GetUtcNow();
        var cutoffTime = now - _heatmapWindow;
        
        lock (_lock)
        {
            // For simplicity, mark all non-empty cells as updated
            // In a real implementation, we'd parse the ANSI sequences to know exactly which cells changed
            // This is a simplified heuristic for demonstration
            
            // Remove old entries from all cells
            for (int y = 0; y < _cellUpdateHistory.GetLength(0); y++)
            {
                for (int x = 0; x < _cellUpdateHistory.GetLength(1); x++)
                {
                    _cellUpdateHistory[y, x].RemoveAll(t => t < cutoffTime);
                }
            }
            
            // For now, assume any output means the whole screen updated
            // This is a simplification - a real parser would track specific cells
            if (text.Contains("\x1b["))
            {
                // Add update to all cells (simplified approach)
                for (int y = 0; y < _cellUpdateHistory.GetLength(0); y++)
                {
                    for (int x = 0; x < _cellUpdateHistory.GetLength(1); x++)
                    {
                        _cellUpdateHistory[y, x].Add(now);
                    }
                }
            }
        }
    }

    private async ValueTask RenderHeatmapAsync(CancellationToken ct = default)
    {
        var output = GenerateHeatmapOutput();
        if (output.Length > 0)
        {
            await _innerAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes(output), ct);
        }
    }

    private async ValueTask RenderActualContentAsync()
    {
        lock (_lock)
        {
            // Generate output from current buffer
            var output = GenerateScreenOutput(_currentBuffer);
            if (output.Length > 0)
            {
                _ = _innerAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes(output));
            }
        }
        await ValueTask.CompletedTask;
    }

    private string GenerateHeatmapOutput()
    {
        var sb = new StringBuilder();
        
        // Move to home and clear screen
        sb.Append("\x1b[H\x1b[2J");
        
        lock (_lock)
        {
            var height = _cellUpdateHistory.GetLength(0);
            var width = _cellUpdateHistory.GetLength(1);
            
            for (int y = 0; y < height; y++)
            {
                sb.Append($"\x1b[{y + 1};1H"); // Move to line
                
                for (int x = 0; x < width; x++)
                {
                    var updateCount = _cellUpdateHistory[y, x].Count;
                    var (block, color) = GetHeatVisualization(updateCount);
                    
                    // Apply color and render block
                    sb.Append(color);
                    sb.Append(block);
                }
                
                sb.Append("\x1b[0m"); // Reset attributes
            }
        }
        
        return sb.ToString();
    }

    private string GenerateScreenOutput(TerminalCell[,] screenBuffer)
    {
        var sb = new StringBuilder();
        
        // Move to home and clear screen
        sb.Append("\x1b[H\x1b[2J");
        
        for (int y = 0; y < screenBuffer.GetLength(0); y++)
        {
            sb.Append($"\x1b[{y + 1};1H"); // Move to line
            
            for (int x = 0; x < screenBuffer.GetLength(1); x++)
            {
                var cell = screenBuffer[y, x];
                
                // Apply colors if present
                if (cell.Foreground.HasValue)
                {
                    var fg = cell.Foreground.Value;
                    sb.Append($"\x1b[38;2;{fg.R};{fg.G};{fg.B}m");
                }
                if (cell.Background.HasValue)
                {
                    var bg = cell.Background.Value;
                    sb.Append($"\x1b[48;2;{bg.R};{bg.G};{bg.B}m");
                }
                
                // Render character
                sb.Append(string.IsNullOrEmpty(cell.Character) ? " " : cell.Character);
                
                // Reset if needed
                if (cell.Foreground.HasValue || cell.Background.HasValue)
                {
                    sb.Append("\x1b[0m");
                }
            }
        }
        
        return sb.ToString();
    }

    private (string Block, string Color) GetHeatVisualization(int updateCount)
    {
        // Map update count to heat level and visualization
        // 0 updates = cold (dark blue/black)
        // 1-2 = cool (blue)
        // 3-5 = warm (yellow)
        // 6-10 = hot (orange)
        // 11+ = very hot (red)
        
        // Unicode block characters from lightest to darkest
        return updateCount switch
        {
            0 => (" ", "\x1b[38;2;20;20;40m"), // Very dark blue
            1 => ("░", "\x1b[38;2;50;50;200m"), // Light blue
            2 => ("▒", "\x1b[38;2;100;100;255m"), // Blue
            3 => ("▓", "\x1b[38;2;200;200;100m"), // Yellow
            4 or 5 => ("█", "\x1b[38;2;255;200;50m"), // Orange
            6 or 7 or 8 or 9 or 10 => ("█", "\x1b[38;2;255;100;0m"), // Red-orange
            _ => ("█", "\x1b[38;2;255;0;0m") // Bright red
        };
    }
}
