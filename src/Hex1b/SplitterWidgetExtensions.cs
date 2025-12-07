using Hex1b.Fluent;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for building SplitterWidget using the fluent API.
/// </summary>
public static class SplitterWidgetExtensions
{
    /// <summary>
    /// Creates a SplitterWidget with left and right panels.
    /// </summary>
    public static SplitterWidget Splitter<TState>(
        this WidgetContext<TState> context,
        Hex1bWidget left,
        Hex1bWidget right,
        int leftWidth = 30)
        => new(left, right, leftWidth);

    /// <summary>
    /// Creates a SplitterWidget with VStack builders for left and right panels.
    /// </summary>
    public static SplitterWidget Splitter<TState>(
        this WidgetContext<TState> context,
        Action<VStackBuilder<TState>> leftBuilder,
        Action<VStackBuilder<TState>> rightBuilder,
        int leftWidth = 30)
    {
        var left = context.VStack(leftBuilder);
        var right = context.VStack(rightBuilder);
        return new SplitterWidget(left, right, leftWidth);
    }

    /// <summary>
    /// Adds a SplitterWidget to the builder.
    /// </summary>
    public static void Splitter<TBuilder>(
        this TBuilder builder,
        Hex1bWidget left,
        Hex1bWidget right,
        int leftWidth = 30)
        where TBuilder : IChildBuilder
        => builder.Add(new SplitterWidget(left, right, leftWidth));

    /// <summary>
    /// Adds a SplitterWidget with VStack builders to the parent builder.
    /// </summary>
    public static void Splitter<TState>(
        this VStackBuilder<TState> builder,
        Action<VStackBuilder<TState>> leftBuilder,
        Action<VStackBuilder<TState>> rightBuilder,
        int leftWidth = 30,
        SizeHint? sizeHint = null)
    {
        var ctx = builder.Context;
        var left = ctx.VStack(leftBuilder);
        var right = ctx.VStack(rightBuilder);
        builder.Add(new SplitterWidget(left, right, leftWidth), sizeHint ?? SizeHint.Fill);
    }
}
