using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the default CTRL-C binding behavior, specifically when there are no focusable widgets.
/// </summary>
public class DefaultCtrlCBindingTests
{
    [Fact]
    public async Task DefaultCtrlCBinding_WithNoFocusableWidgets_ExitsApp()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        using var cts = new CancellationTokenSource();
        
        // Create app with only non-focusable widgets (Border with empty VStack)
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new BorderWidget(
                    new VStackWidget(Array.Empty<Hex1bWidget>()),
                    "Test"
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        var runTask = app.RunAsync(cts.Token);
        
        // Wait for initial render
        await Task.Delay(50);
        
        // Send CTRL-C - this should trigger the default binding and exit
        terminal.SendKey(ConsoleKey.C, '\x03', control: true);
        
        // Wait a bit for the app to process the input and exit
        await Task.Delay(100);
        
        // The app should have exited by now (not due to cancellation)
        Assert.True(runTask.IsCompleted, "App should have exited after CTRL-C");
        
        // Clean up - cancel if still running
        if (!runTask.IsCompleted)
        {
            cts.Cancel();
        }
        await runTask;
    }

    [Fact]
    public async Task DefaultCtrlCBinding_WithOnlyTextBlock_ExitsApp()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        using var cts = new CancellationTokenSource();
        
        // Create app with only TextBlock (non-focusable)
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TextBlockWidget("Press CTRL-C to exit")),
            new Hex1bAppOptions { Terminal = terminal }
        );

        var runTask = app.RunAsync(cts.Token);
        
        // Wait for initial render
        await Task.Delay(50);
        
        // Send CTRL-C
        terminal.SendKey(ConsoleKey.C, '\x03', control: true);
        
        // Wait for the app to process
        await Task.Delay(100);
        
        // The app should have exited
        Assert.True(runTask.IsCompleted, "App should have exited after CTRL-C");
        
        // Clean up
        if (!runTask.IsCompleted)
        {
            cts.Cancel();
        }
        await runTask;
    }

    [Fact]
    public async Task DefaultCtrlCBinding_WithFocusableWidget_StillWorks()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        using var cts = new CancellationTokenSource();
        
        // Create app with a focusable button
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new ButtonWidget("Test Button")
                })
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        var runTask = app.RunAsync(cts.Token);
        
        // Wait for initial render
        await Task.Delay(50);
        
        // Send CTRL-C
        terminal.SendKey(ConsoleKey.C, '\x03', control: true);
        
        // Wait for the app to process
        await Task.Delay(100);
        
        // The app should have exited
        Assert.True(runTask.IsCompleted, "App should have exited after CTRL-C even with focusable widgets");
        
        // Clean up
        if (!runTask.IsCompleted)
        {
            cts.Cancel();
        }
        await runTask;
    }
}
