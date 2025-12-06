using Custard.Theming;
using Custard.Widgets;

namespace Custard;

public sealed class ListNode : CustardNode
{
    public ListState State { get; set; } = new();
    public bool IsFocused { get; set; } = false;

    public override bool IsFocusable => true;

    public override void Render(CustardRenderContext context)
    {
        var theme = CustardThemes.Current;
        var selectedIndicator = theme.Get(ListTheme.SelectedIndicator);
        var unselectedIndicator = theme.Get(ListTheme.UnselectedIndicator);
        var selectedFg = theme.Get(ListTheme.SelectedForegroundColor);
        var selectedBg = theme.Get(ListTheme.SelectedBackgroundColor);
        
        var items = State.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var isSelected = i == State.SelectedIndex;

            if (isSelected && IsFocused)
            {
                // Focused and selected: use theme colors
                context.Write($"{selectedFg.ToForegroundAnsi()}{selectedBg.ToBackgroundAnsi()}{selectedIndicator}{item.Text}\x1b[0m");
            }
            else if (isSelected)
            {
                // Selected but not focused: just show indicator
                context.Write($"{selectedIndicator}{item.Text}");
            }
            else
            {
                // Not selected
                context.Write($"{unselectedIndicator}{item.Text}");
            }

            if (i < items.Count - 1)
            {
                context.Write("\n");
            }
        }
    }

    public override bool HandleInput(CustardInputEvent evt)
    {
        if (!IsFocused) return false;

        if (evt is KeyInputEvent keyEvent)
        {
            switch (keyEvent.Key)
            {
                case ConsoleKey.UpArrow:
                    State.MoveUp();
                    return true;
                case ConsoleKey.DownArrow:
                    State.MoveDown();
                    return true;
                case ConsoleKey.Enter:
                    // Trigger selection changed on Enter as well
                    if (State.SelectedItem != null)
                    {
                        State.OnSelectionChanged?.Invoke(State.SelectedItem);
                    }
                    return true;
            }
        }
        return false;
    }
}
