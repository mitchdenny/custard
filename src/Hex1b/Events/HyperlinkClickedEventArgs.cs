using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for hyperlink click events.
/// </summary>
public sealed class HyperlinkClickedEventArgs : WidgetEventArgs<HyperlinkWidget, HyperlinkNode>
{
    public HyperlinkClickedEventArgs(HyperlinkWidget widget, HyperlinkNode node, InputBindingActionContext context)
        : base(widget, node, context)
    {
    }

    /// <summary>
    /// The URI of the hyperlink that was clicked.
    /// </summary>
    public string Uri => Widget.Uri;

    /// <summary>
    /// The visible text of the hyperlink that was clicked.
    /// </summary>
    public string Text => Widget.Text;
}
