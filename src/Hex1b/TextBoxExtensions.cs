namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating TextBoxWidget.
/// </summary>
public static class TextBoxExtensions
{
    /// <summary>
    /// Creates a TextBox with default empty text.
    /// </summary>
    public static TextBoxWidget TextBox<TParent>(
        this WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
        => new();

    /// <summary>
    /// Creates a TextBox with the specified text.
    /// </summary>
    public static TextBoxWidget TextBox<TParent>(
        this WidgetContext<TParent> ctx,
        string text)
        where TParent : Hex1bWidget
        => new(text);
}
