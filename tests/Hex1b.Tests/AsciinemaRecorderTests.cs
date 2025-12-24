using System.Text;
using System.Text.Json;
using Hex1b;
using Hex1b.Input;
using Hex1b.Terminal;
using Hex1b.Terminal.Testing;
using Hex1b.Widgets;
using Microsoft.Extensions.Time.Testing;

namespace Hex1b.Tests;

public class AsciinemaRecorderTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string GetTempFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hex1b_test_{Guid.NewGuid()}.cast");
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { }
        }
    }

    [Fact]
    public async Task FlushAsync_WritesValidAsciicastV2Format()
    {
        // Arrange
        var tempFile = GetTempFile();
        var workload = new Hex1bAppWorkloadAdapter();
        var options = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 24,
            WorkloadAdapter = workload
        };
        var recorder = options.AddAsciinemaRecorder(tempFile, new AsciinemaRecorderOptions { Title = "Test Recording" });
        using var terminal = new Hex1bTerminal(options);

        // Act - simulate some output
        workload.Write("Hello, World!");
        terminal.FlushOutput();

        await recorder.FlushAsync(TestContext.Current.CancellationToken);

        // Assert
        var content = await File.ReadAllTextAsync(tempFile, TestContext.Current.CancellationToken);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 2, "Should have header and at least one event");

        var header = JsonDocument.Parse(lines[0]);
        Assert.Equal(2, header.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(80, header.RootElement.GetProperty("width").GetInt32());
        Assert.Equal(24, header.RootElement.GetProperty("height").GetInt32());
        Assert.Equal("Test Recording", header.RootElement.GetProperty("title").GetString());

        // Check for output event
        var eventArray = JsonDocument.Parse(lines[1]);
        Assert.Equal(JsonValueKind.Array, eventArray.RootElement.ValueKind);
        Assert.Equal(3, eventArray.RootElement.GetArrayLength());
        Assert.Equal("o", eventArray.RootElement[1].GetString());
        Assert.Contains("Hello", eventArray.RootElement[2].GetString());
    }

    [Fact]
    public async Task FlushAsync_RecordsResizeEvents()
    {
        // Arrange
        var tempFile = GetTempFile();
        var workload = new Hex1bAppWorkloadAdapter();
        var options = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 24,
            WorkloadAdapter = workload
        };
        var recorder = options.AddAsciinemaRecorder(tempFile);
        using var terminal = new Hex1bTerminal(options);

        // Act - directly call the filter's OnResizeAsync since we're in headless mode
        var filter = (IHex1bTerminalWorkloadFilter)recorder;
        await filter.OnResizeAsync(100, 40, TimeSpan.FromSeconds(1));

        await recorder.FlushAsync(TestContext.Current.CancellationToken);

        // Assert
        var content = await File.ReadAllTextAsync(tempFile, TestContext.Current.CancellationToken);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        // Look for resize event
        bool foundResize = false;
        foreach (var line in lines.Skip(1)) // Skip header
        {
            var evt = JsonDocument.Parse(line);
            if (evt.RootElement[1].GetString() == "r")
            {
                Assert.Equal("100x40", evt.RootElement[2].GetString());
                foundResize = true;
                break;
            }
        }

        Assert.True(foundResize, "Should have recorded a resize event");
    }

    [Fact]
    public async Task FlushAsync_CapturesInputWhenEnabled()
    {
        // Arrange
        var tempFile = GetTempFile();
        var workload = new Hex1bAppWorkloadAdapter();
        var options = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 24,
            WorkloadAdapter = workload
        };
        var recorder = options.AddAsciinemaRecorder(tempFile, new AsciinemaRecorderOptions { CaptureInput = true });
        using var terminal = new Hex1bTerminal(options);

        // Simulate input by calling the filter directly (since we're headless)
        var filter = (IHex1bTerminalWorkloadFilter)recorder;
        await filter.OnInputAsync("hello"u8.ToArray(), TimeSpan.FromSeconds(1));

        // Act
        await recorder.FlushAsync(TestContext.Current.CancellationToken);

        // Assert
        var content = await File.ReadAllTextAsync(tempFile, TestContext.Current.CancellationToken);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 2);

        var evt = JsonDocument.Parse(lines[1]);
        Assert.Equal("i", evt.RootElement[1].GetString());
        Assert.Equal("hello", evt.RootElement[2].GetString());
    }

    [Fact]
    public async Task FlushAsync_DoesNotCaptureInputByDefault()
    {
        // Arrange
        var tempFile = GetTempFile();
        var workload = new Hex1bAppWorkloadAdapter();
        var options = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 24,
            WorkloadAdapter = workload
        };
        var recorder = options.AddAsciinemaRecorder(tempFile);
        using var terminal = new Hex1bTerminal(options);

        // Simulate input
        var filter = (IHex1bTerminalWorkloadFilter)recorder;
        await filter.OnInputAsync("hello"u8.ToArray(), TimeSpan.FromSeconds(1));

        // Act
        await recorder.FlushAsync(TestContext.Current.CancellationToken);

        // Assert
        var content = await File.ReadAllTextAsync(tempFile, TestContext.Current.CancellationToken);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        // Should be only header (input not captured)
        Assert.Single(lines);
    }

    [Fact]
    public void AddMarker_AddsMarkerEvent()
    {
        // Arrange
        var tempFile = GetTempFile();
        var workload = new Hex1bAppWorkloadAdapter();
        var options = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 24,
            WorkloadAdapter = workload
        };
        var recorder = options.AddAsciinemaRecorder(tempFile);
        using var terminal = new Hex1bTerminal(options);

        // Act
        recorder.AddMarker("Chapter 1", TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(1, recorder.PendingEventCount);
    }

    [Fact]
    public async Task FlushAsync_IncludesMarkerEvents()
    {
        // Arrange
        var tempFile = GetTempFile();
        var workload = new Hex1bAppWorkloadAdapter();
        var options = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 24,
            WorkloadAdapter = workload
        };
        var recorder = options.AddAsciinemaRecorder(tempFile);
        using var terminal = new Hex1bTerminal(options);

        recorder.AddMarker("Start", TimeSpan.Zero);
        recorder.AddMarker("Chapter 1", TimeSpan.FromSeconds(5));

        // Act
        await recorder.FlushAsync(TestContext.Current.CancellationToken);

        // Assert
        var content = await File.ReadAllTextAsync(tempFile, TestContext.Current.CancellationToken);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var markers = new List<string>();
        foreach (var line in lines.Skip(1))
        {
            var evt = JsonDocument.Parse(line);
            if (evt.RootElement[1].GetString() == "m")
            {
                markers.Add(evt.RootElement[2].GetString() ?? "");
            }
        }

        Assert.Equal(2, markers.Count);
        Assert.Equal("Start", markers[0]);
        Assert.Equal("Chapter 1", markers[1]);
    }

    [Fact]
    public async Task FlushAsync_IncludesEnvironmentWhenEnabled()
    {
        // Arrange
        var tempFile = GetTempFile();
        var workload = new Hex1bAppWorkloadAdapter();
        var options = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 24,
            WorkloadAdapter = workload
        };
        var recorder = options.AddAsciinemaRecorder(tempFile, new AsciinemaRecorderOptions { CaptureEnvironment = true });
        using var terminal = new Hex1bTerminal(options);

        // Act
        await recorder.FlushAsync(TestContext.Current.CancellationToken);

        // Assert
        var content = await File.ReadAllTextAsync(tempFile, TestContext.Current.CancellationToken);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(lines);

        var header = JsonDocument.Parse(lines[0]);
        Assert.True(header.RootElement.TryGetProperty("env", out var env));
        Assert.True(env.TryGetProperty("TERM", out _));
    }

    [Fact]
    public async Task FlushAsync_ExcludesEnvironmentWhenDisabled()
    {
        // Arrange
        var tempFile = GetTempFile();
        var workload = new Hex1bAppWorkloadAdapter();
        var options = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 24,
            WorkloadAdapter = workload
        };
        var recorder = options.AddAsciinemaRecorder(tempFile, new AsciinemaRecorderOptions { CaptureEnvironment = false });
        using var terminal = new Hex1bTerminal(options);

        // Act
        await recorder.FlushAsync(TestContext.Current.CancellationToken);

        // Assert
        var content = await File.ReadAllTextAsync(tempFile, TestContext.Current.CancellationToken);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(lines);

        var header = JsonDocument.Parse(lines[0]);
        Assert.False(header.RootElement.TryGetProperty("env", out _));
    }

    [Fact]
    public async Task ClearPending_RemovesPendingEvents()
    {
        // Arrange
        var tempFile = GetTempFile();
        var recorder = new AsciinemaRecorder(tempFile);
        var filter = (IHex1bTerminalWorkloadFilter)recorder;
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        await filter.OnOutputAsync("test"u8.ToArray(), TimeSpan.FromSeconds(1));

        Assert.Equal(1, recorder.PendingEventCount);

        // Act
        recorder.ClearPending();

        // Assert
        Assert.Equal(0, recorder.PendingEventCount);
    }

    [Fact]
    public async Task FlushAsync_WritesToFile()
    {
        // Arrange
        var tempFile = GetTempFile();
        var workload = new Hex1bAppWorkloadAdapter();
        var options = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 24,
            WorkloadAdapter = workload
        };
        var recorder = options.AddAsciinemaRecorder(tempFile, new AsciinemaRecorderOptions { Title = "File Test" });
        using var terminal = new Hex1bTerminal(options);

        workload.Write("Hello!");
        terminal.FlushOutput();

        // Act
        await recorder.FlushAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(File.Exists(tempFile));
        var content = await File.ReadAllTextAsync(tempFile, TestContext.Current.CancellationToken);
        Assert.Contains("\"version\":2", content);
        Assert.Contains("File Test", content);
        Assert.Contains("Hello!", content);
    }

    [Fact]
    public async Task CaptureCast_AttachesToTestResults()
    {
        // Arrange
        var tempFile = GetTempFile();
        var workload = new Hex1bAppWorkloadAdapter();
        var options = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 24,
            WorkloadAdapter = workload
        };
        var recorder = options.AddAsciinemaRecorder(tempFile, new AsciinemaRecorderOptions { Title = "Capture Test" });
        using var terminal = new Hex1bTerminal(options);

        workload.Write("\x1b[1;32mGreen bold text\x1b[0m");
        terminal.FlushOutput();

        // Act - should not throw and should create file
        await TestCaptureHelper.CaptureCastAsync(recorder, "demo", TestContext.Current.CancellationToken);

        // Assert - check that the file was written
        Assert.True(File.Exists(tempFile));
        var content = await File.ReadAllTextAsync(tempFile, TestContext.Current.CancellationToken);
        Assert.Contains("Green bold text", content);
    }

    /// <summary>
    /// An extended 10-second test that exercises a full responsive todo application
    /// with resizes, navigation, text input, and button interactions.
    /// The generated .cast file can be played back with asciinema.
    /// </summary>
    [Fact]
    public async Task RecordResponsiveTodoSession_10Seconds()
    {
        // === Setup: Create a responsive todo app similar to the website example ===
        var state = new TodoState();
        var tempFile = GetTempFile();
        
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminalOptions = new Hex1bTerminalOptions
        {
            Width = 120,
            Height = 30,
            WorkloadAdapter = workload
        };
        var recorder = terminalOptions.AddAsciinemaRecorder(tempFile, new AsciinemaRecorderOptions
        {
            Title = "Responsive Todo App Demo",
            CaptureInput = true,  // Capture keystrokes for demonstration
            IdleTimeLimit = 2.0f  // Compress idle time in playback
        });
        
        using var terminal = new Hex1bTerminal(terminalOptions);
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(BuildResponsiveTodoWidget(ctx, state)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);

        // === Phase 1: Initial render at 120 cols (wide layout) ===
        recorder.AddMarker("App Start - Wide Layout (120 cols)");
        
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Todo Items"), TimeSpan.FromSeconds(2))
            .Wait(TimeSpan.FromMilliseconds(500))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // === Phase 2: Navigate the todo list ===
        recorder.AddMarker("Navigating Todo List");
        
        await new Hex1bTestSequenceBuilder()
            .Key(Hex1bKey.DownArrow)
            .Wait(TimeSpan.FromMilliseconds(300))
            .Key(Hex1bKey.DownArrow)
            .Wait(TimeSpan.FromMilliseconds(300))
            .Key(Hex1bKey.DownArrow)
            .Wait(TimeSpan.FromMilliseconds(500))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // === Phase 3: Toggle a todo item ===
        recorder.AddMarker("Toggling Todo Item");
        
        await new Hex1bTestSequenceBuilder()
            .Key(Hex1bKey.Spacebar)  // Toggle the selected item
            .Wait(TimeSpan.FromMilliseconds(500))
            .Key(Hex1bKey.DownArrow)
            .Wait(TimeSpan.FromMilliseconds(200))
            .Key(Hex1bKey.Spacebar)  // Toggle another
            .Wait(TimeSpan.FromMilliseconds(500))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // === Phase 4: Tab to the text input and add a new todo ===
        recorder.AddMarker("Adding New Todo Item");
        
        await new Hex1bTestSequenceBuilder()
            .Tab()
            .Wait(TimeSpan.FromMilliseconds(300))
            .Type("Buy holiday gifts")
            .Wait(TimeSpan.FromMilliseconds(500))
            .Tab()  // Move to Add button
            .Wait(TimeSpan.FromMilliseconds(200))
            .Key(Hex1bKey.Enter)  // Click Add
            .Wait(TimeSpan.FromMilliseconds(500))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // === Phase 5: Resize to medium layout ===
        recorder.AddMarker("Resizing to Medium Layout (80 cols)");
        
        // Notify the filter about resize
        await ((IHex1bTerminalWorkloadFilter)recorder).OnResizeAsync(80, 24, TimeSpan.FromSeconds(4));
        terminal.Resize(80, 24);
        await workload.ResizeAsync(80, 24, TestContext.Current.CancellationToken);
        
        await new Hex1bTestSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(800))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // === Phase 6: Navigate in medium layout ===
        recorder.AddMarker("Navigating in Medium Layout");
        
        await new Hex1bTestSequenceBuilder()
            .Shift().Tab()  // Go back to list
            .Wait(TimeSpan.FromMilliseconds(300))
            .Shift().Tab()
            .Wait(TimeSpan.FromMilliseconds(300))
            .Key(Hex1bKey.UpArrow)
            .Wait(TimeSpan.FromMilliseconds(200))
            .Key(Hex1bKey.UpArrow)
            .Wait(TimeSpan.FromMilliseconds(200))
            .Key(Hex1bKey.UpArrow)
            .Wait(TimeSpan.FromMilliseconds(500))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // === Phase 7: Resize to compact layout ===
        recorder.AddMarker("Resizing to Compact Layout (50 cols)");
        
        await ((IHex1bTerminalWorkloadFilter)recorder).OnResizeAsync(50, 20, TimeSpan.FromSeconds(6));
        terminal.Resize(50, 20);
        await workload.ResizeAsync(50, 20, TestContext.Current.CancellationToken);
        
        await new Hex1bTestSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(800))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // === Phase 8: Add another todo in compact mode ===
        recorder.AddMarker("Adding Todo in Compact Mode");
        
        await new Hex1bTestSequenceBuilder()
            .Tab()
            .Wait(TimeSpan.FromMilliseconds(300))
            .Type("Call mom")
            .Wait(TimeSpan.FromMilliseconds(400))
            .Tab()
            .Wait(TimeSpan.FromMilliseconds(200))
            .Key(Hex1bKey.Enter)
            .Wait(TimeSpan.FromMilliseconds(500))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // === Phase 9: Resize back to extra wide ===
        recorder.AddMarker("Resizing to Extra Wide Layout (160 cols)");
        
        await ((IHex1bTerminalWorkloadFilter)recorder).OnResizeAsync(160, 35, TimeSpan.FromSeconds(8));
        terminal.Resize(160, 35);
        await workload.ResizeAsync(160, 35, TestContext.Current.CancellationToken);
        
        await new Hex1bTestSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(800))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // === Phase 10: Final navigation and interactions ===
        recorder.AddMarker("Final Interactions");
        
        await new Hex1bTestSequenceBuilder()
            .Shift().Tab()
            .Wait(TimeSpan.FromMilliseconds(200))
            .Shift().Tab()
            .Wait(TimeSpan.FromMilliseconds(200))
            .Key(Hex1bKey.DownArrow)
            .Wait(TimeSpan.FromMilliseconds(150))
            .Key(Hex1bKey.DownArrow)
            .Wait(TimeSpan.FromMilliseconds(150))
            .Key(Hex1bKey.DownArrow)
            .Wait(TimeSpan.FromMilliseconds(150))
            .Key(Hex1bKey.DownArrow)
            .Wait(TimeSpan.FromMilliseconds(150))
            .Key(Hex1bKey.Spacebar)  // Toggle
            .Wait(TimeSpan.FromMilliseconds(500))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // === Shutdown ===
        recorder.AddMarker("Session End");
        
        await new Hex1bTestSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(500))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Wait for the terminal to process the final cleanup output (ExitTuiMode sequences)
        var exitTimeout = TimeSpan.FromSeconds(2);
        var exitStart = DateTime.UtcNow;
        while (terminal.InAlternateScreen && DateTime.UtcNow - exitStart < exitTimeout)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        // === Save the recording ===
        await TestCaptureHelper.CaptureCastAsync(recorder, "responsive-todo-demo", TestContext.Current.CancellationToken);

        // === Assertions ===
        await recorder.FlushAsync(TestContext.Current.CancellationToken);
        var content = await File.ReadAllTextAsync(tempFile, TestContext.Current.CancellationToken);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length > 50, $"Should have many events, got {lines.Length}");
        
        // Verify the new todos were added
        Assert.Contains(state.Items, t => t.Title == "Buy holiday gifts");
        Assert.Contains(state.Items, t => t.Title == "Call mom");
    }

    /// <summary>
    /// An enhanced 20-second recording test using FakeTimeProvider for instant execution.
    /// Includes gradual mouse movements, slow typing, and gradual window resizing.
    /// The recording appears to take 20 seconds but executes in milliseconds.
    /// </summary>
    [Fact]
    public async Task RecordResponsiveTodoSession_20Seconds_WithFakeTimeProvider()
    {
        // === Setup: Create a responsive todo app with fake time provider ===
        var state = new TodoState();
        var tempFile = GetTempFile();
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminalOptions = new Hex1bTerminalOptions
        {
            Width = 120,
            Height = 30,
            WorkloadAdapter = workload,
            TimeProvider = fakeTime
        };
        var recorder = terminalOptions.AddAsciinemaRecorder(tempFile, new AsciinemaRecorderOptions
        {
            Title = "Responsive Todo App Demo - Extended",
            CaptureInput = true,
            IdleTimeLimit = 2.0f
        });
        
        using var terminal = new Hex1bTerminal(terminalOptions);
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(BuildResponsiveTodoWidget(ctx, state)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);

        // Track elapsed time since session start for markers
        var sessionStart = fakeTime.GetUtcNow();
        TimeSpan GetElapsed() => fakeTime.GetUtcNow() - sessionStart;

        // Helper to advance fake time and flush output
        async Task AdvanceTimeAsync(TimeSpan duration)
        {
            fakeTime.Advance(duration);
            terminal.FlushOutput();
            await Task.Yield();
        }

        // Helper for gradual typing with time progression
        async Task TypeSlowlyAsync(string text, TimeSpan perChar)
        {
            foreach (var c in text)
            {
                workload.SendKey(Hex1bKey.None, c, Hex1bModifiers.None);
                terminal.FlushOutput();
                await AdvanceTimeAsync(perChar);
            }
        }

        // Helper for gradual resize
        async Task ResizeGraduallyAsync(int startWidth, int startHeight, int endWidth, int endHeight, int steps, TimeSpan totalDuration)
        {
            var stepDuration = totalDuration / steps;
            for (int i = 1; i <= steps; i++)
            {
                var t = (double)i / steps;
                var width = (int)(startWidth + (endWidth - startWidth) * t);
                var height = (int)(startHeight + (endHeight - startHeight) * t);
                await ((IHex1bTerminalWorkloadFilter)recorder).OnResizeAsync(width, height, GetElapsed());
                terminal.Resize(width, height);
                await workload.ResizeAsync(width, height, TestContext.Current.CancellationToken);
                terminal.FlushOutput();
                await AdvanceTimeAsync(stepDuration);
            }
        }

        // === Phase 1: Initial render and intro (0-2 seconds) ===
        recorder.AddMarker("App Start - Wide Layout (120 cols)", GetElapsed());
        terminal.FlushOutput();
        await AdvanceTimeAsync(TimeSpan.FromSeconds(1));

        // Wait for initial render
        for (int i = 0; i < 10 && !terminal.CreateSnapshot().ContainsText("Todo Items"); i++)
        {
            terminal.FlushOutput();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        await AdvanceTimeAsync(TimeSpan.FromSeconds(1));

        // === Phase 2: Navigate the todo list slowly (2-4 seconds) ===
        recorder.AddMarker("Slow Navigation Through List", GetElapsed());
        
        for (int i = 0; i < 5; i++)
        {
            workload.SendKey(Hex1bKey.DownArrow, modifiers: Hex1bModifiers.None);
            terminal.FlushOutput();
            await AdvanceTimeAsync(TimeSpan.FromMilliseconds(400));
        }

        // === Phase 3: Toggle items with pauses (4-5.5 seconds) ===
        recorder.AddMarker("Toggling Todo Items", GetElapsed());
        
        workload.SendKey(Hex1bKey.Spacebar, modifiers: Hex1bModifiers.None);
        terminal.FlushOutput();
        await AdvanceTimeAsync(TimeSpan.FromMilliseconds(600));
        
        workload.SendKey(Hex1bKey.UpArrow, modifiers: Hex1bModifiers.None);
        terminal.FlushOutput();
        await AdvanceTimeAsync(TimeSpan.FromMilliseconds(300));
        
        workload.SendKey(Hex1bKey.Spacebar, modifiers: Hex1bModifiers.None);
        terminal.FlushOutput();
        await AdvanceTimeAsync(TimeSpan.FromMilliseconds(600));

        // === Phase 4: Tab to text input and type slowly (5.5-8 seconds) ===
        recorder.AddMarker("Slow Typing - Adding New Todo", GetElapsed());
        
        workload.SendKey(Hex1bKey.Tab, modifiers: Hex1bModifiers.None);
        terminal.FlushOutput();
        await AdvanceTimeAsync(TimeSpan.FromMilliseconds(400));
        
        await TypeSlowlyAsync("Plan vacation trip", TimeSpan.FromMilliseconds(120));
        await AdvanceTimeAsync(TimeSpan.FromMilliseconds(300));
        
        workload.SendKey(Hex1bKey.Tab, modifiers: Hex1bModifiers.None);
        terminal.FlushOutput();
        await AdvanceTimeAsync(TimeSpan.FromMilliseconds(200));
        
        workload.SendKey(Hex1bKey.Enter, modifiers: Hex1bModifiers.None);
        terminal.FlushOutput();
        await AdvanceTimeAsync(TimeSpan.FromMilliseconds(500));

        // === Phase 5: Gradual resize to medium layout (8-10 seconds) ===
        recorder.AddMarker("Gradual Resize to Medium Layout", GetElapsed());
        await ResizeGraduallyAsync(120, 30, 80, 24, 8, TimeSpan.FromSeconds(2));

        // === Phase 6: More navigation and interactions (10-12 seconds) ===
        recorder.AddMarker("Medium Layout Interactions", GetElapsed());
        
        // Shift-Tab back to list
        workload.SendKey(Hex1bKey.Tab, modifiers: Hex1bModifiers.Shift);
        terminal.FlushOutput();
        await AdvanceTimeAsync(TimeSpan.FromMilliseconds(300));
        workload.SendKey(Hex1bKey.Tab, modifiers: Hex1bModifiers.Shift);
        terminal.FlushOutput();
        await AdvanceTimeAsync(TimeSpan.FromMilliseconds(300));
        
        // Navigate up slowly
        for (int i = 0; i < 4; i++)
        {
            workload.SendKey(Hex1bKey.UpArrow, modifiers: Hex1bModifiers.None);
            terminal.FlushOutput();
            await AdvanceTimeAsync(TimeSpan.FromMilliseconds(350));
        }

        // === Phase 7: Gradual resize to compact (12-14 seconds) ===
        recorder.AddMarker("Gradual Resize to Compact Layout", GetElapsed());
        await ResizeGraduallyAsync(80, 24, 50, 20, 8, TimeSpan.FromSeconds(2));

        // === Phase 8: Add another todo with slow typing (14-16 seconds) ===
        recorder.AddMarker("Adding Todo in Compact Mode", GetElapsed());
        
        workload.SendKey(Hex1bKey.Tab, modifiers: Hex1bModifiers.None);
        terminal.FlushOutput();
        await AdvanceTimeAsync(TimeSpan.FromMilliseconds(300));
        
        await TypeSlowlyAsync("Wrap presents", TimeSpan.FromMilliseconds(100));
        await AdvanceTimeAsync(TimeSpan.FromMilliseconds(200));
        
        workload.SendKey(Hex1bKey.Tab, modifiers: Hex1bModifiers.None);
        terminal.FlushOutput();
        await AdvanceTimeAsync(TimeSpan.FromMilliseconds(200));
        
        workload.SendKey(Hex1bKey.Enter, modifiers: Hex1bModifiers.None);
        terminal.FlushOutput();
        await AdvanceTimeAsync(TimeSpan.FromMilliseconds(500));

        // === Phase 9: Gradual resize to extra wide (16-18 seconds) ===
        recorder.AddMarker("Gradual Resize to Extra Wide", GetElapsed());
        await ResizeGraduallyAsync(50, 20, 160, 35, 10, TimeSpan.FromSeconds(2));

        // === Phase 10: Final interactions and navigation (18-19.5 seconds) ===
        recorder.AddMarker("Final Interactions - Extra Wide", GetElapsed());
        
        workload.SendKey(Hex1bKey.Tab, modifiers: Hex1bModifiers.Shift);
        terminal.FlushOutput();
        await AdvanceTimeAsync(TimeSpan.FromMilliseconds(200));
        workload.SendKey(Hex1bKey.Tab, modifiers: Hex1bModifiers.Shift);
        terminal.FlushOutput();
        await AdvanceTimeAsync(TimeSpan.FromMilliseconds(200));
        
        // Navigate through list with varied timing
        for (int i = 0; i < 6; i++)
        {
            workload.SendKey(Hex1bKey.DownArrow, modifiers: Hex1bModifiers.None);
            terminal.FlushOutput();
            await AdvanceTimeAsync(TimeSpan.FromMilliseconds(150 + i * 20));
        }
        
        // Toggle a few items
        workload.SendKey(Hex1bKey.Spacebar, modifiers: Hex1bModifiers.None);
        terminal.FlushOutput();
        await AdvanceTimeAsync(TimeSpan.FromMilliseconds(400));

        // === Phase 11: Shutdown (19.5-20 seconds) ===
        recorder.AddMarker("Session End", GetElapsed());
        await AdvanceTimeAsync(TimeSpan.FromMilliseconds(300));
        
        workload.SendKey(Hex1bKey.C, modifiers: Hex1bModifiers.Control);
        terminal.FlushOutput();
        await AdvanceTimeAsync(TimeSpan.FromMilliseconds(200));

        await runTask;

        // Brief wait for cleanup
        terminal.FlushOutput();

        // === Save the recording ===
        await TestCaptureHelper.CaptureCastAsync(recorder, "responsive-todo-extended", TestContext.Current.CancellationToken);

        // === Assertions ===
        await recorder.FlushAsync(TestContext.Current.CancellationToken);
        var content = await File.ReadAllTextAsync(tempFile, TestContext.Current.CancellationToken);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length > 100, $"Should have many events, got {lines.Length}");
        
        // Verify the new todos were added
        Assert.Contains(state.Items, t => t.Title == "Plan vacation trip");
        Assert.Contains(state.Items, t => t.Title == "Wrap presents");
        
        // Verify the recording spans approximately 20 seconds
        // Parse the last event to check its timestamp
        var lastEvent = lines.Last();
        if (lastEvent.StartsWith('['))
        {
            var timestampEnd = lastEvent.IndexOf(',');
            if (timestampEnd > 1 && double.TryParse(lastEvent[1..timestampEnd], out var lastTimestamp))
            {
                Assert.True(lastTimestamp >= 18, $"Recording should be at least 18 seconds, was {lastTimestamp:F2}s");
            }
        }
    }

    // === Helper classes and methods for the responsive todo test ===

    private class TodoState
    {
        public List<TodoItem> Items { get; } =
        [
            new("Buy groceries", true),
            new("Review pull request", false),
            new("Write documentation", false),
            new("Fix bug #42", true),
            new("Deploy to staging", false),
            new("Team standup meeting", true),
        ];

        public int SelectedIndex { get; set; }
        public string NewItemText { get; set; } = "";

        public void AddItem()
        {
            if (!string.IsNullOrWhiteSpace(NewItemText))
            {
                Items.Add(new TodoItem(NewItemText, false));
                NewItemText = "";
            }
        }

        public void ToggleSelected()
        {
            if (SelectedIndex >= 0 && SelectedIndex < Items.Count)
            {
                Items[SelectedIndex] = Items[SelectedIndex] with 
                { 
                    IsComplete = !Items[SelectedIndex].IsComplete 
                };
            }
        }

        public IReadOnlyList<string> GetListItems() =>
            Items.Select(item => FormatTodoItem(item)).ToList();

        private static string FormatTodoItem(TodoItem item)
        {
            var check = item.IsComplete ? "✓" : "○";
            return $" [{check}] {item.Title}";
        }
    }

    private record TodoItem(string Title, bool IsComplete);

    private static Hex1bWidget BuildResponsiveTodoWidget(RootContext ctx, TodoState state)
    {
        var listItems = state.GetListItems();
        var completedCount = state.Items.Count(i => i.IsComplete);
        var totalCount = state.Items.Count;
        var todoCount = totalCount - completedCount;

        return ctx.Responsive(r => [
            // Extra wide layout (150+ cols): Three columns with stats
            r.WhenMinWidth(150, r => r.HStack(h => [
                h.Border(b => [
                    b.Text("📋 Todo Items"),
                    b.Text(""),
                    b.List(listItems)
                        .OnSelectionChanged(e => state.SelectedIndex = e.SelectedIndex)
                        .OnItemActivated(_ => state.ToggleSelected()),
                    b.Text(""),
                    b.Text("↑↓ Navigate  Space: Toggle")
                ], title: "Tasks").FillWidth(2),
                
                h.Border(b => [
                    b.Text("➕ Add New Task"),
                    b.Text(""),
                    b.TextBox(state.NewItemText).OnTextChanged(args => state.NewItemText = args.NewText),
                    b.Text(""),
                    b.Button("Add Task").OnClick(_ => state.AddItem()),
                    b.Text(""),
                    b.Text("Type and click Add")
                ], title: "New Task").FillWidth(1),
                
                h.Border(b => [
                    b.Text("📊 Statistics"),
                    b.Text(""),
                    b.Text($"Total: {totalCount} items"),
                    b.Text($"Done:  {completedCount} ✓"),
                    b.Text($"Todo:  {todoCount} ○"),
                    b.Text(""),
                    b.Text($"Progress: {GetProgressBar(state)}")
                ], title: "Stats").FillWidth(1)
            ])),
            
            // Wide layout (110+ cols): Two columns
            r.WhenMinWidth(110, r => r.HStack(h => [
                h.Border(b => [
                    b.Text("📋 Todo Items"),
                    b.Text(""),
                    b.List(listItems)
                        .OnSelectionChanged(e => state.SelectedIndex = e.SelectedIndex)
                        .OnItemActivated(_ => state.ToggleSelected()),
                    b.Text(""),
                    b.Text("↑↓ Nav  Space: Toggle")
                ], title: "Tasks").FillWidth(2),
                
                h.VStack(v => [
                    v.Border(b => [
                        b.Text("➕ Add Task"),
                        b.TextBox(state.NewItemText).OnTextChanged(args => state.NewItemText = args.NewText),
                        b.Button("Add").OnClick(_ => state.AddItem())
                    ], title: "New"),
                    v.Border(b => [
                        b.Text($"Done: {completedCount}/{totalCount}"),
                        b.Text(GetProgressBar(state))
                    ], title: "Progress").FillHeight()
                ]).FillWidth(1)
            ])),
            
            // Medium layout (70+ cols): Single column with all features
            r.WhenMinWidth(70, r => r.VStack(v => [
                v.Border(b => [
                    b.Text("📋 Responsive Todo List"),
                    b.Text($"[{completedCount}/{totalCount} complete]")
                ], title: "Todo"),
                
                v.Border(b => [
                    b.List(listItems)
                        .OnSelectionChanged(e => state.SelectedIndex = e.SelectedIndex)
                        .OnItemActivated(_ => state.ToggleSelected())
                ], title: "Items").FillHeight(),
                
                v.HStack(h => [
                    h.TextBox(state.NewItemText).OnTextChanged(args => state.NewItemText = args.NewText).FillWidth(),
                    h.Button("[+]").OnClick(_ => state.AddItem())
                ]),
                
                v.Text("↑↓:Move  Space:Toggle  Tab:Focus")
            ])),
            
            // Compact layout (< 70 cols): Minimal display
            r.Otherwise(r => r.VStack(v => [
                v.Text($"Todo [{completedCount}/{totalCount}]"),
                v.Text("────────────────────"),
                v.List(listItems)
                    .OnSelectionChanged(e => state.SelectedIndex = e.SelectedIndex)
                    .OnItemActivated(_ => state.ToggleSelected())
                    .FillHeight(),
                v.Text("────────────────────"),
                v.TextBox(state.NewItemText).OnTextChanged(args => state.NewItemText = args.NewText),
                v.Button("+ Add").OnClick(_ => state.AddItem())
            ]))
        ]);
    }

    private static string GetProgressBar(TodoState state)
    {
        if (state.Items.Count == 0) return "[          ] 0%";
        
        var percent = state.Items.Count(i => i.IsComplete) * 100 / state.Items.Count;
        var filled = percent / 10;
        var bar = new string('█', filled) + new string('░', 10 - filled);
        return $"[{bar}] {percent}%";
    }
}
