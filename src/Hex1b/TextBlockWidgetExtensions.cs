using Hex1b.Fluent;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for building TextBlockWidget using the fluent API.
/// </summary>
public static class TextBlockWidgetExtensions
{
    /// <summary>
    /// Creates a TextBlockWidget with the specified text.
    /// </summary>
    public static TextBlockWidget Text<TState>(this WidgetContext<TState> context, string text)
        => new(text);

    /// <summary>
    /// Creates a TextBlockWidget with text derived from state.
    /// </summary>
    public static TextBlockWidget Text<TState>(this WidgetContext<TState> context, Func<TState, string> textSelector)
        => new(textSelector(context.State));

    /// <summary>
    /// Adds a TextBlockWidget with the specified text to the builder.
    /// </summary>
    public static void Text<TBuilder>(this TBuilder builder, string text)
        where TBuilder : IChildBuilder
        => builder.Add(new TextBlockWidget(text));

    /// <summary>
    /// Adds a TextBlockWidget with text derived from context state to the builder.
    /// </summary>
    public static void Text<TBuilder, TState>(this TBuilder builder, WidgetContext<TState> context, Func<TState, string> textSelector)
        where TBuilder : IChildBuilder
        => builder.Add(new TextBlockWidget(textSelector(context.State)));
}
