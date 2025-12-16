using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A button node with rescue-specific hardcoded styling.
/// Functions like a normal button but uses rescue colors.
/// </summary>
public sealed class RescueButtonNode : Hex1bNode
{
    public string Label { get; set; } = "";
    public Action? ClickAction { get; set; }
    
    private bool _isFocused;
    public override bool IsFocused { get => _isFocused; set => _isFocused = value; }

    private bool _isHovered;
    public override bool IsHovered { get => _isHovered; set => _isHovered = value; }

    public override bool IsFocusable => true;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        if (ClickAction != null)
        {
            bindings.Key(Hex1bKey.Enter).Action(ClickAction, "Activate button");
            bindings.Key(Hex1bKey.Spacebar).Action(ClickAction, "Activate button");
            bindings.Mouse(MouseButton.Left).Action(ClickAction, "Click button");
        }
    }

    public override Size Measure(Constraints constraints)
    {
        // Button renders as "[ Label ]" - 4 chars for brackets/spaces + label length
        var width = Label.Length + 4;
        var height = 1;
        return constraints.Constrain(new Size(width, height));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var reset = "\x1b[0m";
        
        string output;
        if (IsFocused)
        {
            var fg = RescueFallbackWidget.ButtonFocusedFg.ToForegroundAnsi();
            var bg = RescueFallbackWidget.ButtonFocusedBg.ToBackgroundAnsi();
            output = $"{fg}{bg}[ {Label} ]{reset}";
        }
        else
        {
            var fg = RescueFallbackWidget.ButtonNormalFg.ToForegroundAnsi();
            var bg = RescueFallbackWidget.ButtonNormalBg.ToBackgroundAnsi();
            output = $"{fg}{bg}[ {Label} ]{reset}";
        }
        
        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(Bounds.X, Bounds.Y, output);
        }
        else
        {
            context.SetCursorPosition(Bounds.X, Bounds.Y);
            context.Write(output);
        }
    }
}
