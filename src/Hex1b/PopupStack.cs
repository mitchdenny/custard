using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Internal record representing a popup entry with optional anchor information.
/// </summary>
internal sealed record PopupEntry(
    Func<Hex1bWidget> ContentBuilder,
    Hex1bNode? AnchorNode = null,
    AnchorPosition Position = AnchorPosition.Below
);

/// <summary>
/// Manages a stack of popups for menu-like overlay behavior.
/// Each popup has a transparent backdrop - clicking the backdrop pops that layer.
/// </summary>
/// <remarks>
/// <para>
/// PopupStack is designed for cascading menus, dropdowns, and similar UX patterns where:
/// - Multiple popups can be stacked (e.g., File → Recent Items → filename)
/// - Each popup layer has its own transparent backdrop
/// - Clicking a backdrop dismisses that layer (and propagates to layers below)
/// </para>
/// <para>
/// For modal dialogs where clicking the backdrop should NOT dismiss, use Backdrop directly
/// without an OnClickAway handler.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Simple popup (full-screen backdrop)
/// e.Popups.Push(() => BuildDialog());
/// 
/// // Anchored popup (positioned relative to triggering element)
/// e.Popups.PushAnchored(AnchorPosition.Below, () => BuildMenu());
/// </code>
/// </example>
public sealed class PopupStack
{
    private readonly List<PopupEntry> _entries = [];
    
    /// <summary>
    /// Gets whether any popups are currently open.
    /// </summary>
    public bool HasPopups => _entries.Count > 0;
    
    /// <summary>
    /// Gets the number of popups currently open.
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Pushes a new popup onto the stack (full-screen backdrop, not anchored).
    /// </summary>
    /// <param name="contentBuilder">A function that builds the widget content for the popup.</param>
    public void Push(Func<Hex1bWidget> contentBuilder)
    {
        _entries.Add(new PopupEntry(contentBuilder));
    }
    
    /// <summary>
    /// Pushes a new popup onto the stack with static content (full-screen backdrop, not anchored).
    /// </summary>
    /// <param name="content">The widget content for the popup.</param>
    public void Push(Hex1bWidget content)
    {
        Push(() => content);
    }
    
    /// <summary>
    /// Pushes an anchored popup positioned relative to a specific node.
    /// </summary>
    /// <param name="anchorNode">The node to anchor the popup to.</param>
    /// <param name="position">Where to position the popup relative to the anchor.</param>
    /// <param name="contentBuilder">A function that builds the widget content for the popup.</param>
    public void PushAnchored(Hex1bNode anchorNode, AnchorPosition position, Func<Hex1bWidget> contentBuilder)
    {
        _entries.Add(new PopupEntry(contentBuilder, anchorNode, position));
    }
    
    /// <summary>
    /// Pushes an anchored popup positioned relative to a specific node.
    /// </summary>
    /// <param name="anchorNode">The node to anchor the popup to.</param>
    /// <param name="position">Where to position the popup relative to the anchor.</param>
    /// <param name="content">The widget content for the popup.</param>
    public void PushAnchored(Hex1bNode anchorNode, AnchorPosition position, Hex1bWidget content)
    {
        PushAnchored(anchorNode, position, () => content);
    }

    /// <summary>
    /// Removes the topmost popup from the stack.
    /// </summary>
    /// <returns>True if a popup was removed, false if stack was empty.</returns>
    public bool Pop()
    {
        if (_entries.Count == 0) return false;
        _entries.RemoveAt(_entries.Count - 1);
        return true;
    }

    /// <summary>
    /// Clears all popups from the stack.
    /// </summary>
    public void Clear()
    {
        _entries.Clear();
    }

    /// <summary>
    /// Builds the ZStack widgets for all popups in the stack.
    /// Each popup is wrapped in a transparent Backdrop that calls Pop() when clicked.
    /// Anchored popups are positioned relative to their anchor node.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context (typically from a ZStack).</param>
    /// <returns>An enumerable of backdrop widgets for the popup stack.</returns>
    public IEnumerable<Hex1bWidget> BuildWidgets<TParent>(WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
    {
        foreach (var entry in _entries)
        {
            var content = entry.ContentBuilder();
            
            // If anchored, wrap content in AnchoredWidget for positioning
            if (entry.AnchorNode != null)
            {
                content = new AnchoredWidget(content, entry.AnchorNode, entry.Position);
            }
            
            yield return ctx.Backdrop(content)
                .Transparent()
                .OnClickAway(() => Pop());
        }
    }
    
    /// <summary>
    /// Builds popup widgets wrapped in backdrops for internal use by the reconciler.
    /// Each popup is wrapped in a transparent Backdrop that calls Pop() when clicked.
    /// Anchored popups are positioned relative to their anchor node.
    /// </summary>
    /// <returns>An enumerable of backdrop-wrapped popup widgets.</returns>
    internal IEnumerable<Hex1bWidget> BuildPopupWidgets()
    {
        foreach (var entry in _entries)
        {
            var content = entry.ContentBuilder();
            
            // If anchored, wrap content in AnchoredWidget for positioning
            if (entry.AnchorNode != null)
            {
                content = new AnchoredWidget(content, entry.AnchorNode, entry.Position);
            }
            
            yield return new BackdropWidget(content)
            {
                Style = BackdropStyle.Transparent,
                ClickAwayHandler = () => { Pop(); return Task.CompletedTask; }
            };
        }
    }
}
