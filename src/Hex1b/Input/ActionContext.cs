namespace Hex1b.Input;

/// <summary>
/// Context passed to input binding action handlers.
/// Provides app-level services for focus navigation and other common operations.
/// </summary>
public sealed class ActionContext
{
    private readonly FocusRing _focusRing;

    internal ActionContext(FocusRing focusRing)
    {
        _focusRing = focusRing;
    }

    /// <summary>
    /// Moves focus to the next focusable widget in the ring.
    /// This is the standard way to implement Tab navigation.
    /// </summary>
    /// <returns>True if focus was moved, false if there are no focusables.</returns>
    public bool FocusNext() => _focusRing.FocusNext();

    /// <summary>
    /// Moves focus to the previous focusable widget in the ring.
    /// This is the standard way to implement Shift+Tab navigation.
    /// </summary>
    /// <returns>True if focus was moved, false if there are no focusables.</returns>
    public bool FocusPrevious() => _focusRing.FocusPrevious();

    /// <summary>
    /// Gets the currently focused node, or null if none.
    /// </summary>
    public Hex1bNode? FocusedNode => _focusRing.FocusedNode;

    /// <summary>
    /// Gets all focusable nodes in render order.
    /// </summary>
    public IReadOnlyList<Hex1bNode> Focusables => _focusRing.Focusables;

    /// <summary>
    /// Focuses a specific node if it's in the ring.
    /// </summary>
    /// <returns>True if the node was found and focused.</returns>
    public bool Focus(Hex1bNode node) => _focusRing.Focus(node);
}
