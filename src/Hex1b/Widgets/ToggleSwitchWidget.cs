using Hex1b.Events;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// State for a toggle switch, holding the available options and current selection.
/// </summary>
public class ToggleSwitchState
{
    /// <summary>
    /// The available options for the toggle switch.
    /// </summary>
    public IReadOnlyList<string> Options { get; set; } = [];
    
    /// <summary>
    /// The currently selected option index.
    /// </summary>
    public int SelectedIndex { get; set; } = 0;

    /// <summary>
    /// Gets the currently selected option, or null if no options exist.
    /// </summary>
    public string? SelectedOption => SelectedIndex >= 0 && SelectedIndex < Options.Count 
        ? Options[SelectedIndex] 
        : null;

    /// <summary>
    /// Moves the selection to the previous option (wraps around).
    /// </summary>
    internal void MovePrevious()
    {
        if (Options.Count == 0) return;
        SelectedIndex = SelectedIndex <= 0 ? Options.Count - 1 : SelectedIndex - 1;
    }

    /// <summary>
    /// Moves the selection to the next option (wraps around).
    /// </summary>
    internal void MoveNext()
    {
        if (Options.Count == 0) return;
        SelectedIndex = (SelectedIndex + 1) % Options.Count;
    }

    /// <summary>
    /// Sets the selection to a specific index.
    /// </summary>
    public void SetSelection(int index)
    {
        if (Options.Count == 0 || index < 0 || index >= Options.Count) return;
        SelectedIndex = index;
    }
}

/// <summary>
/// A horizontal toggle switch widget that allows selecting between multiple options.
/// Use arrow keys (left/right) to switch between options when focused.
/// </summary>
public sealed record ToggleSwitchWidget(ToggleSwitchState State) : Hex1bWidget
{
    /// <summary>
    /// Called when the selection changes.
    /// </summary>
    public Func<ToggleSelectionChangedEventArgs, Task>? OnSelectionChanged { get; init; }

    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ToggleSwitchNode ?? new ToggleSwitchNode();
        node.State = State;
        node.SourceWidget = this;
        
        // Set up event handlers
        if (OnSelectionChanged != null)
        {
            node.SelectionChangedAction = ctx =>
            {
                if (State.SelectedOption != null)
                {
                    var args = new ToggleSelectionChangedEventArgs(this, node, ctx, State.SelectedIndex, State.SelectedOption);
                    return OnSelectionChanged(args);
                }
                return Task.CompletedTask;
            };
        }
        else
        {
            node.SelectionChangedAction = null;
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(ToggleSwitchNode);
}
