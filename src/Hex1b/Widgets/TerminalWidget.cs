using Hex1b.Nodes;
using Hex1b.Terminal;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that displays the output of an embedded Hex1bTerminal.
/// Allows a Hex1bApp to host other terminal experiences (like tmux).
/// </summary>
/// <param name="Terminal">The embedded terminal to display.</param>
/// <remarks>
/// <para>
/// This widget renders the screen buffer of an embedded Hex1bTerminal,
/// allowing you to create nested terminal experiences. The terminal
/// should be connected via a Hex1bAppPresentationAdapter.
/// </para>
/// <para>
/// For the first pass, input is not forwarded to the embedded terminal,
/// but this can be added in future versions.
/// </para>
/// </remarks>
public sealed record TerminalWidget(Hex1bTerminal Terminal) : Hex1bWidget
{
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TerminalNode ?? new TerminalNode();
        
        // Mark dirty if terminal changed
        if (node.Terminal != Terminal)
        {
            node.MarkDirty();
        }
        
        node.Terminal = Terminal;
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(TerminalNode);
}
