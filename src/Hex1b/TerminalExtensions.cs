using Hex1b.Terminal;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating TerminalWidget.
/// </summary>
public static class TerminalExtensions
{
    /// <summary>
    /// Creates a TerminalWidget that displays an embedded terminal.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="terminal">The embedded terminal to display.</param>
    /// <returns>A TerminalWidget displaying the embedded terminal.</returns>
    public static TerminalWidget Terminal<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bTerminal terminal)
        where TParent : Hex1bWidget
        => new(terminal);
}
