namespace Hex1b.Widgets;

/// <summary>
/// Specifies alignment within a container. Can be combined using flags.
/// </summary>
[Flags]
public enum Alignment
{
    /// <summary>
    /// No alignment specified (defaults to top-left).
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Align to the left edge horizontally.
    /// </summary>
    Left = 1 << 0,
    
    /// <summary>
    /// Align to the right edge horizontally.
    /// </summary>
    Right = 1 << 1,
    
    /// <summary>
    /// Center horizontally.
    /// </summary>
    HCenter = 1 << 2,
    
    /// <summary>
    /// Align to the top edge vertically.
    /// </summary>
    Top = 1 << 3,
    
    /// <summary>
    /// Align to the bottom edge vertically.
    /// </summary>
    Bottom = 1 << 4,
    
    /// <summary>
    /// Center vertically.
    /// </summary>
    VCenter = 1 << 5,
    
    /// <summary>
    /// Center both horizontally and vertically.
    /// </summary>
    Center = HCenter | VCenter,
    
    /// <summary>
    /// Align to the top-left corner.
    /// </summary>
    TopLeft = Top | Left,
    
    /// <summary>
    /// Align to the top-right corner.
    /// </summary>
    TopRight = Top | Right,
    
    /// <summary>
    /// Align to the bottom-left corner.
    /// </summary>
    BottomLeft = Bottom | Left,
    
    /// <summary>
    /// Align to the bottom-right corner.
    /// </summary>
    BottomRight = Bottom | Right,
    
    /// <summary>
    /// Center horizontally at the top.
    /// </summary>
    TopCenter = Top | HCenter,
    
    /// <summary>
    /// Center horizontally at the bottom.
    /// </summary>
    BottomCenter = Bottom | HCenter,
    
    /// <summary>
    /// Center vertically on the left.
    /// </summary>
    LeftCenter = Left | VCenter,
    
    /// <summary>
    /// Center vertically on the right.
    /// </summary>
    RightCenter = Right | VCenter
}
