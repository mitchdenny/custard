using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that wraps content with a condition that determines whether it should be displayed.
/// Used as part of a ResponsiveWidget to create conditional UI layouts.
/// </summary>
/// <param name="Condition">A function that receives (availableWidth, availableHeight) and returns true if this content should be displayed.</param>
/// <param name="Content">The content to display when the condition is met.</param>
public sealed record ConditionalWidget(Func<int, int, bool> Condition, Hex1bWidget Content) : Hex1bWidget
{
    // ConditionalWidget is never directly reconciled - it's used as configuration for ResponsiveWidget
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
        => throw new NotSupportedException("ConditionalWidget should not be reconciled directly. Use ResponsiveWidget instead.");

    internal override Type GetExpectedNodeType()
        => throw new NotSupportedException("ConditionalWidget should not be reconciled directly. Use ResponsiveWidget instead.");
}

/// <summary>
/// A widget that displays the first child whose condition evaluates to true.
/// Conditions are evaluated during layout with the available size from parent constraints.
/// </summary>
/// <param name="Branches">The list of conditional widgets to evaluate. The first matching branch is displayed.</param>
public sealed record ResponsiveWidget(IReadOnlyList<ConditionalWidget> Branches) : Hex1bWidget
{
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ResponsiveNode ?? new ResponsiveNode();
        node.Branches = Branches;

        // Reconcile child nodes for each branch
        var newChildNodes = new List<Hex1bNode?>();
        for (int i = 0; i < Branches.Count; i++)
        {
            var existingChild = i < node.ChildNodes.Count ? node.ChildNodes[i] : null;
            var reconciledChild = context.ReconcileChild(existingChild, Branches[i].Content, node);
            newChildNodes.Add(reconciledChild);
        }
        node.ChildNodes = newChildNodes;

        // Set initial focus on the first focusable node in each branch.
        // Since we don't know which branch will be active until Measure(),
        // we pre-set focus on all branches' first focusable nodes.
        if (context.IsNew)
        {
            foreach (var child in newChildNodes)
            {
                if (child != null)
                {
                    var firstFocusable = child.GetFocusableNodes().FirstOrDefault();
                    if (firstFocusable != null)
                    {
                        ReconcileContext.SetNodeFocus(firstFocusable, true);
                    }
                }
            }
        }

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(ResponsiveNode);
}
