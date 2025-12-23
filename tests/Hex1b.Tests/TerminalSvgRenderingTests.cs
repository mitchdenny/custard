using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Terminal.Testing;
using Hex1b.Theming;

namespace Hex1b.Tests;

/// <summary>
/// Tests for SVG rendering of terminal snapshots and regions.
/// SVG outputs are attached to test results for visual inspection.
/// </summary>
public class TerminalSvgRenderingTests
{
    [Fact]
    public async Task RenderFullSnapshot_ProducesSvg()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);

        var theme = new Hex1bTheme("Test")
            .Set(PanelTheme.BackgroundColor, Hex1bColor.FromRgb(30, 30, 60))
            .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(0, 100, 200))
            .Set(ButtonTheme.ForegroundColor, Hex1bColor.FromRgb(255, 255, 255));

        using var app = new Hex1bApp(
            ctx => ctx.Panel(p => [
                p.VStack(v => [
                    v.Text("Terminal SVG Rendering Demo"),
                    v.Text(""),
                    v.Button("Click Me"),
                    v.Text(""),
                    v.Text("Status: Ready")
                ])
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Ready"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();

        // Assert
        Assert.NotEmpty(svg);
        Assert.Contains("<svg", svg);
        Assert.Contains("</svg>", svg);
        Assert.Contains("<text", svg); // Should have text elements
        Assert.Contains("<rect", svg); // Should have background rectangles

        // Attach SVG to test output
        AttachSvg("full-snapshot.svg", svg);
    }

    [Fact]
    public async Task RenderArbitraryRegion_ProducesSvg()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 15);

        var theme = new Hex1bTheme("Test")
            .Set(PanelTheme.BackgroundColor, Hex1bColor.FromRgb(20, 40, 60));

        using var app = new Hex1bApp(
            ctx => ctx.Panel(p => [
                p.VStack(v => [
                    v.Text("Line 1: Header"),
                    v.Text("Line 2: Content A"),
                    v.Text("Line 3: Content B"),
                    v.Text("Line 4: Content C"),
                    v.Text("Line 5: Footer")
                ])
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Footer"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();

        // Extract an arbitrary region (columns 5-35, rows 1-4)
        var region = snapshot.GetRegion(new Rect(5, 1, 30, 4));
        var svg = region.ToSvg();

        // Assert
        Assert.NotEmpty(svg);
        Assert.Contains("<svg", svg);
        Assert.Contains("</svg>", svg);
        // The region should have text elements for the content
        Assert.Contains("<text", svg);

        // Attach SVG to test output
        AttachSvg("arbitrary-region.svg", svg);

        // Also render full snapshot for comparison
        var fullSvg = snapshot.ToSvg();
        AttachSvg("arbitrary-region-full.svg", fullSvg);
    }

    [Fact]
    public async Task RenderButtonControl_ProducesSvg()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 12);

        var theme = new Hex1bTheme("Test")
            .Set(PanelTheme.BackgroundColor, Hex1bColor.FromRgb(25, 25, 35))
            .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(70, 130, 180))
            .Set(ButtonTheme.ForegroundColor, Hex1bColor.FromRgb(255, 255, 255));

        using var app = new Hex1bApp(
            ctx => ctx.Panel(p => [
                p.VStack(v => [
                    v.Text("Button Demo"),
                    v.Text(""),
                    v.Button("Submit Form"),
                    v.Text(""),
                    v.Text("Press Enter to submit")
                ])
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Submit Form"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();

        // Find the button by searching for its text and extracting a region around it
        var buttonLocations = snapshot.FindText("Submit Form");
        Assert.NotEmpty(buttonLocations);
        var (line, column) = buttonLocations[0];
        // Button format is "[ Submit Form ]" so expand the region
        var buttonRegion = snapshot.GetRegion(new Rect(column - 2, line, 18, 1));
        var buttonSvg = buttonRegion.ToSvg();

        // Assert
        Assert.NotEmpty(buttonSvg);
        Assert.Contains("<svg", buttonSvg);
        Assert.Contains("</svg>", buttonSvg);
        Assert.Contains("<text", buttonSvg); // Should have text elements

        // Attach SVGs to test output
        AttachSvg("button-control.svg", buttonSvg);
        AttachSvg("button-control-full.svg", snapshot.ToSvg());
    }

    [Fact]
    public async Task RenderWithCursor_ShowsCursorPosition()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 8);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Cursor Demo"),
                v.TextBox("Type here...")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Type here"), TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.Tab) // Focus on textbox
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();

        // Assert - cursor should be rendered
        Assert.NotEmpty(svg);
        Assert.Contains("cursor", svg);

        // Attach SVG to test output
        AttachSvg("cursor-demo.svg", svg);
    }

    [Fact]
    public async Task RenderWithCustomOptions_AppliesSettings()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 30, 5);

        using var app = new Hex1bApp(
            ctx => ctx.Text("Custom Options Test"),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Custom"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();

        var options = new TerminalSvgOptions
        {
            FontFamily = "'JetBrains Mono', monospace",
            FontSize = 16,
            CellWidth = 10,
            CellHeight = 20,
            DefaultBackground = "#282c34",
            DefaultForeground = "#abb2bf",
            CursorColor = "#61afef"
        };

        var svg = snapshot.ToSvg(options);

        // Assert
        Assert.Contains("JetBrains Mono", svg);
        Assert.Contains("font-size: 16px", svg);
        Assert.Contains("#282c34", svg);

        // Attach SVG to test output
        AttachSvg("custom-options.svg", svg);
    }

    /// <summary>
    /// Attaches an SVG string to the test context as a proper test attachment.
    /// In xUnit v3, attachments are recorded in test results XML/JSON.
    /// Also writes to disk for easy viewing.
    /// </summary>
    private static void AttachSvg(string name, string svg)
    {
        // Use xUnit v3's native test attachment API
        TestContext.Current.AddAttachment(name, svg);

        // Build path: TestResults/svg/{TestClass}/{TestName}/{attachment}
        var testContext = TestContext.Current;
        var testClass = testContext.Test?.TestCase?.TestClassName ?? "UnknownClass";
        var testMethodName = testContext.Test?.TestCase?.TestMethod?.MethodName ?? "UnknownMethod";
        
        // Get test display name which includes theory parameters
        var testDisplayName = testContext.Test?.TestDisplayName ?? testMethodName;
        
        // Extract just the method part with parameters if it's a theory
        var methodWithParams = testDisplayName;
        if (methodWithParams.Contains('.'))
        {
            methodWithParams = methodWithParams[(methodWithParams.LastIndexOf('.') + 1)..];
        }

        // Sanitize for filesystem
        var sanitizedClass = SanitizeFileName(testClass);
        var sanitizedMethod = SanitizeFileName(methodWithParams);

        // Build output path
        var assemblyDir = Path.GetDirectoryName(typeof(TerminalSvgRenderingTests).Assembly.Location)!;
        var outputDir = Path.Combine(assemblyDir, "TestResults", "svg", sanitizedClass, sanitizedMethod);
        Directory.CreateDirectory(outputDir);
        
        var filePath = Path.Combine(outputDir, name);
        File.WriteAllText(filePath, svg);
    }

    /// <summary>
    /// Sanitizes a string for use as a file or directory name.
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var result = new System.Text.StringBuilder(name.Length);
        
        foreach (var c in name)
        {
            if (Array.IndexOf(invalidChars, c) >= 0)
            {
                result.Append('_');
            }
            else if (c == '"')
            {
                result.Append('\'');
            }
            else
            {
                result.Append(c);
            }
        }
        
        // Truncate if too long (some filesystems have limits)
        var sanitized = result.ToString();
        if (sanitized.Length > 200)
        {
            sanitized = sanitized[..200];
        }
        
        return sanitized;
    }
}
