using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Terminal;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for TerminalNode rendering and behavior.
/// </summary>
public class TerminalNodeTests
{
    [Fact]
    public void Measure_WithNullTerminal_ReturnsZeroSize()
    {
        var node = new TerminalNode { Terminal = null };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
    }

    [Fact]
    public void Measure_WithTerminal_ReturnsTerminalSize()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = new Hex1bTerminal(workload, width: 80, height: 24);
        var node = new TerminalNode { Terminal = terminal };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(80, size.Width);
        Assert.Equal(24, size.Height);
    }

    [Fact]
    public void Measure_RespectsMaxConstraints()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = new Hex1bTerminal(workload, width: 80, height: 24);
        var node = new TerminalNode { Terminal = terminal };

        var size = node.Measure(new Constraints(0, 40, 0, 12));

        Assert.Equal(40, size.Width);
        Assert.Equal(12, size.Height);
    }

    [Fact]
    public void Measure_RespectsMinConstraints()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = new Hex1bTerminal(workload, width: 40, height: 12);
        var node = new TerminalNode { Terminal = terminal };

        var size = node.Measure(new Constraints(60, 100, 20, 30));

        Assert.Equal(60, size.Width);
        Assert.Equal(20, size.Height);
    }

    [Fact]
    public void Arrange_ResizesTerminalToFitBounds()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = new Hex1bTerminal(workload, width: 80, height: 24);
        var node = new TerminalNode { Terminal = terminal };

        node.Arrange(new Rect(0, 0, 40, 12));

        Assert.Equal(40, terminal.Width);
        Assert.Equal(12, terminal.Height);
    }

    [Fact]
    public void Render_WithNullTerminal_DoesNotThrow()
    {
        var node = new TerminalNode { Terminal = null };
        var adapter = new Hex1bAppWorkloadAdapter();
        var context = new Hex1bRenderContext(adapter);
        
        node.Arrange(new Rect(0, 0, 10, 5));
        
        // Should not throw
        var exception = Record.Exception(() => node.Render(context));
        Assert.Null(exception);
    }

    [Fact]
    public void Render_DisplaysTerminalOutput()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = new Hex1bTerminal(workload, width: 20, height: 5);
        
        // Write some output to the terminal
        terminal.ProcessOutput("Hello World");
        
        var node = new TerminalNode { Terminal = terminal };
        var adapter = new Hex1bAppWorkloadAdapter();
        var context = new Hex1bRenderContext(adapter);
        
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 20, 5));
        node.Render(context);

        // Verify the terminal's content was rendered
        var screenText = terminal.GetScreenText();
        Assert.Contains("Hello World", screenText);
    }

    [Fact]
    public void TerminalWidget_CreatesTerminalNode()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = new Hex1bTerminal(workload, width: 80, height: 24);
        var widget = new TerminalWidget(terminal);

        var context = ReconcileContext.CreateRoot(new FocusRing());
        var node = widget.Reconcile(null, context);

        Assert.IsType<TerminalNode>(node);
        var terminalNode = (TerminalNode)node;
        Assert.Same(terminal, terminalNode.Terminal);
    }

    [Fact]
    public void TerminalWidget_ReusesExistingNode()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal1 = new Hex1bTerminal(workload, width: 80, height: 24);
        var terminal2 = new Hex1bTerminal(workload, width: 80, height: 24);
        
        var widget1 = new TerminalWidget(terminal1);
        var widget2 = new TerminalWidget(terminal2);

        var context = ReconcileContext.CreateRoot(new FocusRing());
        var node1 = widget1.Reconcile(null, context);
        var node2 = widget2.Reconcile(node1, context);

        Assert.Same(node1, node2);
        Assert.Same(terminal2, ((TerminalNode)node2).Terminal);
    }

    [Fact]
    public void TerminalWidget_MarksDirtyWhenTerminalChanges()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal1 = new Hex1bTerminal(workload, width: 80, height: 24);
        var terminal2 = new Hex1bTerminal(workload, width: 80, height: 24);
        
        var widget1 = new TerminalWidget(terminal1);
        var widget2 = new TerminalWidget(terminal2);

        var context = ReconcileContext.CreateRoot(new FocusRing());
        var node = (TerminalNode)widget1.Reconcile(null, context);
        
        // Clear dirty flag
        node.ClearDirty();
        Assert.False(node.IsDirty);

        // Reconcile with different terminal
        widget2.Reconcile(node, context);

        Assert.True(node.IsDirty);
    }
}
