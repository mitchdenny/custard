namespace Hex1b;

using Hex1b.Input;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating ButtonWidget.
/// </summary>
public static class ButtonExtensions
{
    /// <summary>
    /// Creates a ButtonWidget with the specified label and no action.
    /// </summary>
    public static ButtonWidget Button<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        string label)
        where TParent : Hex1bWidget
        => new(label);

    /// <summary>
    /// Creates a ButtonWidget with the specified label and synchronous click handler.
    /// </summary>
    public static ButtonWidget Button<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        string label,
        Action<ActionContext> onClick)
        where TParent : Hex1bWidget
        => new(label) { OnClick = ctx => { onClick(ctx); return Task.CompletedTask; } };

    /// <summary>
    /// Creates a ButtonWidget with the specified label and asynchronous click handler.
    /// </summary>
    public static ButtonWidget Button<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        string label,
        Func<ActionContext, Task> onClick)
        where TParent : Hex1bWidget
        => new(label) { OnClick = onClick };
}
