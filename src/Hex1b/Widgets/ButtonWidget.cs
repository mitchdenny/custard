using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

public sealed record ButtonWidget(string Label) : Hex1bWidget
{
    /// <summary>
    /// The async click handler. All handlers are normalized to async for consistency.
    /// </summary>
    public Func<ActionContext, Task>? OnClick { get; init; }

    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ButtonNode ?? new ButtonNode();
        node.Label = Label;
        node.ClickAction = OnClick;
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(ButtonNode);
}
