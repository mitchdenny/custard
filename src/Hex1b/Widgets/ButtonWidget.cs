using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

public sealed record ButtonWidget(string Label) : Hex1bWidget
{
    /// <summary>
    /// The async click handler. Called when the button is activated via Enter, Space, or mouse click.
    /// </summary>
    public Func<ButtonClickedEventArgs, Task>? OnClick { get; init; }

    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ButtonNode ?? new ButtonNode();
        node.Label = Label;
        node.SourceWidget = this;
        
        // Convert the typed event handler to the internal InputBindingActionContext handler
        if (OnClick != null)
        {
            node.ClickAction = async ctx => 
            {
                var args = new ButtonClickedEventArgs(this, node, ctx);
                await OnClick(args);
            };
        }
        else
        {
            node.ClickAction = null;
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(ButtonNode);
}
