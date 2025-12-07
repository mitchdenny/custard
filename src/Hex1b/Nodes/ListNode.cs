using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

public sealed class ListNode : Hex1bNode
{
    public ListState State { get; set; } = new();
    public bool IsFocused { get; set; } = false;

    public override bool IsFocusable => true;

    public override Size Measure(Constraints constraints)
    {
        // List: width is max item length + indicator (2 chars), height is item count
        var items = State.Items;
        var maxWidth = 0;
        foreach (var item in items)
        {
            maxWidth = Math.Max(maxWidth, item.Text.Length + 2); // "> " indicator
        }
        var height = Math.Max(items.Count, 1);
        return constraints.Constrain(new Size(maxWidth, height));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var selectedIndicator = theme.Get(ListTheme.SelectedIndicator);
        var unselectedIndicator = theme.Get(ListTheme.UnselectedIndicator);
        var selectedFg = theme.Get(ListTheme.SelectedForegroundColor);
        var selectedBg = theme.Get(ListTheme.SelectedBackgroundColor);
        
        // Get inherited colors for non-selected items
        var inheritedColors = context.GetInheritedColorCodes();
        var resetToInherited = context.GetResetToInheritedCodes();
        
        var items = State.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var isSelected = i == State.SelectedIndex;

            // Position cursor for this row
            context.SetCursorPosition(Bounds.X, Bounds.Y + i);

            if (isSelected && IsFocused)
            {
                // Focused and selected: use theme colors
                context.Write($"{selectedFg.ToForegroundAnsi()}{selectedBg.ToBackgroundAnsi()}{selectedIndicator}{item.Text}{resetToInherited}");
            }
            else if (isSelected)
            {
                // Selected but not focused: just show indicator with inherited colors
                context.Write($"{inheritedColors}{selectedIndicator}{item.Text}{resetToInherited}");
            }
            else
            {
                // Not selected: use inherited colors
                context.Write($"{inheritedColors}{unselectedIndicator}{item.Text}{resetToInherited}");
            }
        }
    }

    public override bool HandleInput(Hex1bInputEvent evt)
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
                case ConsoleKey.Spacebar:
                    // Trigger item activated on Enter or Space
                    if (State.SelectedItem != null)
                    {
                        State.OnItemActivated?.Invoke(State.SelectedItem);
                    }
                    return true;
            }
        }
        return false;
    }
}
