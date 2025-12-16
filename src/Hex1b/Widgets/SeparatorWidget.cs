using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// The axis along which content is laid out.
/// </summary>
public enum LayoutAxis
{
    /// <summary>
    /// Content is laid out horizontally (HStack direction).
    /// </summary>
    Horizontal,
    
    /// <summary>
    /// Content is laid out vertically (VStack direction).
    /// </summary>
    Vertical
}

/// <summary>
/// A separator widget that draws a horizontal or vertical line.
/// When placed in a VStack, it draws a horizontal line.
/// When placed in an HStack, it draws a vertical line.
/// The axis can also be set explicitly.
/// </summary>
public sealed record SeparatorWidget : Hex1bWidget
{
    /// <summary>
    /// The character to use for horizontal separators.
    /// </summary>
    public char HorizontalChar { get; init; } = '─';
    
    /// <summary>
    /// The character to use for vertical separators.
    /// </summary>
    public char VerticalChar { get; init; } = '│';
    
    /// <summary>
    /// Optional explicit axis. If null, the axis is inferred from the parent container.
    /// </summary>
    public LayoutAxis? ExplicitAxis { get; init; }

    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as SeparatorNode ?? new SeparatorNode();
        node.HorizontalChar = HorizontalChar;
        node.VerticalChar = VerticalChar;
        node.ExplicitAxis = ExplicitAxis;
        node.InferredAxis = context.LayoutAxis;
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(SeparatorNode);
}
