using Hex1b.Events;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Represents an item in a list widget.
/// </summary>
public record ListItem(string Id, string Text);

/// <summary>
/// State for a list widget, holding the items and current selection.
/// </summary>
public class ListState
{
    public IReadOnlyList<ListItem> Items { get; set; } = [];
    public int SelectedIndex { get; set; } = 0;

    public ListItem? SelectedItem => SelectedIndex >= 0 && SelectedIndex < Items.Count 
        ? Items[SelectedIndex] 
        : null;

    internal void MoveUp()
    {
        if (Items.Count == 0) return;
        SelectedIndex = SelectedIndex <= 0 ? Items.Count - 1 : SelectedIndex - 1;
    }

    internal void MoveDown()
    {
        if (Items.Count == 0) return;
        SelectedIndex = (SelectedIndex + 1) % Items.Count;
    }

    /// <summary>
    /// Sets the selection to a specific index.
    /// </summary>
    public void SetSelection(int index)
    {
        if (Items.Count == 0 || index < 0 || index >= Items.Count) return;
        SelectedIndex = index;
    }
}

public sealed record ListWidget(ListState State) : Hex1bWidget
{
    /// <summary>
    /// Called when the selection changes.
    /// </summary>
    public Func<ListSelectionChangedEventArgs, Task>? OnSelectionChanged { get; init; }

    /// <summary>
    /// Called when an item is activated (Enter or Space key).
    /// </summary>
    public Func<ListItemActivatedEventArgs, Task>? OnItemActivated { get; init; }

    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ListNode ?? new ListNode();
        node.State = State;
        node.SourceWidget = this;
        
        // Set up event handlers
        if (OnSelectionChanged != null)
        {
            node.SelectionChangedAction = ctx =>
            {
                if (State.SelectedItem != null)
                {
                    var args = new ListSelectionChangedEventArgs(this, node, ctx, State.SelectedIndex, State.SelectedItem);
                    return OnSelectionChanged(args);
                }
                return Task.CompletedTask;
            };
        }
        else
        {
            node.SelectionChangedAction = null;
        }

        if (OnItemActivated != null)
        {
            node.ItemActivatedAction = ctx =>
            {
                if (State.SelectedItem != null)
                {
                    var args = new ListItemActivatedEventArgs(this, node, ctx, State.SelectedIndex, State.SelectedItem);
                    return OnItemActivated(args);
                }
                return Task.CompletedTask;
            };
        }
        else
        {
            node.ItemActivatedAction = null;
        }
        
        // Set initial focus if this is a new node (ListNode is always focusable)
        if (context.IsNew)
        {
            node.IsFocused = true;
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(ListNode);
}
