using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for toggle switch selection change events.
/// </summary>
public sealed class ToggleSelectionChangedEventArgs : WidgetEventArgs<ToggleSwitchWidget, ToggleSwitchNode>
{
    /// <summary>
    /// The index of the newly selected option.
    /// </summary>
    public int SelectedIndex { get; }

    /// <summary>
    /// The text of the newly selected option.
    /// </summary>
    public string SelectedOption { get; }

    public ToggleSelectionChangedEventArgs(
        ToggleSwitchWidget widget,
        ToggleSwitchNode node,
        InputBindingActionContext context,
        int selectedIndex,
        string selectedOption)
        : base(widget, node, context)
    {
        SelectedIndex = selectedIndex;
        SelectedOption = selectedOption;
    }
}
