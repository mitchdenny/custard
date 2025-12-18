using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for button click events.
/// </summary>
public sealed class ButtonClickedEventArgs : WidgetEventArgs<ButtonWidget, ButtonNode>
{
    public ButtonClickedEventArgs(ButtonWidget widget, ButtonNode node, InputBindingActionContext context)
        : base(widget, node, context)
    {
    }
}
