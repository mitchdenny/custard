using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Gallery.Exhibits;

/// <summary>
/// An exhibit for exploring clipping and wrapping behavior of child widgets
/// when there is not enough horizontal or vertical space.
/// </summary>
public class LayoutExhibit(ILogger<LayoutExhibit> logger) : Hex1bExhibit
{
    private readonly ILogger<LayoutExhibit> _logger = logger;

    public override string Id => "layout";
    public override string Title => "Layout";
    public override string Description => "Explore clipping and wrapping behavior when space is constrained.";

    /// <summary>
    /// State for the layout exhibit.
    /// </summary>
    private class LayoutState
    {
    }

    public override Func<CancellationToken, Task<Hex1bWidget>> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating layout exhibit widget builder");

        var state = new LayoutState();

        return ct =>
        {
            var ctx = new RootContext<LayoutState>(state);

            // Large text content to demonstrate clipping/wrapping scenarios
            var loremIpsum = """
                Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.
                Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.
                Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur.
                Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.
                
                Curabitur pretium tincidunt lacus. Nulla gravida orci a odio. Nullam varius, turpis et commodo pharetra.
                Est eros bibendum elit, nec luctus magna felis sollicitudin mauris. Integer in mauris eu nibh euismod gravida.
                Duis ac tellus et risus vulputate vehicula. Donec lobortis risus a elit. Etiam tempor.
                
                Ut ullamcorper, ligula eu tempor congue, eros est euismod turpis, id tincidunt sapien risus a quam.
                Maecenas fermentum consequat mi. Donec fermentum. Pellentesque malesuada nulla a mi.
                Duis sapien sem, aliquet sed, vulputate eget, feugiat non, leo. Cras volutpat.
                """;

            var technicalText = """
                ╔══════════════════════════════════════════════════════════════════════════════════════════════════╗
                ║  TECHNICAL SPECIFICATIONS - SYSTEM ARCHITECTURE OVERVIEW - VERSION 2.4.1                         ║
                ╠══════════════════════════════════════════════════════════════════════════════════════════════════╣
                ║  Component: Terminal Rendering Engine                                                             ║
                ║  Status: Active | Memory: 256MB | Threads: 4 | Uptime: 99.97%                                    ║
                ╠══════════════════════════════════════════════════════════════════════════════════════════════════╣
                ║                                                                                                   ║
                ║  ┌─────────────────────────────────────────────────────────────────────────────────────────────┐ ║
                ║  │  Rendering Pipeline:                                                                         │ ║
                ║  │    1. Widget Tree Construction → 2. Reconciliation → 3. Measure → 4. Arrange → 5. Render   │ ║
                ║  │                                                                                              │ ║
                ║  │  Current Frame Stats:                                                                        │ ║
                ║  │    - Widgets: 47 | Nodes: 42 | Dirty: 3 | Cached: 39                                        │ ║
                ║  │    - Render Time: 2.3ms | Layout Time: 0.8ms | Total: 3.1ms                                 │ ║
                ║  └─────────────────────────────────────────────────────────────────────────────────────────────┘ ║
                ║                                                                                                   ║
                ║  Supported Features: ANSI Colors, Box Drawing, Unicode, Alt Screen Buffer                        ║
                ║  Warning: This text is intentionally very wide to test horizontal clipping behavior!             ║
                ╚══════════════════════════════════════════════════════════════════════════════════════════════════╝
                """;

            var widget = ctx.Splitter(
                ctx.Panel(leftPanel => [
                    leftPanel.VStack(left => [
                        left.Text("═══ Left Panel ═══"),
                        left.Text(""),
                        left.Text("This panel contains a large block of"),
                        left.Text("text that may need to be clipped or"),
                        left.Text("wrapped depending on available space."),
                        left.Text(""),
                        left.Text("─── Lorem Ipsum ───"),
                        .. loremIpsum.Split('\n').Select(line => left.Text(line))
                    ])
                ]),
                ctx.Panel(rightPanel => [
                    rightPanel.VStack(right => [
                        right.Text("═══ Right Panel ═══"),
                        right.Text(""),
                        right.Text("This panel contains wide technical text"),
                        right.Text("that tests horizontal clipping behavior."),
                        right.Text(""),
                        right.Text("─── Technical Specs ───"),
                        .. technicalText.Split('\n').Select(line => right.Text(line))
                    ])
                ]),
                leftWidth: 40
            );

            return Task.FromResult<Hex1bWidget>(widget);
        };
    }
}
