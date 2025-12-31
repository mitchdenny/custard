using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for backdrop click-away events.
/// Provides context about where the click occurred and APIs
/// to control popup/overlay behavior.
/// </summary>
public sealed class BackdropClickedEventArgs
{
    /// <summary>
    /// The backdrop widget that was clicked.
    /// </summary>
    public BackdropWidget Widget { get; }
    
    /// <summary>
    /// The X coordinate of the click (absolute screen position).
    /// </summary>
    public int X { get; }
    
    /// <summary>
    /// The Y coordinate of the click (absolute screen position).
    /// </summary>
    public int Y { get; }
    
    /// <summary>
    /// An optional identifier for this backdrop/popup layer.
    /// Used by PopupStack to identify which layer was clicked.
    /// </summary>
    public string? LayerId { get; }
    
    /// <summary>
    /// Gets or sets whether this click-away event has been handled.
    /// If true, no further default action will be taken.
    /// </summary>
    public bool Handled { get; set; }

    internal BackdropClickedEventArgs(BackdropWidget widget, int x, int y, string? layerId)
    {
        Widget = widget;
        X = x;
        Y = y;
        LayerId = layerId;
    }
}
