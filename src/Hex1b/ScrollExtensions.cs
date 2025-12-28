namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building ScrollWidget.
/// </summary>
public static class ScrollExtensions
{
    /// <summary>
    /// Creates a vertical scroll widget with the specified child.
    /// </summary>
    public static ScrollWidget VScroll<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget child,
        bool showScrollbar = true)
        where TParent : Hex1bWidget
        => new(child, ScrollOrientation.Vertical, showScrollbar);

    /// <summary>
    /// Creates a vertical scroll widget with a VStack child built from a callback.
    /// </summary>
    public static ScrollWidget VScroll<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<VStackWidget>, Hex1bWidget[]> childBuilder,
        bool showScrollbar = true)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<VStackWidget>();
        return new ScrollWidget(
            new VStackWidget(childBuilder(childCtx)),
            ScrollOrientation.Vertical,
            showScrollbar);
    }

    /// <summary>
    /// Creates a horizontal scroll widget with the specified child.
    /// </summary>
    public static ScrollWidget HScroll<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget child,
        bool showScrollbar = true)
        where TParent : Hex1bWidget
        => new(child, ScrollOrientation.Horizontal, showScrollbar);

    /// <summary>
    /// Creates a horizontal scroll widget with an HStack child built from a callback.
    /// </summary>
    public static ScrollWidget HScroll<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<HStackWidget>, Hex1bWidget[]> childBuilder,
        bool showScrollbar = true)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<HStackWidget>();
        return new ScrollWidget(
            new HStackWidget(childBuilder(childCtx)),
            ScrollOrientation.Horizontal,
            showScrollbar);
    }

    /// <summary>
    /// Creates a scroll widget with the specified orientation.
    /// </summary>
    public static ScrollWidget Scroll<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget child,
        ScrollOrientation orientation,
        bool showScrollbar = true)
        where TParent : Hex1bWidget
        => new(child, orientation, showScrollbar);
}
