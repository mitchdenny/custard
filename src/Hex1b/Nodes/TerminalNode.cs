using Hex1b.Layout;
using Hex1b.Terminal;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A node that renders the output of an embedded Hex1bTerminal.
/// </summary>
/// <remarks>
/// This node displays the screen buffer of an embedded terminal,
/// supporting resizing and clipping like any other widget.
/// </remarks>
public sealed class TerminalNode : Hex1bNode
{
    /// <summary>
    /// The embedded terminal to display.
    /// </summary>
    private Hex1bTerminal? _terminal;
    public Hex1bTerminal? Terminal 
    { 
        get => _terminal; 
        set
        {
            if (_terminal != value)
            {
                _terminal = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// Cached terminal output lines for rendering.
    /// </summary>
    private string[]? _cachedLines;
    
    /// <summary>
    /// Last measured size to detect if we need to resize the terminal.
    /// </summary>
    private Size _lastMeasuredSize = Size.Zero;

    public override Size Measure(Constraints constraints)
    {
        if (Terminal == null)
        {
            return constraints.Constrain(Size.Zero);
        }

        // The terminal has a fixed size based on its width/height
        // We'll constrain it to fit within the available space
        var termWidth = Terminal.Width;
        var termHeight = Terminal.Height;
        
        var size = new Size(termWidth, termHeight);
        _lastMeasuredSize = constraints.Constrain(size);
        
        return _lastMeasuredSize;
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        
        // Resize the terminal if needed to fit the allocated bounds
        if (Terminal != null && (bounds.Width != Terminal.Width || bounds.Height != Terminal.Height))
        {
            Terminal.Resize(bounds.Width, bounds.Height);
            MarkDirty();
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (Terminal == null)
        {
            return;
        }

        // Get the terminal's rendered output
        var lines = Terminal.GetScreenText().Split('\n');
        _cachedLines = lines;

        // Render each line within bounds, respecting clipping
        var y = Bounds.Y;
        for (int i = 0; i < lines.Length && i < Bounds.Height; i++)
        {
            var line = lines[i];
            
            // Clip the line to the bounds width using display-width-aware slicing
            var displayLine = line;
            var lineWidth = DisplayWidth.GetStringWidth(line);
            if (lineWidth > Bounds.Width)
            {
                var result = DisplayWidth.SliceByDisplayWidth(line, 0, Bounds.Width);
                displayLine = result.text;
            }
            
            // Use WriteClipped to respect layout provider clipping
            context.WriteClipped(Bounds.X, y + i, displayLine);
        }
    }
}
