using Hex1b.Widgets;

namespace Hex1b;

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
/// var popups = new PopupStack();
/// 
/// ctx.ZStack(z => [
///     z.VStack(v => [
///         v.Button("File").OnClick(_ => popups.Push(() => BuildFileMenu(z, popups))),
///     ]),
///     ..popups.BuildWidgets(z)
/// ])
/// </code>
/// </example>
public sealed class PopupStack
{
    private readonly List<Func<Hex1bWidget>> _entries = [];
    
    /// <summary>
    /// Gets whether any popups are currently open.
    /// </summary>
    public bool HasPopups => _entries.Count > 0;
    
    /// <summary>
    /// Gets the number of popups currently open.
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Pushes a new popup onto the stack.
    /// </summary>
    /// <param name="contentBuilder">A function that builds the widget content for the popup.</param>
    public void Push(Func<Hex1bWidget> contentBuilder)
    {
        _entries.Add(contentBuilder);
    }
    
    /// <summary>
    /// Pushes a new popup onto the stack with static content.
    /// </summary>
    /// <param name="content">The widget content for the popup.</param>
    public void Push(Hex1bWidget content)
    {
        Push(() => content);
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
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context (typically from a ZStack).</param>
    /// <returns>An enumerable of backdrop widgets for the popup stack.</returns>
    public IEnumerable<Hex1bWidget> BuildWidgets<TParent>(WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
    {
        foreach (var contentBuilder in _entries)
        {
            var content = contentBuilder();
            
            yield return ctx.Backdrop(content)
                .Transparent()
                .OnClickAway(() => Pop());
        }
    }
    
    /// <summary>
    /// Builds popup widgets wrapped in backdrops for internal use by the reconciler.
    /// Each popup is wrapped in a transparent Backdrop that calls Pop() when clicked.
    /// </summary>
    /// <returns>An enumerable of backdrop-wrapped popup widgets.</returns>
    internal IEnumerable<Hex1bWidget> BuildPopupWidgets()
    {
        foreach (var contentBuilder in _entries)
        {
            var content = contentBuilder();
            
            yield return new BackdropWidget(content)
            {
                Style = BackdropStyle.Transparent,
                ClickAwayHandler = () => { Pop(); return Task.CompletedTask; }
            };
        }
    }
}
