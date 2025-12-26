using Hex1b.Terminal;
using Hex1b.Theming;
using System.Text;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the OptimizedPresentationFilter.
/// </summary>
public class OptimizedPresentationFilterTests
{
    [Fact]
    public async Task Filter_FirstWrite_ForwardsAsIs()
    {
        // Arrange
        var filter = new OptimizedPresentationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        var screenBuffer = CreateEmptyBuffer(80, 24);
        screenBuffer[0, 0] = new TerminalCell("H", null, null);
        
        var originalOutput = Encoding.UTF8.GetBytes("H");

        // Act
        var result = await filter.TransformOutputAsync(
            originalOutput,
            screenBuffer,
            80,
            24,
            TimeSpan.Zero);

        // Assert - First write should be forwarded as-is
        Assert.Equal(originalOutput.Length, result.Length);
    }

    [Fact]
    public async Task Filter_NoChanges_SuppressesOutput()
    {
        // Arrange
        var filter = new OptimizedPresentationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        var screenBuffer = CreateEmptyBuffer(80, 24);
        var originalOutput = Encoding.UTF8.GetBytes("Test");

        // First write to establish baseline
        await filter.TransformOutputAsync(originalOutput, screenBuffer, 80, 24, TimeSpan.Zero);

        // Act - Second write with same buffer (no changes)
        var result = await filter.TransformOutputAsync(
            originalOutput,
            screenBuffer,
            80,
            24,
            TimeSpan.Zero);

        // Assert - Should suppress output (no changes)
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public async Task Filter_CellChange_GeneratesOptimizedOutput()
    {
        // Arrange
        var filter = new OptimizedPresentationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        var screenBuffer1 = CreateEmptyBuffer(80, 24);
        screenBuffer1[0, 0] = new TerminalCell("A", null, null);
        
        var originalOutput1 = Encoding.UTF8.GetBytes("A");

        // First write
        await filter.TransformOutputAsync(originalOutput1, screenBuffer1, 80, 24, TimeSpan.Zero);

        // Create new buffer with change
        var screenBuffer2 = CreateEmptyBuffer(80, 24);
        screenBuffer2[0, 0] = new TerminalCell("B", null, null);
        
        var originalOutput2 = Encoding.UTF8.GetBytes("\x1b[1;1HB");

        // Act
        var result = await filter.TransformOutputAsync(
            originalOutput2,
            screenBuffer2,
            80,
            24,
            TimeSpan.Zero);

        // Assert - Should generate output for the change
        Assert.False(result.IsEmpty);
        var output = Encoding.UTF8.GetString(result.Span);
        Assert.Contains("B", output);
    }

    [Fact]
    public async Task Filter_MultipleChanges_GeneratesOptimizedOutput()
    {
        // Arrange
        var filter = new OptimizedPresentationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        var screenBuffer1 = CreateEmptyBuffer(80, 24);
        await filter.TransformOutputAsync(Array.Empty<byte>(), screenBuffer1, 80, 24, TimeSpan.Zero);

        // Create buffer with multiple changes
        var screenBuffer2 = CreateEmptyBuffer(80, 24);
        screenBuffer2[0, 0] = new TerminalCell("H", null, null);
        screenBuffer2[0, 1] = new TerminalCell("i", null, null);
        screenBuffer2[1, 0] = new TerminalCell("!", null, null);

        // Act
        var result = await filter.TransformOutputAsync(
            Encoding.UTF8.GetBytes("Hi\n!"),
            screenBuffer2,
            80,
            24,
            TimeSpan.Zero);

        // Assert
        Assert.False(result.IsEmpty);
        var output = Encoding.UTF8.GetString(result.Span);
        
        // Should contain the changed characters
        Assert.Contains("H", output);
        Assert.Contains("i", output);
        Assert.Contains("!", output);
    }

    [Fact]
    public async Task Filter_Resize_HandlesCorrectly()
    {
        // Arrange
        var filter = new OptimizedPresentationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        var screenBuffer1 = CreateEmptyBuffer(80, 24);
        screenBuffer1[0, 0] = new TerminalCell("A", null, null);
        
        await filter.TransformOutputAsync(Encoding.UTF8.GetBytes("A"), screenBuffer1, 80, 24, TimeSpan.Zero);

        // Act - Resize and wait for debounce to expire
        await filter.OnResizeAsync(100, 30, TimeSpan.Zero);
        await Task.Delay(60); // Wait for debounce window (50ms) to pass
        
        var screenBuffer2 = CreateEmptyBuffer(100, 30);
        screenBuffer2[0, 0] = new TerminalCell("B", null, null);
        
        var result = await filter.TransformOutputAsync(
            Encoding.UTF8.GetBytes("B"),
            screenBuffer2,
            100,
            30,
            TimeSpan.Zero);

        // Assert - Should handle resize correctly (forwards original on first frame after resize)
        Assert.False(result.IsEmpty);
    }

    [Fact]
    public async Task Filter_ResizeDebounce_SuppressesOutputDuringRapidResize()
    {
        // Arrange
        var filter = new OptimizedPresentationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        // Establish baseline
        var screenBuffer1 = CreateEmptyBuffer(80, 24);
        await filter.TransformOutputAsync(Encoding.UTF8.GetBytes("A"), screenBuffer1, 80, 24, TimeSpan.Zero);
        
        // Trigger resize - this starts the debounce window
        await filter.OnResizeAsync(100, 30, TimeSpan.Zero);
        
        // Immediately try to transform (within debounce window)
        var screenBuffer2 = CreateEmptyBuffer(100, 30);
        var originalOutput = Encoding.UTF8.GetBytes("B");
        
        // Act
        var result = await filter.TransformOutputAsync(
            originalOutput,
            screenBuffer2,
            100,
            30,
            TimeSpan.Zero);

        // Assert - During debounce, output should be SUPPRESSED (empty) to prevent lag
        Assert.True(result.IsEmpty, "Output should be suppressed during resize debounce window");
    }

    [Fact]
    public async Task Filter_ColorChange_DetectsChange()
    {
        // Arrange
        var filter = new OptimizedPresentationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        var screenBuffer1 = CreateEmptyBuffer(80, 24);
        screenBuffer1[0, 0] = new TerminalCell("A", null, null);
        
        await filter.TransformOutputAsync(Encoding.UTF8.GetBytes("A"), screenBuffer1, 80, 24, TimeSpan.Zero);

        // Change color
        var screenBuffer2 = CreateEmptyBuffer(80, 24);
        screenBuffer2[0, 0] = new TerminalCell("A", Hex1bColor.FromRgb(255, 0, 0), null);

        // Act
        var result = await filter.TransformOutputAsync(
            Encoding.UTF8.GetBytes("\x1b[31mA"),
            screenBuffer2,
            80,
            24,
            TimeSpan.Zero);

        // Assert - Should detect color change
        Assert.False(result.IsEmpty);
    }

    [Fact]
    public async Task Filter_AttributeChange_DetectsChange()
    {
        // Arrange
        var filter = new OptimizedPresentationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        var screenBuffer1 = CreateEmptyBuffer(80, 24);
        screenBuffer1[0, 0] = new TerminalCell("A", null, null, CellAttributes.None);
        
        await filter.TransformOutputAsync(Encoding.UTF8.GetBytes("A"), screenBuffer1, 80, 24, TimeSpan.Zero);

        // Change attribute
        var screenBuffer2 = CreateEmptyBuffer(80, 24);
        screenBuffer2[0, 0] = new TerminalCell("A", null, null, CellAttributes.Bold);

        // Act
        var result = await filter.TransformOutputAsync(
            Encoding.UTF8.GetBytes("\x1b[1mA"),
            screenBuffer2,
            80,
            24,
            TimeSpan.Zero);

        // Assert - Should detect attribute change
        Assert.False(result.IsEmpty);
    }

    [Fact]
    public async Task Filter_RapidResize_DoesNotThrow()
    {
        // Arrange
        var filter = new OptimizedPresentationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        // Simulate rapid resizing while processing output
        var tasks = new List<Task>();
        
        for (int i = 0; i < 100; i++)
        {
            var iteration = i;
            tasks.Add(Task.Run(async () =>
            {
                var width = 80 + (iteration % 20);
                var height = 24 + (iteration % 10);
                await filter.OnResizeAsync(width, height, TimeSpan.FromMilliseconds(iteration));
            }));
            
            tasks.Add(Task.Run(async () =>
            {
                var width = 80 + (iteration % 20);
                var height = 24 + (iteration % 10);
                var buffer = CreateEmptyBuffer(width, height);
                buffer[0, 0] = new TerminalCell($"{iteration}", null, null);
                
                await filter.TransformOutputAsync(
                    Encoding.UTF8.GetBytes($"{iteration}"),
                    buffer,
                    width,
                    height,
                    TimeSpan.FromMilliseconds(iteration));
            }));
        }

        // Act & Assert - Should complete without exceptions
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task Filter_DimensionMismatch_ForwardsOriginalOutput()
    {
        // Arrange
        var filter = new OptimizedPresentationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        // First write to establish baseline
        var screenBuffer1 = CreateEmptyBuffer(80, 24);
        await filter.TransformOutputAsync(Encoding.UTF8.GetBytes("A"), screenBuffer1, 80, 24, TimeSpan.Zero);
        
        // Now send output with different dimensions (simulating race where resize event
        // hasn't been processed yet)
        var screenBuffer2 = CreateEmptyBuffer(100, 30);
        screenBuffer2[0, 0] = new TerminalCell("B", null, null);
        var originalOutput = Encoding.UTF8.GetBytes("B");
        
        // Act
        var result = await filter.TransformOutputAsync(
            originalOutput,
            screenBuffer2,
            100,  // Different dimensions than filter knows about
            30,
            TimeSpan.Zero);

        // Assert - Should forward original output when dimensions don't match
        Assert.Equal(originalOutput.Length, result.Length);
    }

    [Fact]
    public async Task Filter_BackgroundColorRemoval_GeneratesResetSequence()
    {
        // Arrange - this tests the blue screen fix
        var filter = new OptimizedPresentationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        // First write with blue background
        var screenBuffer1 = CreateEmptyBuffer(80, 24);
        screenBuffer1[0, 0] = new TerminalCell("A", null, Hex1bColor.FromRgb(0, 0, 255));
        await filter.TransformOutputAsync(Encoding.UTF8.GetBytes("A"), screenBuffer1, 80, 24, TimeSpan.Zero);

        // Second write with no background (should reset)
        var screenBuffer2 = CreateEmptyBuffer(80, 24);
        screenBuffer2[0, 0] = new TerminalCell("A", null, null);  // No background
        
        // Act
        var result = await filter.TransformOutputAsync(
            Encoding.UTF8.GetBytes("A"),
            screenBuffer2,
            80,
            24,
            TimeSpan.Zero);

        // Assert - Should contain either the background reset sequence or a full reset
        // The \x1b[0m reset also clears background to default, so either is valid
        var output = Encoding.UTF8.GetString(result.Span);
        Assert.True(
            output.Contains("\x1b[49m") || output.Contains("\x1b[0m"), 
            $"Output should contain background reset or full reset: {output}");
    }

    [Fact]
    public async Task Filter_ForegroundColorRemoval_GeneratesResetSequence()
    {
        // Arrange
        var filter = new OptimizedPresentationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        // First write with red foreground
        var screenBuffer1 = CreateEmptyBuffer(80, 24);
        screenBuffer1[0, 0] = new TerminalCell("A", Hex1bColor.FromRgb(255, 0, 0), null);
        await filter.TransformOutputAsync(Encoding.UTF8.GetBytes("A"), screenBuffer1, 80, 24, TimeSpan.Zero);

        // Second write with no foreground (should reset)
        var screenBuffer2 = CreateEmptyBuffer(80, 24);
        screenBuffer2[0, 0] = new TerminalCell("A", null, null);  // No foreground
        
        // Act
        var result = await filter.TransformOutputAsync(
            Encoding.UTF8.GetBytes("A"),
            screenBuffer2,
            80,
            24,
            TimeSpan.Zero);

        // Assert - Should contain either the foreground reset sequence or a full reset
        // The \x1b[0m reset also clears foreground to default, so either is valid
        var output = Encoding.UTF8.GetString(result.Span);
        Assert.True(
            output.Contains("\x1b[39m") || output.Contains("\x1b[0m"), 
            $"Output should contain foreground reset or full reset: {output}");
    }

    private static TerminalCell[,] CreateEmptyBuffer(int width, int height)
    {
        var buffer = new TerminalCell[height, width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                buffer[y, x] = TerminalCell.Empty;
            }
        }
        return buffer;
    }
}
