using Hex1b.Layout;

namespace Hex1b.Widgets;

/// <summary>
/// A layout widget that provides clipping and rendering assistance to its children.
/// Children that are aware of layout can query whether characters should be rendered.
/// </summary>
public sealed record LayoutWidget(Hex1bWidget Child, ClipMode ClipMode = ClipMode.Clip) : Hex1bWidget;

/// <summary>
/// Determines how content that exceeds bounds is handled.
/// </summary>
public enum ClipMode
{
    /// <summary>
    /// Content that exceeds bounds is not rendered.
    /// </summary>
    Clip,
    
    /// <summary>
    /// Content is allowed to overflow (no clipping).
    /// </summary>
    Overflow,
    
    // Future: Wrap, Scroll, Ellipsis, etc.
}
