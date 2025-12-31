using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Defines how a ZStack's content is clipped.
/// </summary>
public abstract record ClipScope
{
    /// <summary>
    /// Content is clipped to the parent's bounds. This is the default.
    /// </summary>
    public static ClipScope Parent { get; } = new ParentClipScope();
    
    /// <summary>
    /// Content is not clipped - can render to the full screen/terminal bounds.
    /// </summary>
    public static ClipScope Screen { get; } = new ScreenClipScope();
    
    /// <summary>
    /// Content is clipped to a specific widget's bounds.
    /// </summary>
    /// <param name="widget">The widget whose bounds define the clip region.</param>
    public static ClipScope Widget(Hex1bWidget widget) => new WidgetClipScope(widget);
    
    /// <summary>
    /// Resolves this clip scope to actual bounds during arrange.
    /// </summary>
    /// <param name="zstackBounds">The ZStack's own bounds.</param>
    /// <param name="parentClipRect">The parent's clip rectangle.</param>
    /// <param name="screenBounds">The full screen bounds.</param>
    /// <param name="widgetNodeResolver">A function to resolve widgets to their nodes.</param>
    internal abstract Rect Resolve(
        Rect zstackBounds,
        Rect? parentClipRect,
        Rect screenBounds,
        Func<Hex1bWidget, Hex1bNode?>? widgetNodeResolver);
}

/// <summary>
/// Clips to the parent's bounds.
/// </summary>
internal sealed record ParentClipScope : ClipScope
{
    internal override Rect Resolve(
        Rect zstackBounds,
        Rect? parentClipRect,
        Rect screenBounds,
        Func<Hex1bWidget, Hex1bNode?>? widgetNodeResolver)
    {
        // Use parent's clip rect if available, otherwise use our own bounds
        return parentClipRect ?? zstackBounds;
    }
}

/// <summary>
/// No clipping - full screen bounds.
/// </summary>
internal sealed record ScreenClipScope : ClipScope
{
    internal override Rect Resolve(
        Rect zstackBounds,
        Rect? parentClipRect,
        Rect screenBounds,
        Func<Hex1bWidget, Hex1bNode?>? widgetNodeResolver)
    {
        return screenBounds;
    }
}

/// <summary>
/// Clips to a specific widget's bounds.
/// </summary>
internal sealed record WidgetClipScope(Hex1bWidget TargetWidget) : ClipScope
{
    internal override Rect Resolve(
        Rect zstackBounds,
        Rect? parentClipRect,
        Rect screenBounds,
        Func<Hex1bWidget, Hex1bNode?>? widgetNodeResolver)
    {
        if (widgetNodeResolver == null)
        {
            // No resolver available - fall back to own bounds
            return zstackBounds;
        }
        
        var targetNode = widgetNodeResolver(TargetWidget);
        if (targetNode == null)
        {
            // Target widget not found - fall back to own bounds
            return zstackBounds;
        }
        
        return targetNode.Bounds;
    }
}
