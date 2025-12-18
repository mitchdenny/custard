using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for list selection change events.
/// </summary>
public sealed class ListSelectionChangedEventArgs : WidgetEventArgs<ListWidget, ListNode>
{
    /// <summary>
    /// The index of the newly selected item.
    /// </summary>
    public int SelectedIndex { get; }

    /// <summary>
    /// The newly selected item.
    /// </summary>
    public ListItem SelectedItem { get; }

    public ListSelectionChangedEventArgs(
        ListWidget widget,
        ListNode node,
        InputBindingActionContext context,
        int selectedIndex,
        ListItem selectedItem)
        : base(widget, node, context)
    {
        SelectedIndex = selectedIndex;
        SelectedItem = selectedItem;
    }
}

/// <summary>
/// Event arguments for list item activation events (Enter/Space key).
/// </summary>
public sealed class ListItemActivatedEventArgs : WidgetEventArgs<ListWidget, ListNode>
{
    /// <summary>
    /// The index of the activated item.
    /// </summary>
    public int ActivatedIndex { get; }

    /// <summary>
    /// The activated item.
    /// </summary>
    public ListItem ActivatedItem { get; }

    public ListItemActivatedEventArgs(
        ListWidget widget,
        ListNode node,
        InputBindingActionContext context,
        int activatedIndex,
        ListItem activatedItem)
        : base(widget, node, context)
    {
        ActivatedIndex = activatedIndex;
        ActivatedItem = activatedItem;
    }
}
