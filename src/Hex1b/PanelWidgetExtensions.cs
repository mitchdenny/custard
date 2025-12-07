using Hex1b.Fluent;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for building PanelWidget using the fluent API.
/// </summary>
public static class PanelWidgetExtensions
{
    /// <summary>
    /// Creates a PanelWidget wrapping the specified child.
    /// </summary>
    public static PanelWidget Panel<TState>(
        this WidgetContext<TState> context,
        Hex1bWidget child)
        => new(child);

    /// <summary>
    /// Creates a PanelWidget with a VStack child built using a builder action.
    /// </summary>
    public static PanelWidget Panel<TState>(
        this WidgetContext<TState> context,
        Action<VStackBuilder<TState>> childBuilder)
    {
        var child = context.VStack(childBuilder);
        return new PanelWidget(child);
    }

    /// <summary>
    /// Adds a PanelWidget to the builder.
    /// </summary>
    public static void Panel<TBuilder>(
        this TBuilder builder,
        Hex1bWidget child)
        where TBuilder : IChildBuilder
        => builder.Add(new PanelWidget(child));

    /// <summary>
    /// Adds a PanelWidget with VStack content to a VStackBuilder.
    /// </summary>
    public static void Panel<TState>(
        this VStackBuilder<TState> builder,
        Action<VStackBuilder<TState>> childBuilder,
        SizeHint? sizeHint = null)
    {
        var child = builder.Context.VStack(childBuilder);
        builder.Add(new PanelWidget(child), sizeHint);
    }
}
