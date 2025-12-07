using Hex1b.Fluent;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for building ListWidget using the fluent API.
/// </summary>
public static class ListWidgetExtensions
{
    /// <summary>
    /// Creates a ListWidget with the specified state.
    /// </summary>
    public static ListWidget List<TState>(this WidgetContext<TState> context, ListState state)
        => new(state);

    /// <summary>
    /// Creates a ListWidget with state selected from the context state.
    /// </summary>
    public static ListWidget List<TState>(this WidgetContext<TState> context, Func<TState, ListState> stateSelector)
        => new(stateSelector(context.State));

    /// <summary>
    /// Adds a ListWidget with the specified state to the builder.
    /// </summary>
    public static void List<TBuilder>(this TBuilder builder, ListState state)
        where TBuilder : IChildBuilder
        => builder.Add(new ListWidget(state));

    /// <summary>
    /// Adds a ListWidget with state selected from context to the builder.
    /// </summary>
    public static void List<TBuilder, TState>(this TBuilder builder, WidgetContext<TState> context, Func<TState, ListState> stateSelector)
        where TBuilder : IChildBuilder
        => builder.Add(new ListWidget(stateSelector(context.State)));
}
