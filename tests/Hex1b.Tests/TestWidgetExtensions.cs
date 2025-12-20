using Hex1b.Widgets;

namespace Hex1b.Tests;

public static class TestWidgetExtensions
{
    /// <summary>
    /// Creates a <see cref="TestWidget"/> for witnessing render loop timing.
    /// </summary>
    public static TestWidget Test<TParent>(this WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
        => new();
}
