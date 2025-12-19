namespace Hex1b;

using Hex1b.Events;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating ToggleSwitchWidget.
/// </summary>
public static class ToggleSwitchExtensions
{
    /// <summary>
    /// Creates a ToggleSwitchWidget with the provided state.
    /// </summary>
    public static ToggleSwitchWidget ToggleSwitch<TParent>(
        this WidgetContext<TParent> ctx,
        ToggleSwitchState state)
        where TParent : Hex1bWidget
        => new(state);

    /// <summary>
    /// Creates a ToggleSwitchWidget with inline options.
    /// </summary>
    public static ToggleSwitchWidget ToggleSwitch<TParent>(
        this WidgetContext<TParent> ctx,
        IReadOnlyList<string> options,
        int selectedIndex = 0)
        where TParent : Hex1bWidget
        => new(new ToggleSwitchState
        {
            Options = options,
            SelectedIndex = selectedIndex
        });
}
