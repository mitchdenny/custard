namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building SplitterWidget.
/// </summary>
public static class SplitterExtensions
{
    /// <summary>
    /// Creates a Splitter with left and right child widgets.
    /// </summary>
    public static SplitterWidget Splitter<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Hex1bWidget left,
        Hex1bWidget right,
        int leftWidth = 30)
        where TParent : Hex1bWidget
        => new(left, right, leftWidth);

    /// <summary>
    /// Creates a Splitter where both panes are VStacks built from callbacks.
    /// </summary>
    public static SplitterWidget Splitter<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Func<WidgetContext<VStackWidget, TState>, Hex1bWidget[]> leftBuilder,
        Func<WidgetContext<VStackWidget, TState>, Hex1bWidget[]> rightBuilder,
        int leftWidth = 30)
        where TParent : Hex1bWidget
    {
        var leftCtx = new WidgetContext<VStackWidget, TState>(ctx.State);
        var rightCtx = new WidgetContext<VStackWidget, TState>(ctx.State);
        return new SplitterWidget(
            new VStackWidget(leftBuilder(leftCtx)),
            new VStackWidget(rightBuilder(rightCtx)),
            leftWidth);
    }

    /// <summary>
    /// Creates a Splitter with narrowed state for child panes.
    /// </summary>
    public static SplitterWidget Splitter<TParent, TState, TLeftState, TRightState>(
        this WidgetContext<TParent, TState> ctx,
        TLeftState leftState,
        Func<WidgetContext<VStackWidget, TLeftState>, Hex1bWidget[]> leftBuilder,
        TRightState rightState,
        Func<WidgetContext<VStackWidget, TRightState>, Hex1bWidget[]> rightBuilder,
        int leftWidth = 30)
        where TParent : Hex1bWidget
    {
        var leftCtx = new WidgetContext<VStackWidget, TLeftState>(leftState);
        var rightCtx = new WidgetContext<VStackWidget, TRightState>(rightState);
        return new SplitterWidget(
            new VStackWidget(leftBuilder(leftCtx)),
            new VStackWidget(rightBuilder(rightCtx)),
            leftWidth);
    }
}
