using Hex1b.Nodes;

namespace Hex1b.Widgets;

public sealed record ButtonWidget(string Label, Action OnClick) : Hex1bWidget
{
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ButtonNode ?? new ButtonNode();
        node.Label = Label;
        node.OnClick = OnClick;
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(ButtonNode);
}
