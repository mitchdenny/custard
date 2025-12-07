using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for ResponsiveNode layout, rendering, and condition evaluation.
/// </summary>
public class ResponsiveNodeTests
{
    private static Hex1bRenderContext CreateContext(Hex1bTerminal terminal)
    {
        return new Hex1bRenderContext(terminal);
    }

    [Fact]
    public void Measure_FirstMatchingCondition_ReturnsChildSize()
    {
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new TextBlockWidget("Hidden")),
                new ConditionalWidget((w, h) => true, new TextBlockWidget("Visible"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "Hidden" },
                new TextBlockNode { Text = "Visible" }
            ]
        };

        var size = node.Measure(Constraints.Unbounded);

        // Should measure the "Visible" text (7 chars)
        Assert.Equal(7, size.Width);
        Assert.Equal(1, size.Height);
        Assert.Equal(1, node.ActiveBranchIndex);
    }

    [Fact]
    public void Measure_FirstConditionTrue_SelectsFirst()
    {
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => true, new TextBlockWidget("First")),
                new ConditionalWidget((w, h) => true, new TextBlockWidget("Second"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "First" },
                new TextBlockNode { Text = "Second" }
            ]
        };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(5, size.Width); // "First"
        Assert.Equal(0, node.ActiveBranchIndex);
    }

    [Fact]
    public void Measure_NoMatchingCondition_ReturnsZero()
    {
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new TextBlockWidget("Hidden1")),
                new ConditionalWidget((w, h) => false, new TextBlockWidget("Hidden2"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "Hidden1" },
                new TextBlockNode { Text = "Hidden2" }
            ]
        };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
        Assert.Equal(-1, node.ActiveBranchIndex);
    }

    [Fact]
    public void Measure_EmptyBranches_ReturnsZero()
    {
        var node = new ResponsiveNode
        {
            Branches = [],
            ChildNodes = []
        };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
        Assert.Equal(-1, node.ActiveBranchIndex);
    }

    [Fact]
    public void Measure_RespectsConstraints()
    {
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => true, new TextBlockWidget("This is a long text"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "This is a long text" }
            ]
        };

        var size = node.Measure(new Constraints(0, 10, 0, 5));

        Assert.True(size.Width <= 10);
        Assert.True(size.Height <= 5);
    }

    [Fact]
    public void Arrange_ActiveChildGetsFullBounds()
    {
        var child1 = new TextBlockNode { Text = "Hidden" };
        var child2 = new TextBlockNode { Text = "Visible" };
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new TextBlockWidget("Hidden")),
                new ConditionalWidget((w, h) => true, new TextBlockWidget("Visible"))
            ],
            ChildNodes = [child1, child2]
        };
        var bounds = new Rect(5, 3, 20, 10);

        node.Measure(Constraints.Tight(20, 10));
        node.Arrange(bounds);

        // Only the active child should have bounds set
        Assert.Equal(bounds, child2.Bounds);
        Assert.Equal(bounds, node.Bounds);
    }

    [Fact]
    public void Render_OnlyRendersActiveChild()
    {
        using var terminal = new Hex1bTerminal(30, 5);
        var context = CreateContext(terminal);
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new TextBlockWidget("Hidden")),
                new ConditionalWidget((w, h) => true, new TextBlockWidget("Visible"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "Hidden" },
                new TextBlockNode { Text = "Visible" }
            ]
        };

        node.Measure(Constraints.Tight(30, 5));
        node.Arrange(new Rect(0, 0, 30, 5));
        node.Render(context);

        var screenText = terminal.GetScreenText();
        Assert.Contains("Visible", screenText);
        Assert.DoesNotContain("Hidden", screenText);
    }

    [Fact]
    public void Render_NoMatchingCondition_RendersNothing()
    {
        using var terminal = new Hex1bTerminal(30, 5);
        var context = CreateContext(terminal);
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new TextBlockWidget("Hidden"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "Hidden" }
            ]
        };

        node.Measure(Constraints.Tight(30, 5));
        node.Arrange(new Rect(0, 0, 30, 5));
        node.Render(context);

        var screenText = terminal.GetScreenText();
        Assert.DoesNotContain("Hidden", screenText);
    }

    [Fact]
    public void Measure_ConditionReceivesAvailableSize()
    {
        int receivedWidth = 0;
        int receivedHeight = 0;
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => { receivedWidth = w; receivedHeight = h; return true; }, new TextBlockWidget("Test"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "Test" }
            ]
        };

        node.Measure(new Constraints(0, 100, 0, 50));

        Assert.Equal(100, receivedWidth);
        Assert.Equal(50, receivedHeight);
    }

    [Fact]
    public void Measure_WidthBasedCondition_SelectsCorrectBranch()
    {
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => w >= 100, new TextBlockWidget("Wide")),
                new ConditionalWidget((w, h) => w >= 50, new TextBlockWidget("Medium")),
                new ConditionalWidget((w, h) => true, new TextBlockWidget("Narrow"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "Wide" },
                new TextBlockNode { Text = "Medium" },
                new TextBlockNode { Text = "Narrow" }
            ]
        };

        // Wide layout
        node.Measure(new Constraints(0, 120, 0, 30));
        Assert.Equal(0, node.ActiveBranchIndex);

        // Medium layout
        node.Measure(new Constraints(0, 80, 0, 30));
        Assert.Equal(1, node.ActiveBranchIndex);

        // Narrow layout
        node.Measure(new Constraints(0, 40, 0, 30));
        Assert.Equal(2, node.ActiveBranchIndex);
    }

    [Fact]
    public void GetFocusableNodes_ReturnsOnlyActiveChildFocusables()
    {
        var button1 = new ButtonNode { Label = "Hidden" };
        var button2 = new ButtonNode { Label = "Visible" };
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new ButtonWidget("Hidden", () => { })),
                new ConditionalWidget((w, h) => true, new ButtonWidget("Visible", () => { }))
            ],
            ChildNodes = [button1, button2]
        };

        // Evaluate conditions
        node.Measure(Constraints.Unbounded);

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Single(focusables);
        Assert.Contains(button2, focusables);
        Assert.DoesNotContain(button1, focusables);
    }

    [Fact]
    public void GetFocusableNodes_NoMatchingCondition_ReturnsEmpty()
    {
        var button = new ButtonNode { Label = "Hidden" };
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new ButtonWidget("Hidden", () => { }))
            ],
            ChildNodes = [button]
        };

        node.Measure(Constraints.Unbounded);
        var focusables = node.GetFocusableNodes().ToList();

        Assert.Empty(focusables);
    }

    [Fact]
    public void HandleInput_PassesToActiveChild()
    {
        var clicked = false;
        var button = new ButtonNode
        {
            Label = "Click",
            IsFocused = true,
            OnClick = () => clicked = true
        };
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => true, new ButtonWidget("Click", () => clicked = true))
            ],
            ChildNodes = [button]
        };

        node.Measure(Constraints.Unbounded);
        var handled = node.HandleInput(new KeyInputEvent(ConsoleKey.Enter, '\r', false, false, false));

        Assert.True(handled);
        Assert.True(clicked);
    }

    [Fact]
    public void HandleInput_NoActiveChild_ReturnsFalse()
    {
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new TextBlockWidget("Hidden"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "Hidden" }
            ]
        };

        node.Measure(Constraints.Unbounded);
        var handled = node.HandleInput(new KeyInputEvent(ConsoleKey.A, 'A', false, false, false));

        Assert.False(handled);
    }

    [Fact]
    public void IsFocusable_ReturnsFalse()
    {
        var node = new ResponsiveNode();

        Assert.False(node.IsFocusable);
    }

    [Fact]
    public void ActiveChild_ReturnsCorrectNode()
    {
        var child1 = new TextBlockNode { Text = "First" };
        var child2 = new TextBlockNode { Text = "Second" };
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new TextBlockWidget("First")),
                new ConditionalWidget((w, h) => true, new TextBlockWidget("Second"))
            ],
            ChildNodes = [child1, child2]
        };

        node.Measure(Constraints.Unbounded);

        Assert.Same(child2, node.ActiveChild);
    }

    [Fact]
    public void ActiveChild_NoMatch_ReturnsNull()
    {
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new TextBlockWidget("Hidden"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "Hidden" }
            ]
        };

        node.Measure(Constraints.Unbounded);

        Assert.Null(node.ActiveChild);
    }

    [Fact]
    public void NestedResponsive_WorksCorrectly()
    {
        using var terminal = new Hex1bTerminal(30, 5);
        var context = CreateContext(terminal);

        var innerNode = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => true, new TextBlockWidget("Inner"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "Inner" }
            ]
        };

        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => true, new TextBlockWidget("Outer"))
            ],
            ChildNodes = [innerNode]
        };

        // Override ChildNodes to use the inner responsive
        node.ChildNodes = [innerNode];

        node.Measure(Constraints.Tight(30, 5));
        node.Arrange(new Rect(0, 0, 30, 5));
        node.Render(context);

        Assert.Contains("Inner", terminal.GetScreenText());
    }

    [Fact]
    public void Responsive_WithOtherwiseFallback_ShowsFallback()
    {
        using var terminal = new Hex1bTerminal(30, 5);
        var context = CreateContext(terminal);
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new TextBlockWidget("First")),
                new ConditionalWidget((w, h) => false, new TextBlockWidget("Second")),
                new ConditionalWidget((w, h) => true, new TextBlockWidget("Fallback")) // Otherwise is (w,h) => true
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "First" },
                new TextBlockNode { Text = "Second" },
                new TextBlockNode { Text = "Fallback" }
            ]
        };

        node.Measure(Constraints.Tight(30, 5));
        node.Arrange(new Rect(0, 0, 30, 5));
        node.Render(context);

        Assert.Contains("Fallback", terminal.GetScreenText());
    }
}
