using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A container node that sets hardcoded rescue colors on the render context,
/// bypassing the theme system. This allows child widgets to render with
/// consistent rescue styling without relying on theming.
/// </summary>
public sealed class RescueFallbackContainerNode : Hex1bNode
{
    public Hex1bNode? Child { get; set; }
    
    private List<Hex1bNode>? _focusableNodes;

    public override Size Measure(Constraints constraints)
    {
        // Fill all available space
        Child?.Measure(constraints);
        return constraints.Constrain(new Size(constraints.MaxWidth, constraints.MaxHeight));
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        Child?.Arrange(bounds);
    }

    public override void Render(Hex1bRenderContext context)
    {
        // Save previous inherited colors
        var previousForeground = context.InheritedForeground;
        var previousBackground = context.InheritedBackground;
        
        // Set hardcoded rescue colors
        context.InheritedForeground = RescueFallbackWidget.TextColor;
        context.InheritedBackground = RescueFallbackWidget.BackgroundColor;
        
        // Fill background
        var bgCode = RescueFallbackWidget.BackgroundColor.ToBackgroundAnsi();
        var reset = "\x1b[0m";
        for (int row = 0; row < Bounds.Height; row++)
        {
            context.SetCursorPosition(Bounds.X, Bounds.Y + row);
            context.Write($"{bgCode}{new string(' ', Bounds.Width)}{reset}");
        }
        
        // Render child content
        Child?.Render(context);
        
        // Restore previous inherited colors
        context.InheritedForeground = previousForeground;
        context.InheritedBackground = previousBackground;
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
    
    public void InvalidateFocusCache()
    {
        _focusableNodes = null;
    }

    public void SetInitialFocus()
    {
        _focusableNodes ??= GetFocusableNodes().ToList();
        if (_focusableNodes.Count > 0)
        {
            _focusableNodes[0].IsFocused = true;
        }
    }
}
