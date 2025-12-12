namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building LayoutWidget.
/// </summary>
public static class LayoutExtensions
{
    /// <summary>
    /// Creates a Layout that wraps a single child widget with clipping enabled.
    /// </summary>
    public static LayoutWidget Layout<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Hex1bWidget child,
        ClipMode clipMode = ClipMode.Clip)
        where TParent : Hex1bWidget
        => new(child, clipMode);

    /// <summary>
    /// Creates a Layout with a VStack child.
    /// </summary>
    public static LayoutWidget Layout<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Func<WidgetContext<VStackWidget, TState>, Hex1bWidget[]> builder,
        ClipMode clipMode = ClipMode.Clip)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<VStackWidget, TState>(ctx.State);
        var children = builder(childCtx);
        return new LayoutWidget(new VStackWidget(children), clipMode);
    }

    /// <summary>
    /// Wraps an existing widget in a Layout with clipping.
    /// </summary>
    public static LayoutWidget WithClipping<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Hex1bWidget child)
        where TParent : Hex1bWidget
        => new(child, ClipMode.Clip);
}
