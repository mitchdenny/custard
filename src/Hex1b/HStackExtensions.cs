namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building HStack widgets.
/// </summary>
public static class HStackExtensions
{
    /// <summary>
    /// Creates an HStack where the callback returns an array of children.
    /// </summary>
    public static HStackWidget HStack<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Func<WidgetContext<HStackWidget, TState>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<HStackWidget, TState>(ctx.State);
        var children = builder(childCtx);
        return new HStackWidget(children);
    }

    /// <summary>
    /// Creates an HStack with narrowed state.
    /// </summary>
    public static HStackWidget HStack<TParent, TState, TChildState>(
        this WidgetContext<TParent, TState> ctx,
        TChildState childState,
        Func<WidgetContext<HStackWidget, TChildState>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<HStackWidget, TChildState>(childState);
        var children = builder(childCtx);
        return new HStackWidget(children);
    }

    /// <summary>
    /// Creates an HStack with state selected from parent state.
    /// </summary>
    public static HStackWidget HStack<TParent, TState, TChildState>(
        this WidgetContext<TParent, TState> ctx,
        Func<TState, TChildState> stateSelector,
        Func<WidgetContext<HStackWidget, TChildState>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<HStackWidget, TChildState>(stateSelector(ctx.State));
        var children = builder(childCtx);
        return new HStackWidget(children);
    }
}
