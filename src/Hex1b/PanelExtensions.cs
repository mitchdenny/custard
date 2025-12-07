namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building PanelWidget.
/// </summary>
public static class PanelExtensions
{
    /// <summary>
    /// Creates a Panel wrapping a single child widget.
    /// </summary>
    public static PanelWidget Panel<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Hex1bWidget child)
        where TParent : Hex1bWidget
        => new(child);

    /// <summary>
    /// Creates a Panel with a VStack child.
    /// </summary>
    public static PanelWidget Panel<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Func<WidgetContext<VStackWidget, TState>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<VStackWidget, TState>(ctx.State);
        var children = builder(childCtx);
        return new PanelWidget(new VStackWidget(children));
    }

    /// <summary>
    /// Creates a Panel with narrowed state.
    /// </summary>
    public static PanelWidget Panel<TParent, TState, TChildState>(
        this WidgetContext<TParent, TState> ctx,
        TChildState childState,
        Func<WidgetContext<VStackWidget, TChildState>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<VStackWidget, TChildState>(childState);
        var children = builder(childCtx);
        return new PanelWidget(new VStackWidget(children));
    }
}
