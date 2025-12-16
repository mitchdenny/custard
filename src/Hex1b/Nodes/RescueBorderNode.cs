using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A border node with rescue-specific hardcoded styling.
/// Draws a double-line border with rescue colors.
/// </summary>
public sealed class RescueBorderNode : Hex1bNode
{
    public Hex1bNode? Child { get; set; }
    
    // Border characters (hardcoded)
    private const char TopLeft = '╔';
    private const char TopRight = '╗';
    private const char BottomLeft = '╚';
    private const char BottomRight = '╝';
    private const char Horizontal = '═';
    private const char Vertical = '║';

    public override Size Measure(Constraints constraints)
    {
        // Account for border (1 char on each side)
        var innerConstraints = new Constraints(
            Math.Max(0, constraints.MinWidth - 2),
            Math.Max(0, constraints.MaxWidth - 2),
            Math.Max(0, constraints.MinHeight - 2),
            Math.Max(0, constraints.MaxHeight - 2)
        );
        
        var childSize = Child?.Measure(innerConstraints) ?? Size.Zero;
        
        return constraints.Constrain(new Size(childSize.Width + 2, childSize.Height + 2));
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        
        // Child gets bounds inside the border
        if (Child != null && bounds.Width > 2 && bounds.Height > 2)
        {
            Child.Arrange(new Rect(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2));
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        var x = Bounds.X;
        var y = Bounds.Y;
        var width = Bounds.Width;
        var height = Bounds.Height;

        if (width < 2 || height < 2) return;

        var bgCode = RescueFallbackWidget.BackgroundColor.ToBackgroundAnsi();
        var fgCode = RescueFallbackWidget.BorderColor.ToForegroundAnsi();
        var reset = "\x1b[0m";

        // Top border
        context.SetCursorPosition(x, y);
        context.Write($"{bgCode}{fgCode}{TopLeft}{new string(Horizontal, width - 2)}{TopRight}{reset}");

        // Side borders
        for (int row = 1; row < height - 1; row++)
        {
            context.SetCursorPosition(x, y + row);
            context.Write($"{bgCode}{fgCode}{Vertical}{reset}");
            context.SetCursorPosition(x + width - 1, y + row);
            context.Write($"{bgCode}{fgCode}{Vertical}{reset}");
        }

        // Bottom border
        context.SetCursorPosition(x, y + height - 1);
        context.Write($"{bgCode}{fgCode}{BottomLeft}{new string(Horizontal, width - 2)}{BottomRight}{reset}");

        // Render child content
        Child?.Render(context);
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Child != null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Child != null) yield return Child;
    }
}
