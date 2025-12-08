using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for TextBlockNode rendering using Hex1bTerminal.
/// </summary>
public class TextBlockNodeTests
{
    #region Measurement Tests

    [Fact]
    public void Measure_ReturnsCorrectSize()
    {
        var node = new TextBlockNode { Text = "Hello World" };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(11, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_EmptyText_ReturnsZeroWidth()
    {
        var node = new TextBlockNode { Text = "" };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_RespectsMaxWidthConstraint()
    {
        var node = new TextBlockNode { Text = "This is a very long text that exceeds constraints" };

        var size = node.Measure(new Constraints(0, 10, 0, 5));

        Assert.Equal(10, size.Width);
    }

    [Fact]
    public void Measure_RespectsMinWidthConstraint()
    {
        var node = new TextBlockNode { Text = "Hi" };

        var size = node.Measure(new Constraints(10, 20, 0, 5));

        Assert.Equal(10, size.Width);
    }

    #endregion

    #region Rendering Tests

    [Fact]
    public void Render_WritesTextToTerminal()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = new Hex1bRenderContext(terminal);
        var node = new TextBlockNode { Text = "Hello World" };

        node.Render(context);

        Assert.Equal("Hello World", terminal.GetLineTrimmed(0));
    }

    [Fact]
    public void Render_EmptyText_WritesNothing()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = new Hex1bRenderContext(terminal);
        var node = new TextBlockNode { Text = "" };

        node.Render(context);

        Assert.Equal("", terminal.GetLineTrimmed(0));
    }

    [Fact]
    public void Render_SpecialCharacters_RendersCorrectly()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = new Hex1bRenderContext(terminal);
        var node = new TextBlockNode { Text = "Hello → World ← Test" };

        node.Render(context);

        Assert.Equal("Hello → World ← Test", terminal.GetLineTrimmed(0));
    }

    [Fact]
    public void Render_InNarrowTerminal_TextIsTruncatedByTerminalWidth()
    {
        // Terminal is only 10 chars wide - text will wrap/truncate at terminal boundary
        using var terminal = new Hex1bTerminal(10, 5);
        var context = new Hex1bRenderContext(terminal);
        var node = new TextBlockNode { Text = "This is a long text" };

        node.Render(context);

        // The first line should contain the first 10 characters
        Assert.Equal("This is a ", terminal.GetLine(0));
        // The rest wraps to the next line (terminal behavior, not widget)
        Assert.Equal("long text", terminal.GetLineTrimmed(1));
    }

    [Fact]
    public void Render_AtSpecificPosition_WritesAtCursorPosition()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = new Hex1bRenderContext(terminal);
        var node = new TextBlockNode { Text = "Positioned" };

        context.SetCursorPosition(5, 3);
        node.Render(context);

        // Check that text appears at the right position
        var line = terminal.GetLine(3);
        Assert.Equal("     Positioned", line.TrimEnd());
    }

    [Fact]
    public void Render_VeryLongText_WrapsAtTerminalEdge()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        var context = new Hex1bRenderContext(terminal);
        var longText = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var node = new TextBlockNode { Text = longText };

        node.Render(context);

        // First 20 chars on line 0
        Assert.Equal("ABCDEFGHIJKLMNOPQRST", terminal.GetLine(0));
        // Remaining chars on line 1
        Assert.Equal("UVWXYZ", terminal.GetLineTrimmed(1));
    }

    #endregion

    #region Layout Tests

    [Fact]
    public void Arrange_SetsBounds()
    {
        var node = new TextBlockNode { Text = "Test" };
        var bounds = new Rect(5, 10, 20, 1);

        node.Arrange(bounds);

        Assert.Equal(bounds, node.Bounds);
    }

    #endregion

    #region Focus and Input Tests

    [Fact]
    public void IsFocusable_ReturnsFalse()
    {
        var node = new TextBlockNode { Text = "Test" };

        Assert.False(node.IsFocusable);
    }

    [Fact]
    public void HandleInput_AlwaysReturnsFalse()
    {
        var node = new TextBlockNode { Text = "Test" };

        var handled = node.HandleInput(new KeyInputEvent(ConsoleKey.A, 'a', false, false, false));

        Assert.False(handled);
    }

    #endregion

    #region Integration Tests with Hex1bApp

    [Fact]
    public async Task Integration_TextBlockWidget_RendersViaHex1bApp()
    {
        using var terminal = new Hex1bTerminal(80, 24);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(ctx.Text("Integration Test")),
            terminal
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("Integration Test"));
    }

    [Fact]
    public async Task Integration_MultipleTextBlocks_InVStack_RenderOnSeparateLines()
    {
        using var terminal = new Hex1bTerminal(80, 24);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("First Line"),
                    v.Text("Second Line"),
                    v.Text("Third Line")
                ])
            ),
            terminal
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("First Line"));
        Assert.True(terminal.ContainsText("Second Line"));
        Assert.True(terminal.ContainsText("Third Line"));

        // Verify they appear at different positions
        var firstPositions = terminal.FindText("First Line");
        var secondPositions = terminal.FindText("Second Line");
        var thirdPositions = terminal.FindText("Third Line");

        Assert.Single(firstPositions);
        Assert.Single(secondPositions);
        Assert.Single(thirdPositions);

        // Each should be on a different line
        Assert.NotEqual(firstPositions[0].Line, secondPositions[0].Line);
        Assert.NotEqual(secondPositions[0].Line, thirdPositions[0].Line);
    }

    [Fact]
    public async Task Integration_TextBlock_WithStateChange_UpdatesOnReRender()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var counter = 0;

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) =>
            {
                counter++;
                return Task.FromResult<Hex1bWidget>(
                    ctx.VStack(v => [
                        v.Text($"Counter: {counter}"),
                        v.Button("Increment", () => { /* counter increments on next render */ })
                    ])
                );
            },
            terminal
        );

        // Press Enter to trigger button (causes re-render)
        terminal.SendKey(ConsoleKey.Enter, '\r');
        terminal.CompleteInput();

        await app.RunAsync();

        // After button press and re-render, counter should be 2
        Assert.True(terminal.ContainsText("Counter: 2"));
    }

    [Fact]
    public async Task Integration_TextBlock_InNarrowTerminal_RendersCorrectly()
    {
        // Very narrow terminal - 15 chars wide
        using var terminal = new Hex1bTerminal(15, 10);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Short"),
                    v.Text("A longer text here")
                ])
            ),
            terminal
        );

        terminal.CompleteInput();
        await app.RunAsync();

        // "Short" should fit on its line
        Assert.True(terminal.ContainsText("Short"));
        // Long text will wrap at terminal edge
        Assert.True(terminal.ContainsText("A longer text h"));
    }

    [Fact]
    public async Task Integration_TextBlock_WithDynamicState_ShowsCurrentValue()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var message = "Hello from State";

        using var app = new Hex1bApp<string>(
            message,
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.Text(s => s)
            ),
            terminal
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("Hello from State"));
    }

    [Fact]
    public async Task Integration_TextBlock_EmptyString_DoesNotCrash()
    {
        using var terminal = new Hex1bTerminal(80, 24);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(ctx.Text("")),
            terminal
        );

        terminal.CompleteInput();
        await app.RunAsync();

        // Should complete without error
        Assert.False(terminal.InAlternateScreen);
    }

    [Fact]
    public async Task Integration_TextBlock_UnicodeContent_RendersCorrectly()
    {
        using var terminal = new Hex1bTerminal(80, 24);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(ctx.Text("日本語テスト 🎉 émojis")),
            terminal
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("日本語テスト"));
        Assert.True(terminal.ContainsText("🎉"));
    }

    #endregion
}
