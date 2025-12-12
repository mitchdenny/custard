using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b;

public sealed class TextBlockNode : Hex1bNode
{
    public string Text { get; set; } = "";
    public TextOverflow Overflow { get; set; } = TextOverflow.Overflow;
    
    /// <summary>
    /// Cached wrapped lines, computed during Measure when Overflow is Wrap.
    /// </summary>
    private List<string>? _wrappedLines;
    
    /// <summary>
    /// The width used to compute wrapped lines. If constraints change, we re-wrap.
    /// </summary>
    private int _lastWrapWidth = -1;

    public override Size Measure(Constraints constraints)
    {
        switch (Overflow)
        {
            case TextOverflow.Wrap:
                return MeasureWrapped(constraints);
                
            case TextOverflow.Ellipsis:
                // Ellipsis: single line, but respects max width
                var ellipsisWidth = Math.Min(Text.Length, constraints.MaxWidth);
                return constraints.Constrain(new Size(ellipsisWidth, 1));
                
            case TextOverflow.Overflow:
            default:
                // Original behavior: single-line, width is text length
                return constraints.Constrain(new Size(Text.Length, 1));
        }
    }

    private Size MeasureWrapped(Constraints constraints)
    {
        var maxWidth = constraints.MaxWidth;
        
        // If unbounded or very large, treat as single line
        if (maxWidth == int.MaxValue || maxWidth <= 0)
        {
            _wrappedLines = [Text];
            _lastWrapWidth = maxWidth;
            return constraints.Constrain(new Size(Text.Length, 1));
        }
        
        // Only re-wrap if width changed
        if (_wrappedLines == null || _lastWrapWidth != maxWidth)
        {
            _wrappedLines = WrapText(Text, maxWidth);
            _lastWrapWidth = maxWidth;
        }
        
        var width = _wrappedLines.Count > 0 ? _wrappedLines.Max(l => l.Length) : 0;
        var height = _wrappedLines.Count;
        
        return constraints.Constrain(new Size(width, height));
    }

    /// <summary>
    /// Wraps text to fit within the specified width.
    /// Uses word boundaries when possible, breaks words only when necessary.
    /// </summary>
    private static List<string> WrapText(string text, int maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0)
            return [""];
            
        var lines = new List<string>();
        var words = text.Split(' ');
        var currentLine = "";
        
        foreach (var word in words)
        {
            if (word.Length > maxWidth)
            {
                // Word is longer than max width - must break it
                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine);
                    currentLine = "";
                }
                
                // Break the word into chunks
                for (int i = 0; i < word.Length; i += maxWidth)
                {
                    var chunk = word.Substring(i, Math.Min(maxWidth, word.Length - i));
                    if (chunk.Length == maxWidth)
                    {
                        lines.Add(chunk);
                    }
                    else
                    {
                        currentLine = chunk;
                    }
                }
            }
            else if (currentLine.Length == 0)
            {
                currentLine = word;
            }
            else if (currentLine.Length + 1 + word.Length <= maxWidth)
            {
                currentLine += " " + word;
            }
            else
            {
                lines.Add(currentLine);
                currentLine = word;
            }
        }
        
        if (currentLine.Length > 0)
        {
            lines.Add(currentLine);
        }
        
        return lines.Count > 0 ? lines : [""];
    }

    public override void Render(Hex1bRenderContext context)
    {
        var colorCodes = context.GetInheritedColorCodes();
        var resetCodes = !string.IsNullOrEmpty(colorCodes) ? context.GetResetToInheritedCodes() : "";
        
        switch (Overflow)
        {
            case TextOverflow.Wrap:
                RenderWrapped(context, colorCodes, resetCodes);
                break;
                
            case TextOverflow.Ellipsis:
                RenderEllipsis(context, colorCodes, resetCodes);
                break;
                
            case TextOverflow.Overflow:
            default:
                RenderOverflow(context, colorCodes, resetCodes);
                break;
        }
    }

    private void RenderOverflow(Hex1bRenderContext context, string colorCodes, string resetCodes)
    {
        // When a LayoutProvider is active, use clipped rendering
        // Otherwise, use the original simple behavior for backward compatibility
        if (context.CurrentLayoutProvider != null)
        {
            // Use Bounds for position - parent sets cursor but we need absolute coords for clipping
            if (!string.IsNullOrEmpty(colorCodes))
            {
                context.WriteClipped(Bounds.X, Bounds.Y, $"{colorCodes}{Text}{resetCodes}");
            }
            else
            {
                context.WriteClipped(Bounds.X, Bounds.Y, Text);
            }
        }
        else
        {
            // No layout provider - write at current cursor position (original behavior)
            if (!string.IsNullOrEmpty(colorCodes))
            {
                context.Write($"{colorCodes}{Text}{resetCodes}");
            }
            else
            {
                context.Write(Text);
            }
        }
    }

    private void RenderWrapped(Hex1bRenderContext context, string colorCodes, string resetCodes)
    {
        if (_wrappedLines == null || _wrappedLines.Count == 0)
            return;
            
        for (int i = 0; i < _wrappedLines.Count && i < Bounds.Height; i++)
        {
            var line = _wrappedLines[i];
            var y = Bounds.Y + i;
            
            if (!string.IsNullOrEmpty(colorCodes))
            {
                context.WriteClipped(Bounds.X, y, $"{colorCodes}{line}{resetCodes}");
            }
            else
            {
                context.WriteClipped(Bounds.X, y, line);
            }
        }
    }

    private void RenderEllipsis(Hex1bRenderContext context, string colorCodes, string resetCodes)
    {
        var text = Text;
        if (Text.Length > Bounds.Width && Bounds.Width > 3)
        {
            text = Text.Substring(0, Bounds.Width - 3) + "...";
        }
        else if (Text.Length > Bounds.Width)
        {
            text = Text.Substring(0, Bounds.Width);
        }
        
        if (!string.IsNullOrEmpty(colorCodes))
        {
            context.WriteClipped(Bounds.X, Bounds.Y, $"{colorCodes}{text}{resetCodes}");
        }
        else
        {
            context.WriteClipped(Bounds.X, Bounds.Y, text);
        }
    }
}
