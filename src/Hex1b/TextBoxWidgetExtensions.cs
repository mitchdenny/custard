using Hex1b.Fluent;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for building TextBoxWidget using the fluent API.
/// </summary>
public static class TextBoxWidgetExtensions
{
    /// <summary>
    /// Creates a TextBoxWidget with the specified state.
    /// </summary>
    public static TextBoxWidget TextBox<TState>(this WidgetContext<TState> context, TextBoxState state)
        => new(state);

    /// <summary>
    /// Creates a TextBoxWidget with state selected from the context state.
    /// </summary>
    public static TextBoxWidget TextBox<TState>(this WidgetContext<TState> context, Func<TState, TextBoxState> stateSelector)
        => new(stateSelector(context.State));

    /// <summary>
    /// Adds a TextBoxWidget with the specified state to the builder.
    /// </summary>
    public static void TextBox<TBuilder>(this TBuilder builder, TextBoxState state)
        where TBuilder : IChildBuilder
        => builder.Add(new TextBoxWidget(state));

    /// <summary>
    /// Adds a TextBoxWidget with state selected from context to the builder.
    /// </summary>
    public static void TextBox<TBuilder, TState>(this TBuilder builder, WidgetContext<TState> context, Func<TState, TextBoxState> stateSelector)
        where TBuilder : IChildBuilder
        => builder.Add(new TextBoxWidget(stateSelector(context.State)));
}
