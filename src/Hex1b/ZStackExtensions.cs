namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating <see cref="ZStackWidget"/>.
/// </summary>
public static class ZStackExtensions
{
    /// <summary>
    /// Creates a ZStack that layers children on the Z-axis (depth).
    /// Children are rendered in order, with later children appearing on top of earlier ones.
    /// All children share the same bounds (the ZStack's bounds).
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="children">A function that returns the child widgets. First = bottom, last = top.</param>
    /// <returns>A new ZStackWidget.</returns>
    /// <example>
    /// <code>
    /// ctx.ZStack(z => [
    ///     // Base layer (bottom)
    ///     z.VStack(v => [
    ///         v.Text("Main content here")
    ///     ]),
    ///     
    ///     // Overlay layer (top) - renders on top of the base layer
    ///     z.Border(b => [
    ///         b.Text("Floating panel!")
    ///     ]).Align(Alignment.TopRight)
    /// ])
    /// </code>
    /// </example>
    public static ZStackWidget ZStack<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<ZStackWidget>, IEnumerable<Hex1bWidget?>> children)
        where TParent : Hex1bWidget
    {
        var childContext = new WidgetContext<ZStackWidget>();
        var childWidgets = children(childContext)
            .Where(c => c != null)
            .Cast<Hex1bWidget>()
            .ToList();
        return new ZStackWidget(childWidgets);
    }
}
