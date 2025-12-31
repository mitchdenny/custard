namespace Hex1b.Widgets;

/// <summary>
/// Specifies the preferred position of a popup relative to its anchor widget.
/// </summary>
public enum AnchorPosition
{
    /// <summary>
    /// Position the popup below the anchor, aligned to the anchor's left edge.
    /// Common for dropdown menus.
    /// </summary>
    Below,
    
    /// <summary>
    /// Position the popup above the anchor, aligned to the anchor's left edge.
    /// Useful when there's no room below.
    /// </summary>
    Above,
    
    /// <summary>
    /// Position the popup to the left of the anchor, aligned to the anchor's top edge.
    /// </summary>
    Left,
    
    /// <summary>
    /// Position the popup to the right of the anchor, aligned to the anchor's top edge.
    /// Common for cascading/submenu patterns.
    /// </summary>
    Right
}
