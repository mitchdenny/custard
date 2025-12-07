namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building Responsive widgets.
/// The Responsive widget displays the first child whose condition evaluates to true
/// based on the available space from parent constraints.
/// </summary>
public static class ResponsiveExtensions
{
    /// <summary>
    /// Creates a conditional widget that wraps content with a size-based condition.
    /// The condition receives (availableWidth, availableHeight) from the parent's layout constraints.
    /// Use inside a Responsive() builder to create conditional branches.
    /// </summary>
    public static ConditionalWidget When<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Func<int, int, bool> condition,
        Func<WidgetContext<ConditionalWidget, TState>, Hex1bWidget> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<ConditionalWidget, TState>(ctx.State);
        var content = builder(childCtx);
        return new ConditionalWidget(condition, content);
    }

    /// <summary>
    /// Creates a conditional widget with a width-only condition.
    /// Convenience overload for common width-based responsive layouts.
    /// </summary>
    public static ConditionalWidget WhenWidth<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Func<int, bool> widthCondition,
        Func<WidgetContext<ConditionalWidget, TState>, Hex1bWidget> builder)
        where TParent : Hex1bWidget
    {
        return ctx.When((w, h) => widthCondition(w), builder);
    }

    /// <summary>
    /// Creates a conditional widget with a minimum width requirement.
    /// The content is displayed when availableWidth >= minWidth.
    /// </summary>
    public static ConditionalWidget WhenMinWidth<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        int minWidth,
        Func<WidgetContext<ConditionalWidget, TState>, Hex1bWidget> builder)
        where TParent : Hex1bWidget
    {
        return ctx.When((w, h) => w >= minWidth, builder);
    }

    /// <summary>
    /// Creates a conditional widget that always matches.
    /// Use as the last branch in a Responsive() to provide a fallback.
    /// </summary>
    public static ConditionalWidget Otherwise<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Func<WidgetContext<ConditionalWidget, TState>, Hex1bWidget> builder)
        where TParent : Hex1bWidget
    {
        return ctx.When((w, h) => true, builder);
    }

    /// <summary>
    /// Creates a Responsive widget that displays the first child whose condition evaluates to true.
    /// Conditions receive (availableWidth, availableHeight) from the parent's layout constraints.
    /// Use collection expression syntax with When()/WhenMinWidth()/Otherwise() to define conditional branches.
    /// Example: ctx.Responsive(r => [r.WhenMinWidth(100, r => r.Text("Wide")), r.Otherwise(r => r.Text("Narrow"))])
    /// </summary>
    public static ResponsiveWidget Responsive<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Func<WidgetContext<ResponsiveWidget, TState>, ConditionalWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<ResponsiveWidget, TState>(ctx.State);
        var branches = builder(childCtx);
        return new ResponsiveWidget(branches);
    }

    /// <summary>
    /// Creates a Responsive widget with narrowed state.
    /// First argument is the child state, enabling progressive state narrowing.
    /// </summary>
    public static ResponsiveWidget Responsive<TParent, TState, TChildState>(
        this WidgetContext<TParent, TState> ctx,
        TChildState childState,
        Func<WidgetContext<ResponsiveWidget, TChildState>, ConditionalWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<ResponsiveWidget, TChildState>(childState);
        var branches = builder(childCtx);
        return new ResponsiveWidget(branches);
    }
}
