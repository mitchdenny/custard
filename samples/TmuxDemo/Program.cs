using System.Text;
using Hex1b.Terminal;

// TmuxDemo - Demonstrates launching tmux via the native PTY workload adapter
// Run with: dotnet run --project samples/TmuxDemo
//
// This sample showcases the new native PTY spawner which properly sets up
// setsid/TIOCSCTTY, allowing tmux and other terminal multiplexers to work.
//
// Prerequisites:
// - Linux or macOS
// - tmux installed (apt install tmux / brew install tmux)

if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
{
    Console.WriteLine("This demo requires Linux or macOS.");
    return 1;
}

// Check if tmux is available
var tmuxPath = FindExecutable("tmux");
if (tmuxPath == null)
{
    Console.WriteLine("tmux is not installed. Please install it first:");
    Console.WriteLine("  Ubuntu/Debian: sudo apt install tmux");
    Console.WriteLine("  macOS: brew install tmux");
    return 1;
}

Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                    Hex1b Tmux Demo                               ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
Console.WriteLine("║ This demo launches tmux using the native PTY workload adapter.   ║");
Console.WriteLine("║                                                                   ║");
Console.WriteLine("║ The new native spawner properly establishes a controlling         ║");
Console.WriteLine("║ terminal via setsid/TIOCSCTTY, which is required for tmux.       ║");
Console.WriteLine("║                                                                   ║");
Console.WriteLine("║ Controls:                                                         ║");
Console.WriteLine("║   Ctrl+B, %    Split pane vertically                             ║");
Console.WriteLine("║   Ctrl+B, \"    Split pane horizontally                           ║");
Console.WriteLine("║   Ctrl+B, ←→   Switch between panes                              ║");
Console.WriteLine("║   Ctrl+B, c    Create new window                                 ║");
Console.WriteLine("║   Ctrl+B, d    Detach (exits demo)                               ║");
Console.WriteLine("║   exit         Exit the shell                                    ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("Press any key to start tmux...");
Console.ReadKey(true);

// Get terminal size
var width = Console.WindowWidth > 0 ? Console.WindowWidth : 120;
var height = Console.WindowHeight > 0 ? Console.WindowHeight : 40;

try
{
    // Create a child process that launches tmux
    // Using 'tmux new-session' to start a fresh session
    await using var process = new Hex1bTerminalChildProcess(
        tmuxPath,
        ["new-session", "-A", "-s", "hex1b-demo"],  // Attach-or-create session named 'hex1b-demo'
        workingDirectory: Environment.CurrentDirectory,
        inheritEnvironment: true,
        initialWidth: width,
        initialHeight: height
    );
    
    // Start the process with PTY attached
    await process.StartAsync();
    
    Console.WriteLine($"[Tmux started with PID {process.ProcessId}]");
    Console.WriteLine();
    
    // Create terminal options with console presentation adapter
    var presentation = new ConsolePresentationAdapter(enableMouse: true);
    
    var terminalOptions = new Hex1bTerminalOptions
    {
        Width = width,
        Height = height,
        PresentationAdapter = presentation,
        WorkloadAdapter = process
    };
    
    // Create the terminal that bridges the console ↔ tmux process
    using var terminal = new Hex1bTerminal(terminalOptions);
    
    // Wait for the process to exit (user detaches or exits)
    var cts = new CancellationTokenSource();
    
    // Handle Ctrl+C gracefully
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };
    
    try
    {
        var exitCode = await process.WaitForExitAsync(cts.Token);
        Console.WriteLine();
        Console.WriteLine($"[Tmux exited with code {exitCode}]");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine();
        Console.WriteLine("[Interrupted]");
        process.Kill();
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    return 1;
}

Console.WriteLine("Demo finished. Press any key to exit...");
Console.ReadKey(true);
return 0;

// Helper to find an executable in PATH
static string? FindExecutable(string name)
{
    var pathEnv = Environment.GetEnvironmentVariable("PATH");
    if (string.IsNullOrEmpty(pathEnv))
        return null;
    
    var paths = pathEnv.Split(':');
    foreach (var path in paths)
    {
        var fullPath = Path.Combine(path, name);
        if (File.Exists(fullPath))
            return fullPath;
    }
    
    return null;
}
