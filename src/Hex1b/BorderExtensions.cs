namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building BorderWidget.
/// </summary>
public static class BorderExtensions
{
    /// <summary>
    /// Creates a Border wrapping a single child widget.
    /// </summary>
    public static BorderWidget Border<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Hex1bWidget child,
        string? title = null)
        where TParent : Hex1bWidget
        => new(child, title);

    /// <summary>
    /// Creates a Border with a VStack child.
    /// </summary>
    public static BorderWidget Border<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Func<WidgetContext<VStackWidget, TState>, Hex1bWidget[]> builder,
        string? title = null)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<VStackWidget, TState>(ctx.State);
        var children = builder(childCtx);
        return new BorderWidget(new VStackWidget(children), title);
    }

    /// <summary>
    /// Creates a Border with narrowed state.
    /// </summary>
    public static BorderWidget Border<TParent, TState, TChildState>(
        this WidgetContext<TParent, TState> ctx,
        TChildState childState,
        Func<WidgetContext<VStackWidget, TChildState>, Hex1bWidget[]> builder,
        string? title = null)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<VStackWidget, TChildState>(childState);
        var children = builder(childCtx);
        return new BorderWidget(new VStackWidget(children), title);
    }
}
