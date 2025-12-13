using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A vertical splitter/divider that separates left and right panes.
/// </summary>
public sealed record SplitterWidget(Hex1bWidget Left, Hex1bWidget Right, int LeftWidth = 30) : Hex1bWidget
{
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as SplitterNode ?? new SplitterNode();
        node.Left = context.ReconcileChild(node.Left, Left, node);
        node.Right = context.ReconcileChild(node.Right, Right, node);
        
        // Only set LeftWidth on initial creation - preserve user resizing
        if (context.IsNew)
        {
            node.LeftWidth = LeftWidth;
        }
        
        // Invalidate focus cache since children may have changed
        node.InvalidateFocusCache();
        
        // Set initial focus if this is a new node
        if (context.IsNew)
        {
            node.SetInitialFocus();
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(SplitterNode);
}
