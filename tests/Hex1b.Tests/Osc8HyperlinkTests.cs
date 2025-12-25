using Hex1b;
using Hex1b.Input;
using Hex1b.Terminal;
using Hex1b.Terminal.Testing;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for OSC 8 hyperlink support, including both low-level terminal parsing
/// and high-level HyperlinkWidget integration with Hex1bApp.
/// </summary>
public class Osc8HyperlinkTests
{
    #region Low-Level OSC 8 Parsing Tests

    [Fact]
    public void ProcessOutput_WithOsc8Sequence_CreatesHyperlinkData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // OSC 8 format: ESC ] 8 ; params ; URI ST
        // ST can be ESC \ or BEL (\x07)
        terminal.ProcessOutput("\x1b]8;;https://example.com\x1b\\");
        terminal.ProcessOutput("Link Text");
        terminal.ProcessOutput("\x1b]8;;\x1b\\"); // End hyperlink
        
        // Should track the hyperlink data
        Assert.Equal(1, terminal.TrackedHyperlinkCount);
        Assert.True(terminal.ContainsHyperlinkData());
        
        // The cells with "Link Text" should have the hyperlink data
        var hyperlinkData = terminal.GetHyperlinkDataAt(0, 0);
        Assert.NotNull(hyperlinkData);
        Assert.Equal("https://example.com", hyperlinkData.Uri);
        Assert.Equal("", hyperlinkData.Parameters);
    }

    [Fact]
    public void ProcessOutput_WithOsc8UsingBel_CreatesHyperlinkData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // OSC 8 with BEL terminator instead of ESC \
        terminal.ProcessOutput("\x1b]8;;https://example.org\x07");
        terminal.ProcessOutput("Click here");
        terminal.ProcessOutput("\x1b]8;;\x07"); // End hyperlink
        
        Assert.Equal(1, terminal.TrackedHyperlinkCount);
        
        var hyperlinkData = terminal.GetHyperlinkDataAt(0, 0);
        Assert.NotNull(hyperlinkData);
        Assert.Equal("https://example.org", hyperlinkData.Uri);
    }

    [Fact]
    public void ProcessOutput_WithOsc8WithParameters_StoresParameters()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // OSC 8 with parameters (e.g., id=unique-id)
        terminal.ProcessOutput("\x1b]8;id=test123;https://example.com/path\x1b\\");
        terminal.ProcessOutput("Link");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        var hyperlinkData = terminal.GetHyperlinkDataAt(0, 0);
        Assert.NotNull(hyperlinkData);
        Assert.Equal("https://example.com/path", hyperlinkData.Uri);
        Assert.Equal("id=test123", hyperlinkData.Parameters);
    }

    [Fact]
    public void ProcessOutput_EndOsc8_ReleasesHyperlink()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Start hyperlink
        terminal.ProcessOutput("\x1b]8;;https://example.com\x1b\\");
        terminal.ProcessOutput("Link");
        
        // Verify hyperlink exists
        Assert.Equal(1, terminal.TrackedHyperlinkCount);
        var linkData1 = terminal.GetHyperlinkDataAt(0, 0);
        Assert.NotNull(linkData1);
        
        // End hyperlink
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        // Write more text without hyperlink
        terminal.ProcessOutput(" Plain");
        
        // The plain text should not have hyperlink
        var linkData2 = terminal.GetHyperlinkDataAt(5, 0);
        Assert.Null(linkData2);
        
        // But the original link text should still have it
        var linkData3 = terminal.GetHyperlinkDataAt(0, 0);
        Assert.NotNull(linkData3);
    }

    [Fact]
    public void TrackedHyperlink_WhenCellOverwritten_ReleasesReference()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Create hyperlink
        terminal.ProcessOutput("\x1b]8;;https://example.com\x1b\\");
        terminal.ProcessOutput("Link");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        Assert.Equal(1, terminal.TrackedHyperlinkCount);
        
        // Overwrite the cells
        terminal.ProcessOutput("\x1b[1;1HXXXXXXXX");
        
        // Hyperlink data should be released (refcount reached 0)
        Assert.Equal(0, terminal.TrackedHyperlinkCount);
    }

    [Fact]
    public void TrackedHyperlink_Deduplication_ReusesSameObject()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Create same hyperlink twice
        terminal.ProcessOutput("\x1b]8;;https://example.com\x1b\\");
        terminal.ProcessOutput("First");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        terminal.ProcessOutput(" ");
        
        terminal.ProcessOutput("\x1b]8;;https://example.com\x1b\\");
        terminal.ProcessOutput("Second");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        // Should still only have one unique tracked object
        Assert.Equal(1, terminal.TrackedHyperlinkCount);
        
        // Both link texts should reference the same object
        var link1 = terminal.GetHyperlinkDataAt(0, 0);
        var link2 = terminal.GetHyperlinkDataAt(6, 0);
        Assert.Same(link1, link2);
    }

    [Fact]
    public void TrackedHyperlink_RefCount_IncreasesWithDeduplication()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Create same hyperlink with multiple characters
        terminal.ProcessOutput("\x1b]8;;https://example.com\x1b\\");
        terminal.ProcessOutput("Link");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        var trackedLink = terminal.GetTrackedHyperlinkAt(0, 0);
        Assert.NotNull(trackedLink);
        
        // RefCount should be 4 (one for each character: L, i, n, k)
        Assert.Equal(4, trackedLink.RefCount);
    }

    [Fact]
    public void TrackedHyperlink_DifferentParameters_CreatesSeparateObjects()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Same URI but different parameters should create different objects
        terminal.ProcessOutput("\x1b]8;id=1;https://example.com\x1b\\");
        terminal.ProcessOutput("A");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        terminal.ProcessOutput("\x1b]8;id=2;https://example.com\x1b\\");
        terminal.ProcessOutput("B");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        // Should have two unique tracked objects
        Assert.Equal(2, terminal.TrackedHyperlinkCount);
        
        var link1 = terminal.GetHyperlinkDataAt(0, 0);
        var link2 = terminal.GetHyperlinkDataAt(1, 0);
        Assert.NotSame(link1, link2);
        Assert.Equal("id=1", link1!.Parameters);
        Assert.Equal("id=2", link2!.Parameters);
    }

    [Fact]
    public void ProcessOutput_MultilineHyperlink_TracksAcrossRows()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Start hyperlink and write across multiple lines
        terminal.ProcessOutput("\x1b]8;;https://example.com\x1b\\");
        terminal.ProcessOutput("Line 1\n");
        terminal.ProcessOutput("Line 2");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        // Both lines should have the hyperlink
        var link1 = terminal.GetHyperlinkDataAt(0, 0);
        var link2 = terminal.GetHyperlinkDataAt(0, 1);
        
        Assert.NotNull(link1);
        Assert.NotNull(link2);
        Assert.Same(link1, link2);
        Assert.Equal("https://example.com", link1.Uri);
    }

    [Fact]
    public void ProcessOutput_NestedHyperlinks_ReplacesWithNewLink()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Start first hyperlink
        terminal.ProcessOutput("\x1b]8;;https://first.com\x1b\\");
        terminal.ProcessOutput("A");
        
        // Start second hyperlink without closing first (should replace)
        terminal.ProcessOutput("\x1b]8;;https://second.com\x1b\\");
        terminal.ProcessOutput("B");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        // First character should have first link
        var link1 = terminal.GetHyperlinkDataAt(0, 0);
        Assert.NotNull(link1);
        Assert.Equal("https://first.com", link1.Uri);
        
        // Second character should have second link
        var link2 = terminal.GetHyperlinkDataAt(1, 0);
        Assert.NotNull(link2);
        Assert.Equal("https://second.com", link2.Uri);
        
        // Should have two tracked objects
        Assert.Equal(2, terminal.TrackedHyperlinkCount);
    }

    [Fact]
    public void WorkloadAdapter_WithOsc8_TerminalReceivesHyperlinkData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Write through workload adapter
        workload.Write("\x1b]8;;https://example.com\x1b\\");
        workload.Write("Link");
        workload.Write("\x1b]8;;\x1b\\");
        
        // Flush should process it
        terminal.FlushOutput();
        
        Assert.Equal(1, terminal.TrackedHyperlinkCount);
        Assert.True(terminal.ContainsHyperlinkData());
        
        var linkData = terminal.GetHyperlinkDataAt(0, 0);
        Assert.NotNull(linkData);
        Assert.Equal("https://example.com", linkData.Uri);
    }

    [Fact]
    public void ProcessOutput_ComplexUri_PreservesAllCharacters()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Complex URI with query parameters, hash, etc.
        var uri = "https://example.com/path?foo=bar&baz=qux#section";
        terminal.ProcessOutput($"\x1b]8;;{uri}\x1b\\");
        terminal.ProcessOutput("X");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        var linkData = terminal.GetHyperlinkDataAt(0, 0);
        Assert.NotNull(linkData);
        Assert.Equal(uri, linkData.Uri);
    }

    #endregion

    #region HyperlinkWidget Integration Tests with Snapshots

    [Fact]
    public async Task HyperlinkWidget_SingleLink_RendersWithOsc8()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Click the link below:"),
                v.Hyperlink("Visit GitHub", "https://github.com/mitchdenny/hex1b")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Visit GitHub"), TimeSpan.FromSeconds(2))
            .Capture("single-link")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Capture snapshots
        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-single");

        // Verify the link text is rendered
        Assert.True(snapshot.ContainsText("Visit GitHub"));
        Assert.True(snapshot.ContainsText("Click the link below"));
    }

    [Fact]
    public async Task HyperlinkWidget_MultipleLinks_AllRenderCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 12);

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Navigation Links:"),
                v.Hyperlink("GitHub", "https://github.com"),
                v.Hyperlink("Documentation", "https://hex1b.dev/docs"),
                v.Hyperlink("Examples", "https://hex1b.dev/examples"),
                v.Hyperlink("API Reference", "https://hex1b.dev/api")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("API Reference"), TimeSpan.FromSeconds(2))
            .Capture("multiple-links")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Capture snapshots
        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-multiple");

        // Verify all links are rendered
        Assert.True(snapshot.ContainsText("GitHub"));
        Assert.True(snapshot.ContainsText("Documentation"));
        Assert.True(snapshot.ContainsText("Examples"));
        Assert.True(snapshot.ContainsText("API Reference"));
    }

    [Fact]
    public async Task HyperlinkWidget_InHStack_RendersInline()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 70, 8);

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Quick Links:"),
                v.HStack(h => [
                    h.Hyperlink("[Home]", "https://hex1b.dev"),
                    h.Text(" | "),
                    h.Hyperlink("[Docs]", "https://hex1b.dev/docs"),
                    h.Text(" | "),
                    h.Hyperlink("[GitHub]", "https://github.com/mitchdenny/hex1b")
                ])
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[GitHub]"), TimeSpan.FromSeconds(2))
            .Capture("inline-links")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Capture snapshots
        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-inline");

        // Verify inline layout
        Assert.True(snapshot.ContainsText("[Home]"));
        Assert.True(snapshot.ContainsText("[Docs]"));
        Assert.True(snapshot.ContainsText("[GitHub]"));
    }

    [Fact]
    public async Task HyperlinkWidget_InBorder_RendersWithFrame()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 10);

        await using var app = new Hex1bApp(
            ctx => ctx.Border(
                ctx.VStack(v => [
                    v.Text("Important Links"),
                    v.Text(""),
                    v.Hyperlink("Project Repository", "https://github.com/mitchdenny/hex1b"),
                    v.Hyperlink("Issue Tracker", "https://github.com/mitchdenny/hex1b/issues")
                ]),
                title: "Resources"
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Issue Tracker"), TimeSpan.FromSeconds(2))
            .Capture("bordered-links")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Capture snapshots
        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-bordered");

        // Verify content
        Assert.True(snapshot.ContainsText("Resources"));
        Assert.True(snapshot.ContainsText("Project Repository"));
        Assert.True(snapshot.ContainsText("Issue Tracker"));
    }

    [Fact]
    public async Task HyperlinkWidget_WithClickHandler_TracksClicks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 8);
        var clickedUri = "";
        var clickCount = 0;

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text($"Clicks: {clickCount}"),
                v.Hyperlink("Click Me", "https://example.com")
                    .OnClick(e => { clickedUri = e.Uri; clickCount++; })
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click Me"), TimeSpan.FromSeconds(2))
            .Enter() // Click the link
            .Enter() // Click again
            .Capture("after-clicks")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Capture snapshots
        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-clicked");

        Assert.Equal("https://example.com", clickedUri);
        Assert.Equal(2, clickCount);
        Assert.True(snapshot.ContainsText("Clicks: 2"));
    }

    [Fact]
    public async Task HyperlinkWidget_TabNavigation_FocusesLinks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);
        var lastClickedUri = "";

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Hyperlink("First Link", "https://first.com")
                    .OnClick(e => lastClickedUri = e.Uri),
                v.Hyperlink("Second Link", "https://second.com")
                    .OnClick(e => lastClickedUri = e.Uri),
                v.Hyperlink("Third Link", "https://third.com")
                    .OnClick(e => lastClickedUri = e.Uri)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Navigate and capture at each focus state
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Third Link"), TimeSpan.FromSeconds(2))
            .Capture("focus-first")
            .Tab()
            .Capture("focus-second")
            .Tab()
            .Capture("focus-third")
            .Enter() // Click third link
            .Capture("after-click-third")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Capture final snapshot
        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-navigation");

        Assert.Equal("https://third.com", lastClickedUri);
    }

    [Fact]
    public async Task HyperlinkWidget_MixedWithButtons_InterleavedCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 12);
        var buttonClicked = false;
        var linkClicked = false;

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Actions:"),
                v.Hyperlink("Read Documentation", "https://hex1b.dev/docs")
                    .OnClick(_ => linkClicked = true),
                v.Button("Submit Form")
                    .OnClick(_ => { buttonClicked = true; return Task.CompletedTask; }),
                v.Hyperlink("View Source", "https://github.com/mitchdenny/hex1b")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("View Source"), TimeSpan.FromSeconds(2))
            .Enter() // Click first link
            .Tab()
            .Enter() // Click button
            .Capture("mixed-widgets")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Capture snapshots
        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-mixed");

        Assert.True(linkClicked);
        Assert.True(buttonClicked);
        Assert.True(snapshot.ContainsText("Read Documentation"));
        Assert.True(snapshot.ContainsText("Submit Form"));
        Assert.True(snapshot.ContainsText("View Source"));
    }

    [Fact]
    public async Task HyperlinkWidget_ComplexUrls_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 12);

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Complex URL Examples:"),
                v.Hyperlink("Search Results", "https://www.google.com/search?q=terminal+hyperlinks&hl=en"),
                v.Hyperlink("Wikipedia Section", "https://en.wikipedia.org/wiki/ANSI_escape_code#OSC_(Operating_System_Command)_sequences"),
                v.Hyperlink("File Protocol", "file:///home/user/documents/readme.txt"),
                v.Hyperlink("Mailto Link", "mailto:test@example.com?subject=Hello&body=Test")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Mailto Link"), TimeSpan.FromSeconds(2))
            .Capture("complex-urls")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Capture snapshots
        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-complex-urls");

        Assert.True(snapshot.ContainsText("Search Results"));
        Assert.True(snapshot.ContainsText("Wikipedia Section"));
        Assert.True(snapshot.ContainsText("File Protocol"));
        Assert.True(snapshot.ContainsText("Mailto Link"));
    }

    #endregion
}

