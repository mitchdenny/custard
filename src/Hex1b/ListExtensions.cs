namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building ListWidget.
/// </summary>
public static class ListExtensions
{
    /// <summary>
    /// Creates a List with the specified state.
    /// </summary>
    public static ListWidget List<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        ListState listState)
        where TParent : Hex1bWidget
        => new(listState);

    /// <summary>
    /// Creates a List with state selected from context state.
    /// </summary>
    public static ListWidget List<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Func<TState, ListState> stateSelector)
        where TParent : Hex1bWidget
        => new(stateSelector(ctx.State));
}
