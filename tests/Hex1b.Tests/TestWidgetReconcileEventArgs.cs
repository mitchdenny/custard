using Hex1b;
using Hex1b.Events;
using Hex1b.Input;

namespace Hex1b.Tests;

internal sealed class TestWidgetReconcileEventArgs : WidgetEventArgs<TestWidget, TestWidgetNode>
{
    public TestWidgetReconcileEventArgs(
        TestWidget widget,
        TestWidgetNode node,
        InputBindingActionContext context,
        int reconcileCount,
        Hex1bNode? existingNode)
        : base(widget, node, context)
    {
        ReconcileCount = reconcileCount;
        ExistingNode = existingNode;
    }

    public int ReconcileCount { get; }
    public Hex1bNode? ExistingNode { get; }
}
