using Hex1b.Input;
using Hex1b.Terminal;
using Hex1b.Terminal.Testing;
using Hex1b.Tokens;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Integration tests for the DeltaEncodingFilter to verify it only sends changed cells.
/// </summary>
public class DeltaEncodingFilterIntegrationTests
{
    /// <summary>
    /// A null presentation adapter that just discards output.
    /// Required to trigger the presentation filter pipeline.
    /// </summary>
    private class NullPresentationAdapter : IHex1bTerminalPresentationAdapter, IDisposable
    {
        private readonly int _width;
        private readonly int _height;
        private readonly TaskCompletionSource _disconnected = new();
        
        public NullPresentationAdapter(int width, int height)
        {
            _width = width;
            _height = height;
        }

        public int Width => _width;
        public int Height => _height;
        public TerminalCapabilities Capabilities => TerminalCapabilities.Minimal;
        public event Action<int, int>? Resized;
        public event Action? Disconnected;

        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
        {
            // Wait indefinitely until cancelled
            try
            {
                await _disconnected.Task.WaitAsync(ct);
            }
            catch (OperationCanceledException) { }
            return ReadOnlyMemory<byte>.Empty;
        }

        public ValueTask FlushAsync(CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask EnterTuiModeAsync(CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask ExitTuiModeAsync(CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disconnected?.Invoke();
            _disconnected.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            Disconnected?.Invoke();
            _disconnected.TrySetResult();
        }
    }

    /// <summary>
    /// A test presentation filter that captures the token stream after delta encoding.
    /// Placed after the DeltaEncodingFilter to see what actually gets sent.
    /// </summary>
    private class TokenCapturePresentationFilter : IHex1bTerminalPresentationFilter
    {
        public List<List<AnsiToken>> FrameTokens { get; } = new();
        public List<int> FrameCellCounts { get; } = new();
        
        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
        {
            FrameTokens.Clear();
            FrameCellCounts.Clear();
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(IReadOnlyList<AppliedToken> appliedTokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            var tokens = appliedTokens.Select(at => at.Token).ToList();
            FrameTokens.Add(tokens);
            
            // Count TextTokens as a proxy for cells being updated
            var textTokenCount = tokens.OfType<TextToken>().Sum(t => t.Text.Length);
            FrameCellCounts.Add(textTokenCount);
            
            return ValueTask.FromResult<IReadOnlyList<AnsiToken>>(tokens);
        }

        public ValueTask OnInputAsync(ReadOnlyMemory<byte> data, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task DeltaFilter_IdenticalFrames_NoOutputAfterFirst()
    {
        // Arrange
        var captureFilter = new TokenCapturePresentationFilter();
        var deltaFilter = new DeltaEncodingFilter();
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var presentation = new NullPresentationAdapter(40, 10);
        
        var terminalOptions = new Hex1bTerminalOptions
        {
            WorkloadAdapter = workload,
            PresentationAdapter = presentation,
            Width = 40,
            Height = 10
        };
        terminalOptions.PresentationFilters.Add(deltaFilter);
        terminalOptions.PresentationFilters.Add(captureFilter);
        
        using var terminal = new Hex1bTerminal(terminalOptions);
        
        // Static content that doesn't change
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new TextBlockWidget("Static Content")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .Wait(50) // Give app time to initialize
            .WaitUntil(s => s.ContainsText("Static Content"), TimeSpan.FromSeconds(5))
            .Wait(100) // Let a few frames pass
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        // After waiting for content, we should have received at least some frames with text
        Assert.True(captureFilter.FrameTokens.Count >= 1, 
            $"Should have at least one frame. Had {captureFilter.FrameTokens.Count}");
        
        // Find the first frame that actually has text content (skip control-only frames)
        var framesWithContent = captureFilter.FrameCellCounts.Where(c => c > 0).ToList();
        Assert.True(framesWithContent.Count > 0, 
            $"At least one frame should have text content. Frame counts: [{string.Join(", ", captureFilter.FrameCellCounts)}]");
        
        var firstContentFrame = framesWithContent[0];
        
        // After the first content render, subsequent frames should have very few cells (delta only)
        if (framesWithContent.Count > 1)
        {
            for (int i = 1; i < framesWithContent.Count; i++)
            {
                Assert.True(framesWithContent[i] <= firstContentFrame / 4,
                    $"Frame {i} should have significantly fewer cells than first content frame. " +
                    $"First frame: {firstContentFrame}, Frame {i}: {framesWithContent[i]}");
            }
        }
    }

    [Fact]
    public async Task DeltaFilter_ButtonClick_OnlyUpdatesButtonCell()
    {
        // Arrange
        var captureFilter = new TokenCapturePresentationFilter();
        var deltaFilter = new DeltaEncodingFilter();
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var presentation = new NullPresentationAdapter(60, 20);
        
        var terminalOptions = new Hex1bTerminalOptions
        {
            WorkloadAdapter = workload,
            PresentationAdapter = presentation,
            Width = 60,
            Height = 20
        };
        terminalOptions.PresentationFilters.Add(deltaFilter);
        terminalOptions.PresentationFilters.Add(captureFilter);
        
        using var terminal = new Hex1bTerminal(terminalOptions);
        
        var clickCount = 0;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new TextBlockWidget("Header Text That Should Not Change"),
                    new ButtonWidget($"Clicks: {clickCount}")
                        .OnClick(_ => { clickCount++; return Task.CompletedTask; }),
                    new TextBlockWidget("Footer Text That Should Not Change"),
                })
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Clicks: 0"), TimeSpan.FromSeconds(5))
            .Wait(50) // Let first frame complete
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Get the cell count right before the click
        var framesBeforeClick = captureFilter.FrameTokens.Count;
        
        await new Hex1bTestSequenceBuilder()
            .Key(Hex1bKey.Enter) // Click the focused button
            .WaitUntil(s => s.ContainsText("Clicks: 1"), TimeSpan.FromSeconds(5))
            .Capture("after_click")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        // The frames after the click should only update the button, not the header/footer
        // A full repaint of a 60x20 terminal would be ~1200 cells
        // Just updating "Clicks: 0" to "Clicks: 1" should be ~10-20 cells max
        
        if (captureFilter.FrameTokens.Count > framesBeforeClick)
        {
            for (int i = framesBeforeClick; i < captureFilter.FrameCellCounts.Count; i++)
            {
                // Allow some buffer for cursor position tokens etc, but should be way less than full screen
                Assert.True(captureFilter.FrameCellCounts[i] < 100,
                    $"Frame {i} after click should have minimal cells (< 100), but had {captureFilter.FrameCellCounts[i]}. " +
                    $"This suggests the delta filter is not working - it's repainting too much.");
            }
        }
    }

    [Fact]
    public async Task DeltaFilter_HSplitter_DragDoesNotRepaintEntireScreen()
    {
        // Arrange
        var captureFilter = new TokenCapturePresentationFilter();
        var deltaFilter = new DeltaEncodingFilter();
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var presentation = new NullPresentationAdapter(80, 24);
        
        var terminalOptions = new Hex1bTerminalOptions
        {
            WorkloadAdapter = workload,
            PresentationAdapter = presentation,
            Width = 80,
            Height = 24
        };
        terminalOptions.PresentationFilters.Add(deltaFilter);
        terminalOptions.PresentationFilters.Add(captureFilter);
        
        using var terminal = new Hex1bTerminal(terminalOptions);
        
        var splitterPosition = 20;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    left => [
                        left.Text("Left Panel Content"),
                        left.Text("More Left Content"),
                    ],
                    right => [
                        right.Text("Right Panel Content"),
                        right.Text("More Right Content"),
                    ],
                    leftWidth: splitterPosition
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .Wait(50) // Give app time to initialize
            .WaitUntil(s => s.ContainsText("Left Panel"), TimeSpan.FromSeconds(10))
            .Wait(100) // Let initial render complete
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        var framesBeforeDrag = captureFilter.FrameTokens.Count;
        var totalScreenCells = 80 * 24; // 1920 cells
        
        // Navigate to splitter and drag it
        await new Hex1bTestSequenceBuilder()
            .Tab() // Focus should move to splitter handle
            .Right() // Drag splitter right
            .Wait(50)
            .Right() // Drag more
            .Wait(50)
            .Capture("after_drag")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        // When dragging a splitter, we should NOT repaint the entire screen
        // Only the cells at the splitter boundary and any content that shifted should update
        // A full repaint would be ~1920 cells, a good delta should be < 10% of that
        
        var maxAcceptableCells = totalScreenCells / 4; // 25% of screen is generous threshold
        
        for (int i = framesBeforeDrag; i < captureFilter.FrameCellCounts.Count; i++)
        {
            Assert.True(captureFilter.FrameCellCounts[i] < maxAcceptableCells,
                $"Frame {i} during splitter drag should have < {maxAcceptableCells} cells, but had {captureFilter.FrameCellCounts[i]}. " +
                $"This suggests the delta filter is repainting too much of the screen during resize.");
        }
    }

    [Fact]
    public async Task DeltaFilter_ListSelection_OnlyUpdatesChangedRows()
    {
        // Arrange
        var captureFilter = new TokenCapturePresentationFilter();
        var deltaFilter = new DeltaEncodingFilter();
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var presentation = new NullPresentationAdapter(40, 15);
        
        var terminalOptions = new Hex1bTerminalOptions
        {
            WorkloadAdapter = workload,
            PresentationAdapter = presentation,
            Width = 40,
            Height = 15
        };
        terminalOptions.PresentationFilters.Add(deltaFilter);
        terminalOptions.PresentationFilters.Add(captureFilter);
        
        using var terminal = new Hex1bTerminal(terminalOptions);
        
        var items = new List<string> { "Item 1", "Item 2", "Item 3", "Item 4", "Item 5" };
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new TextBlockWidget("Static Header"),
                    new ListWidget(items),
                    new TextBlockWidget("Static Footer"),
                })
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(5))
            .Wait(100) // Let initial render complete
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        var framesBeforeNav = captureFilter.FrameTokens.Count;
        
        // Navigate down in the list - should only update 2 rows (old selection, new selection)
        await new Hex1bTestSequenceBuilder()
            .Down() // Move selection from Item 1 to Item 2
            .Wait(50)
            .Capture("after_nav")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        // Moving selection should only update 2 list items (old and new selection)
        // That's roughly 2 * 40 = 80 characters max, but with attributes maybe 100
        // Should definitely NOT repaint header, footer, or unchanged list items
        
        for (int i = framesBeforeNav; i < captureFilter.FrameCellCounts.Count; i++)
        {
            Assert.True(captureFilter.FrameCellCounts[i] < 200,
                $"Frame {i} after list navigation should have < 200 cells, but had {captureFilter.FrameCellCounts[i]}. " +
                $"Only 2 list rows should need updating when changing selection.");
        }
    }

    [Fact]
    public async Task DeltaFilter_Counter_OnlyUpdatesNumberDisplay()
    {
        // Arrange
        var captureFilter = new TokenCapturePresentationFilter();
        var deltaFilter = new DeltaEncodingFilter();
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var presentation = new NullPresentationAdapter(50, 10);
        
        var terminalOptions = new Hex1bTerminalOptions
        {
            WorkloadAdapter = workload,
            PresentationAdapter = presentation,
            Width = 50,
            Height = 10
        };
        terminalOptions.PresentationFilters.Add(deltaFilter);
        terminalOptions.PresentationFilters.Add(captureFilter);
        
        using var terminal = new Hex1bTerminal(terminalOptions);
        
        var counter = 0;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new TextBlockWidget("This header should not repaint"),
                    new HStackWidget(new Hex1bWidget[]
                    {
                        new ButtonWidget("-").OnClick(_ => { counter--; return Task.CompletedTask; }),
                        new TextBlockWidget($"  Count: {counter}  "),
                        new ButtonWidget("+").OnClick(_ => { counter++; return Task.CompletedTask; }),
                    }),
                    new TextBlockWidget("This footer should not repaint"),
                })
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Count: 0"), TimeSpan.FromSeconds(5))
            .Tab() // Focus moves from - button to + button
            .Wait(100) // Let renders complete
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        var framesBeforeIncrement = captureFilter.FrameTokens.Count;
        
        // Increment the counter
        await new Hex1bTestSequenceBuilder()
            .Key(Hex1bKey.Enter) // Click +
            .WaitUntil(s => s.ContainsText("Count: 1"), TimeSpan.FromSeconds(5))
            .Capture("after_increment")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        // Incrementing counter should only update "Count: 0" -> "Count: 1"
        // That's about 10-15 characters, not the whole 50x10 = 500 cell screen
        
        for (int i = framesBeforeIncrement; i < captureFilter.FrameCellCounts.Count; i++)
        {
            Assert.True(captureFilter.FrameCellCounts[i] < 100,
                $"Frame {i} after counter increment should have < 100 cells, but had {captureFilter.FrameCellCounts[i]}. " +
                $"Only the counter value should need updating, not the whole screen.");
        }
    }

    /// <summary>
    /// A presentation filter that captures exact cell positions that are updated.
    /// Tracks cursor position tokens followed by text tokens to determine exactly which cells are written.
    /// </summary>
    private class CellPositionCapturePresentationFilter : IHex1bTerminalPresentationFilter
    {
        public List<HashSet<(int X, int Y)>> FrameCellPositions { get; } = new();
        
        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
        {
            FrameCellPositions.Clear();
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(IReadOnlyList<AppliedToken> appliedTokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            var tokens = appliedTokens.Select(at => at.Token).ToList();
            var cellPositions = new HashSet<(int X, int Y)>();
            
            // Parse cursor positions and text to determine exact cell updates
            int cursorX = 0;
            int cursorY = 0;
            
            foreach (var token in tokens)
            {
                switch (token)
                {
                    case CursorPositionToken cpt:
                        // CursorPositionToken uses 1-based coordinates
                        cursorX = cpt.Column - 1;
                        cursorY = cpt.Row - 1;
                        break;
                        
                    case TextToken tt:
                        // Each character in the text token is a cell update
                        foreach (var ch in tt.Text)
                        {
                            if (ch >= ' ') // Skip control characters
                            {
                                cellPositions.Add((cursorX, cursorY));
                                cursorX++;
                            }
                        }
                        break;
                }
            }
            
            FrameCellPositions.Add(cellPositions);
            return ValueTask.FromResult<IReadOnlyList<AnsiToken>>(tokens);
        }

        public ValueTask OnInputAsync(ReadOnlyMemory<byte> data, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task DeltaFilter_HSplitter_MoveLeft_UpdatesOnlyTwoColumns()
    {
        // Arrange - use a precise filter that tracks exact cell positions
        var captureFilter = new CellPositionCapturePresentationFilter();
        var deltaFilter = new DeltaEncodingFilter();
        
        const int terminalWidth = 40;
        const int terminalHeight = 10;
        const int initialSplitterPosition = 20; // Splitter at column 20
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var presentation = new NullPresentationAdapter(terminalWidth, terminalHeight);
        
        var terminalOptions = new Hex1bTerminalOptions
        {
            WorkloadAdapter = workload,
            PresentationAdapter = presentation,
            Width = terminalWidth,
            Height = terminalHeight
        };
        terminalOptions.PresentationFilters.Add(deltaFilter);
        terminalOptions.PresentationFilters.Add(captureFilter);
        
        using var terminal = new Hex1bTerminal(terminalOptions);
        
        // Create splitter with empty VStacks on each side - no content to shift
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    left => [], // Empty left panel
                    right => [], // Empty right panel
                    leftWidth: initialSplitterPosition
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - wait for initial render
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .Wait(100) // Let initial render complete
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Record the frame count before the move
        var framesBeforeMove = captureFilter.FrameCellPositions.Count;
        
        // Move splitter left by pressing Left arrow (splitter should auto-focus or we Tab to it)
        await new Hex1bTestSequenceBuilder()
            .Left() // Move splitter left by 1 cell
            .Wait(100) // Let render complete
            .Capture("after_move")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - analyze the frames after the move
        Assert.True(captureFilter.FrameCellPositions.Count > framesBeforeMove,
            $"Should have frames after the move. Before: {framesBeforeMove}, After: {captureFilter.FrameCellPositions.Count}");
        
        // Get all cell positions updated after the move
        var allUpdatedCells = new HashSet<(int X, int Y)>();
        for (int i = framesBeforeMove; i < captureFilter.FrameCellPositions.Count; i++)
        {
            foreach (var cell in captureFilter.FrameCellPositions[i])
            {
                allUpdatedCells.Add(cell);
            }
        }
        
        // Get the unique X columns that were updated
        var updatedColumns = allUpdatedCells.Select(c => c.X).Distinct().OrderBy(x => x).ToList();
        
        // With empty panels, moving splitter left by 1 should only update cells at:
        // - The old splitter position (column 20, now becomes right panel background)
        // - The new splitter position (column 19, now becomes splitter)
        // So we expect at most 2-3 columns to be updated (allowing for handle width)
        
        Assert.True(updatedColumns.Count <= 4,
            $"Moving splitter left by 1 should update at most 4 columns, but updated {updatedColumns.Count} columns: [{string.Join(", ", updatedColumns)}]. " +
            $"Total cells updated: {allUpdatedCells.Count}. " +
            $"This suggests cells outside the splitter area are being repainted.");
        
        // Verify the updated columns are near the splitter position
        foreach (var col in updatedColumns)
        {
            var distanceFromSplitter = Math.Abs(col - initialSplitterPosition);
            Assert.True(distanceFromSplitter <= 3,
                $"Updated column {col} is {distanceFromSplitter} cells away from splitter at {initialSplitterPosition}. " +
                $"Only cells near the splitter should be updated.");
        }
        
        // Verify the number of cells is approximately height * columns (allowing for some variation)
        var maxExpectedCells = terminalHeight * 4; // At most 4 columns * height
        Assert.True(allUpdatedCells.Count <= maxExpectedCells,
            $"Should update at most {maxExpectedCells} cells (4 columns * {terminalHeight} rows), but updated {allUpdatedCells.Count} cells.");
    }

    /// <summary>
    /// When a node is replaced with a smaller one, the old region must be cleared.
    /// This tests the fix for the "ghosting" bug where old content remained visible.
    /// </summary>
    [Fact]
    public async Task DeltaFilter_NodeReplacement_ClearsOldRegion()
    {
        // Arrange
        const int terminalWidth = 50;
        const int terminalHeight = 10;
        
        using var workload = new Hex1bAppWorkloadAdapter();
        
        using var terminal = new Hex1bTerminal(workload, terminalWidth, terminalHeight);
        
        // State that toggles between a large widget and a small widget
        var showLarge = true;
        
        using var app = new Hex1bApp(
            ctx =>
            {
                Hex1bWidget content;
                if (showLarge)
                {
                    // Large widget with more children
                    content = ctx.VStack(v => [
                        v.Text("Line 1: XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"),
                        v.Text("Line 2: XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"),
                        v.Text("Line 3: XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"),
                        v.Text("Line 4: XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"),
                        v.Text("Line 5: XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"),
                    ]);
                }
                else
                {
                    // Small widget with fewer children
                    content = ctx.VStack(v => [
                        v.Text("SMALL")
                    ]);
                }
                
                return Task.FromResult<Hex1bWidget>(
                    ctx.VStack(v => [
                        v.Button("Toggle").OnClick(_ => { showLarge = !showLarge; return Task.CompletedTask; }),
                        content
                    ])
                );
            },
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - wait for initial render with large widget (5 lines)
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var beforeToggle = await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 5"), TimeSpan.FromSeconds(5))
            .Wait(50)
            .Capture("before_toggle")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Verify initial state has all 5 lines
        Assert.True(beforeToggle.ContainsText("Line 1"), "Should have Line 1 before toggle");
        Assert.True(beforeToggle.ContainsText("Line 5"), "Should have Line 5 before toggle");
        
        // Toggle to small widget by clicking the button
        var afterToggle = await new Hex1bTestSequenceBuilder()
            .Key(Hex1bKey.Enter) // Click Toggle button
            .WaitUntil(s => s.ContainsText("SMALL"), TimeSpan.FromSeconds(5))
            .Wait(50)
            .Capture("after_toggle")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // Assert - verify the terminal buffer has cleared the old content
        Assert.True(afterToggle.ContainsText("SMALL"), "Should have SMALL after toggle");
        
        // Lines 2-5 should be GONE (cleared to spaces)
        Assert.False(afterToggle.ContainsText("Line 2"), 
            $"Line 2 should be cleared after toggle. Buffer:\n{afterToggle}");
        Assert.False(afterToggle.ContainsText("Line 3"), 
            $"Line 3 should be cleared after toggle. Buffer:\n{afterToggle}");
        Assert.False(afterToggle.ContainsText("Line 4"), 
            $"Line 4 should be cleared after toggle. Buffer:\n{afterToggle}");
        Assert.False(afterToggle.ContainsText("Line 5"), 
            $"Line 5 should be cleared after toggle. Buffer:\n{afterToggle}");
    }
}
