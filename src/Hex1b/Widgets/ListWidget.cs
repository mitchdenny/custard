using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Represents an item in a list widget.
/// </summary>
public record ListItem(string Id, string Text);

/// <summary>
/// A list widget with selectable items. Holds its own selection state.
/// </summary>
public class ListState
{
    public IReadOnlyList<ListItem> Items { get; set; } = [];
    public int SelectedIndex { get; set; } = 0;
    public Action<ListItem>? OnSelectionChanged { get; set; }
    
    /// <summary>
    /// Called when an item is activated (Space or Enter key).
    /// </summary>
    public Action<ListItem>? OnItemActivated { get; set; }

    public ListItem? SelectedItem => SelectedIndex >= 0 && SelectedIndex < Items.Count 
        ? Items[SelectedIndex] 
        : null;

    public void MoveUp()
    {
        if (Items.Count == 0) return;
        SelectedIndex = SelectedIndex <= 0 ? Items.Count - 1 : SelectedIndex - 1;
        OnSelectionChanged?.Invoke(Items[SelectedIndex]);
    }

    public void MoveDown()
    {
        if (Items.Count == 0) return;
        SelectedIndex = (SelectedIndex + 1) % Items.Count;
        OnSelectionChanged?.Invoke(Items[SelectedIndex]);
    }
}

public sealed record ListWidget(ListState State) : Hex1bWidget
{
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ListNode ?? new ListNode();
        node.State = State;
        
        // Set initial focus if this is a new node (ListNode is always focusable)
        if (context.IsNew)
        {
            node.IsFocused = true;
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(ListNode);
}
