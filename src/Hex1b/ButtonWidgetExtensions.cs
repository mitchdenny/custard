using Hex1b.Fluent;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for building ButtonWidget using the fluent API.
/// </summary>
public static class ButtonWidgetExtensions
{
    /// <summary>
    /// Creates a ButtonWidget with the specified label and click handler.
    /// </summary>
    public static ButtonWidget Button<TState>(this WidgetContext<TState> context, string label, Action onClick)
        => new(label, onClick);

    /// <summary>
    /// Creates a ButtonWidget with label derived from state.
    /// </summary>
    public static ButtonWidget Button<TState>(this WidgetContext<TState> context, Func<TState, string> labelSelector, Action onClick)
        => new(labelSelector(context.State), onClick);

    /// <summary>
    /// Adds a ButtonWidget with the specified label and click handler to the builder.
    /// </summary>
    public static void Button<TBuilder>(this TBuilder builder, string label, Action onClick)
        where TBuilder : IChildBuilder
        => builder.Add(new ButtonWidget(label, onClick));

    /// <summary>
    /// Adds a ButtonWidget with label derived from context state to the builder.
    /// </summary>
    public static void Button<TBuilder, TState>(this TBuilder builder, WidgetContext<TState> context, Func<TState, string> labelSelector, Action onClick)
        where TBuilder : IChildBuilder
        => builder.Add(new ButtonWidget(labelSelector(context.State), onClick));
}
