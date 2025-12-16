using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// The orientation of a scroll widget.
/// </summary>
public enum ScrollOrientation
{
    /// <summary>
    /// Content scrolls vertically (up/down).
    /// </summary>
    Vertical,
    
    /// <summary>
    /// Content scrolls horizontally (left/right).
    /// </summary>
    Horizontal
}

/// <summary>
/// State for tracking scroll position.
/// </summary>
public class ScrollState
{
    /// <summary>
    /// The current scroll offset (in characters).
    /// For vertical scrolling, this is the row offset.
    /// For horizontal scrolling, this is the column offset.
    /// </summary>
    public int Offset { get; set; }
    
    /// <summary>
    /// The size of the content being scrolled (in characters).
    /// This is set by the ScrollNode during layout.
    /// </summary>
    public int ContentSize { get; internal set; }
    
    /// <summary>
    /// The size of the visible viewport (in characters).
    /// This is set by the ScrollNode during layout.
    /// </summary>
    public int ViewportSize { get; internal set; }
    
    /// <summary>
    /// Whether the scrollbar is currently needed (content exceeds viewport).
    /// </summary>
    public bool IsScrollable => ContentSize > ViewportSize;
    
    /// <summary>
    /// The maximum scroll offset.
    /// </summary>
    public int MaxOffset => Math.Max(0, ContentSize - ViewportSize);
    
    /// <summary>
    /// Scroll up (or left) by the specified amount.
    /// </summary>
    public void ScrollUp(int amount = 1)
    {
        Offset = Math.Max(0, Offset - amount);
    }
    
    /// <summary>
    /// Scroll down (or right) by the specified amount.
    /// </summary>
    public void ScrollDown(int amount = 1)
    {
        Offset = Math.Min(MaxOffset, Offset + amount);
    }
    
    /// <summary>
    /// Scroll to the beginning.
    /// </summary>
    public void ScrollToStart()
    {
        Offset = 0;
    }
    
    /// <summary>
    /// Scroll to the end.
    /// </summary>
    public void ScrollToEnd()
    {
        Offset = MaxOffset;
    }
    
    /// <summary>
    /// Scroll up by a full page (viewport size).
    /// </summary>
    public void PageUp()
    {
        ScrollUp(Math.Max(1, ViewportSize - 1));
    }
    
    /// <summary>
    /// Scroll down by a full page (viewport size).
    /// </summary>
    public void PageDown()
    {
        ScrollDown(Math.Max(1, ViewportSize - 1));
    }
}

/// <summary>
/// A scroll widget that provides scrolling capability for content that exceeds the available space.
/// Only supports one direction at a time (vertical or horizontal).
/// </summary>
public sealed record ScrollWidget : Hex1bWidget
{
    /// <summary>
    /// The child widget to scroll.
    /// </summary>
    public Hex1bWidget Child { get; }
    
    /// <summary>
    /// The scroll state (offset, content size, viewport size).
    /// </summary>
    public ScrollState State { get; }
    
    /// <summary>
    /// The scroll orientation (vertical or horizontal).
    /// </summary>
    public ScrollOrientation Orientation { get; init; }
    
    /// <summary>
    /// Whether to show the scrollbar when content is scrollable.
    /// </summary>
    public bool ShowScrollbar { get; init; }

    /// <summary>
    /// Creates a new ScrollWidget.
    /// </summary>
    /// <param name="child">The child widget to scroll.</param>
    /// <param name="state">The scroll state. If null, a new state is created.</param>
    /// <param name="orientation">The scroll orientation. Defaults to Vertical.</param>
    /// <param name="showScrollbar">Whether to show the scrollbar. Defaults to true.</param>
    public ScrollWidget(
        Hex1bWidget child,
        ScrollState? state = null,
        ScrollOrientation orientation = ScrollOrientation.Vertical,
        bool showScrollbar = true)
    {
        Child = child;
        State = state ?? new ScrollState();
        Orientation = orientation;
        ShowScrollbar = showScrollbar;
    }

    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ScrollNode ?? new ScrollNode();
        node.Child = context.ReconcileChild(node.Child, Child, node);
        node.State = State;
        node.Orientation = Orientation;
        node.ShowScrollbar = ShowScrollbar;
        
        // Invalidate focus cache since children may have changed
        node.InvalidateFocusCache();
        
        // Set initial focus only if this is a new node AND we're at the root or parent doesn't manage focus
        if (context.IsNew && !context.ParentManagesFocus())
        {
            node.SetInitialFocus();
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(ScrollNode);
}
