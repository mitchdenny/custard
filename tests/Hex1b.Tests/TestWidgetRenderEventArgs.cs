using Hex1b.Events;
using Hex1b.Input;

namespace Hex1b.Tests;

internal sealed class TestWidgetRenderEventArgs : WidgetEventArgs<TestWidget, TestWidgetNode>
{
    public TestWidgetRenderEventArgs(TestWidget widget, TestWidgetNode node, InputBindingActionContext context, int renderCount)
        : base(widget, node, context)
    {
        RenderCount = renderCount;
    }

    public int RenderCount { get; }
}
