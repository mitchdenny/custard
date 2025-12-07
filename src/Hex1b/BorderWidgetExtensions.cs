using Hex1b.Fluent;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for building BorderWidget using the fluent API.
/// </summary>
public static class BorderWidgetExtensions
{
    /// <summary>
    /// Creates a BorderWidget wrapping the specified child.
    /// </summary>
    public static BorderWidget Border<TState>(
        this WidgetContext<TState> context,
        Hex1bWidget child,
        string? title = null)
        => new(child, title);

    /// <summary>
    /// Creates a BorderWidget with a VStack child built using a builder action.
    /// </summary>
    public static BorderWidget Border<TState>(
        this WidgetContext<TState> context,
        Action<VStackBuilder<TState>> childBuilder,
        string? title = null)
    {
        var child = context.VStack(childBuilder);
        return new BorderWidget(child, title);
    }

    /// <summary>
    /// Adds a BorderWidget to the builder.
    /// </summary>
    public static void Border<TBuilder>(
        this TBuilder builder,
        Hex1bWidget child,
        string? title = null)
        where TBuilder : IChildBuilder
        => builder.Add(new BorderWidget(child, title));

    /// <summary>
    /// Adds a BorderWidget with VStack content to a VStackBuilder.
    /// </summary>
    public static void Border<TState>(
        this VStackBuilder<TState> builder,
        Action<VStackBuilder<TState>> childBuilder,
        string? title = null,
        SizeHint? sizeHint = null)
    {
        var child = builder.Context.VStack(childBuilder);
        builder.Add(new BorderWidget(child, title), sizeHint);
    }

    /// <summary>
    /// Adds a BorderWidget with VStack content to an HStackBuilder.
    /// </summary>
    public static void Border<TState>(
        this HStackBuilder<TState> builder,
        Action<VStackBuilder<TState>> childBuilder,
        string? title = null,
        SizeHint? sizeHint = null)
    {
        var child = builder.Context.VStack(childBuilder);
        builder.Add(new BorderWidget(child, title), sizeHint);
    }
}
