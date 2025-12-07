using Hex1b.Fluent;
using Hex1b.Widgets;

namespace Hex1b;

#pragma warning disable HEX1B001 // Experimental Navigator API

/// <summary>
/// Extension methods for building NavigatorWidget using the fluent API.
/// </summary>
public static class NavigatorWidgetExtensions
{
    /// <summary>
    /// Creates a NavigatorWidget with the specified state.
    /// </summary>
    public static NavigatorWidget Navigator<TState>(this WidgetContext<TState> context, NavigatorState state)
        => new(state);

    /// <summary>
    /// Creates a NavigatorWidget with state selected from the context state.
    /// </summary>
    public static NavigatorWidget Navigator<TState>(this WidgetContext<TState> context, Func<TState, NavigatorState> stateSelector)
        => new(stateSelector(context.State));

    /// <summary>
    /// Adds a NavigatorWidget with the specified state to the builder.
    /// </summary>
    public static void Navigator<TBuilder>(this TBuilder builder, NavigatorState state)
        where TBuilder : IChildBuilder
        => builder.Add(new NavigatorWidget(state));

    /// <summary>
    /// Adds a NavigatorWidget with state selected from context to the builder.
    /// </summary>
    public static void Navigator<TBuilder, TState>(this TBuilder builder, WidgetContext<TState> context, Func<TState, NavigatorState> stateSelector)
        where TBuilder : IChildBuilder
        => builder.Add(new NavigatorWidget(stateSelector(context.State)));
}

#pragma warning restore HEX1B001
