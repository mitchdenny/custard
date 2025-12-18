namespace Hex1b;

using Hex1b.Events;
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

    /// <summary>
    /// Creates a List with state selected from context state and a synchronous item activated callback.
    /// </summary>
    public static ListWidget List<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Func<TState, ListState> stateSelector,
        Action<ListItemActivatedEventArgs> onItemActivated)
        where TParent : Hex1bWidget
        => new(stateSelector(ctx.State)) { OnItemActivated = args => { onItemActivated(args); return Task.CompletedTask; } };

    /// <summary>
    /// Creates a List with the specified state and a synchronous item activated callback.
    /// </summary>
    public static ListWidget List<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        ListState listState,
        Action<ListItemActivatedEventArgs> onItemActivated)
        where TParent : Hex1bWidget
        => new(listState) { OnItemActivated = args => { onItemActivated(args); return Task.CompletedTask; } };

    /// <summary>
    /// Creates a List with the specified state and an async item activated callback.
    /// </summary>
    public static ListWidget List<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        ListState listState,
        Func<ListItemActivatedEventArgs, Task> onItemActivated)
        where TParent : Hex1bWidget
        => new(listState) { OnItemActivated = onItemActivated };
}
