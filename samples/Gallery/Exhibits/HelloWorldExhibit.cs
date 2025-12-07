using Hex1b;
using Hex1b.Fluent;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Gallery.Exhibits;

/// <summary>
/// A simple hello world using the fluent context-based API.
/// </summary>
public class HelloWorldExhibit(ILogger<HelloWorldExhibit> logger) : Hex1bExhibit
{
    private readonly ILogger<HelloWorldExhibit> _logger = logger;

    public override string Id => "hello-world";
    public override string Title => "Hello World";
    public override string Description => "A simple hello world with interactive button.";

    public override string SourceCode => """
        // Define application state (even if empty)
        public class AppState
        {
            public int ClickCount { get; set; }
        }
        
        var state = new AppState();
        
        // Using fluent API with typed state context
        var app = new Hex1bApp<AppState>(state, (ctx, ct) =>
        {
            return Task.FromResult<Hex1bWidget>(
                ctx.VStack(stack =>
                {
                    stack.Text("╔════════════════════════════════════╗");
                    stack.Text("║    Hello, Fluent World!            ║");
                    stack.Text("║    Using the Context-Based API     ║");
                    stack.Text("╚════════════════════════════════════╝");
                    stack.Text("");
                    stack.Text($"Click count: {ctx.State.ClickCount}");
                    stack.Text("");
                    stack.Button("Click me!", () => ctx.State.ClickCount++);
                })
            );
        });
        
        await app.RunAsync();
        """;

    /// <summary>
    /// Simple state for this exhibit.
    /// </summary>
    private class HelloState
    {
        public int ClickCount { get; set; }
    }

    public override Func<CancellationToken, Task<Hex1bWidget>> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating hello world widget builder");

        // Create state once, captured in closure
        var state = new HelloState();

        return ct =>
        {
            // Create root context with the state
            var ctx = new RootContext<HelloState>(state);

            // Build using fluent API
            var widget = ctx.VStack(stack =>
            {
                stack.Text("╔════════════════════════════════════╗");
                stack.Text("║    Hello, Fluent World!            ║");
                stack.Text("║    Using the Context-Based API     ║");
                stack.Text("╚════════════════════════════════════╝");
                stack.Text("");
                stack.Text($"Click count: {ctx.State.ClickCount}");
                stack.Text("");
                stack.Button("Click me!", () => ctx.State.ClickCount++);
            });

            return Task.FromResult<Hex1bWidget>(widget);
        };
    }
}
