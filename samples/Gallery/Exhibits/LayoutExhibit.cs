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

            // Long paragraph text for wrapping demonstration
            var wrappingText = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur.";

            var technicalText = """
                ╔══════════════════════════════════════════════════════════════════════════════════════════════════╗
                ║  TECHNICAL SPECIFICATIONS - SYSTEM ARCHITECTURE OVERVIEW - VERSION 2.4.1                         ║
                ╠══════════════════════════════════════════════════════════════════════════════════════════════════╣
                ║  Component: Terminal Rendering Engine                                                             ║
                ║  Status: Active | Memory: 256MB | Threads: 4 | Uptime: 99.97%                                    ║
                ╚══════════════════════════════════════════════════════════════════════════════════════════════════╝
                """;

            var widget = ctx.Splitter(
                ctx.Layout(
                    ctx.Panel(leftPanel => [
                        leftPanel.VStack(left => [
                            left.Text("═══ Left Panel (Wrapping) ═══"),
                            left.Text(""),
                            left.Text("TextOverflow.Wrap:", TextOverflow.Wrap),
                            left.Text("─────────────────────────────"),
                            left.Text(wrappingText, TextOverflow.Wrap),
                            left.Text(""),
                            left.Text("TextOverflow.Ellipsis:", TextOverflow.Wrap),
                            left.Text("─────────────────────────────"),
                            left.Text(wrappingText, TextOverflow.Ellipsis),
                            left.Text(""),
                            left.Text("TextOverflow.Overflow (default):", TextOverflow.Wrap),
                            left.Text("─────────────────────────────"),
                            left.Text(wrappingText, TextOverflow.Overflow)
                        ])
                    ]),
                    ClipMode.Clip
                ),
                ctx.Layout(
                    ctx.Panel(rightPanel => [
                        rightPanel.VStack(right => [
                            right.Text("═══ Right Panel (Clipped) ═══"),
                            right.Text(""),
                            right.Text("Wide ASCII art with clipping:"),
                            right.Text(""),
                            .. technicalText.Split('\n').Select(line => right.Text(line))
                        ])
                    ]),
                    ClipMode.Clip
                ),
                leftWidth: 40
            );

            return Task.FromResult<Hex1bWidget>(widget);
        };
    }
}
