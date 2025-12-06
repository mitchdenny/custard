using System.Threading.Channels;

namespace Custard;

// ============================================================================
// Terminal Abstraction - Decouples the framework from Console
// ============================================================================

/// <summary>
/// Represents an input event from the terminal.
/// </summary>
public abstract record CustardInputEvent;

public sealed record KeyInputEvent(ConsoleKey Key, char KeyChar, bool Shift, bool Alt, bool Control) : CustardInputEvent;

/// <summary>
/// Abstraction for terminal output operations.
/// Implementations can target Console, websockets, test harnesses, etc.
/// </summary>
public interface ICustardTerminalOutput
{
    void Write(string text);
    void Clear();
    void SetCursorPosition(int left, int top);
    void EnterAlternateScreen();
    void ExitAlternateScreen();
    int Width { get; }
    int Height { get; }
}

/// <summary>
/// Abstraction for terminal input operations.
/// Implementations can target Console, websockets, test harnesses, etc.
/// </summary>
public interface ICustardTerminalInput
{
    /// <summary>
    /// Reads input events asynchronously. The channel completes when the terminal closes.
    /// </summary>
    ChannelReader<CustardInputEvent> InputEvents { get; }
}

/// <summary>
/// Combined terminal interface for convenience.
/// </summary>
public interface ICustardTerminal : ICustardTerminalOutput, ICustardTerminalInput
{
}

/// <summary>
/// Console-based terminal implementation.
/// </summary>
public sealed class ConsoleCustardTerminal : ICustardTerminal, IDisposable
{
    private const string EnterAlternateBuffer = "\x1b[?1049h";
    private const string ExitAlternateBuffer = "\x1b[?1049l";
    private const string ClearScreen = "\x1b[2J";
    private const string MoveCursorHome = "\x1b[H";

    private readonly Channel<CustardInputEvent> _inputChannel;
    private readonly CancellationTokenSource _inputLoopCts;
    private readonly Task _inputLoopTask;

    public ConsoleCustardTerminal()
    {
        _inputChannel = Channel.CreateUnbounded<CustardInputEvent>();
        _inputLoopCts = new CancellationTokenSource();
        _inputLoopTask = Task.Run(() => ReadInputLoopAsync(_inputLoopCts.Token));
    }

    public ChannelReader<CustardInputEvent> InputEvents => _inputChannel.Reader;

    public int Width => Console.WindowWidth;
    public int Height => Console.WindowHeight;

    public void Write(string text) => Console.Write(text);

    public void Clear()
    {
        Console.Write(ClearScreen);
        Console.Write(MoveCursorHome);
    }

    public void SetCursorPosition(int left, int top) => Console.SetCursorPosition(left, top);

    public void EnterAlternateScreen()
    {
        Console.Write(EnterAlternateBuffer);
        Console.Write(ClearScreen);
        Console.Write(MoveCursorHome);
    }

    public void ExitAlternateScreen() => Console.Write(ExitAlternateBuffer);

    private async Task ReadInputLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Console.KeyAvailable is non-blocking check
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(intercept: true);
                    var evt = new KeyInputEvent(
                        keyInfo.Key,
                        keyInfo.KeyChar,
                        (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0,
                        (keyInfo.Modifiers & ConsoleModifiers.Alt) != 0,
                        (keyInfo.Modifiers & ConsoleModifiers.Control) != 0
                    );
                    await _inputChannel.Writer.WriteAsync(evt, cancellationToken);
                }
                else
                {
                    // Small delay to avoid busy-waiting
                    await Task.Delay(10, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        finally
        {
            _inputChannel.Writer.Complete();
        }
    }

    public void Dispose()
    {
        _inputLoopCts.Cancel();
        _inputLoopCts.Dispose();
        // Don't await the task in Dispose, just let it complete
    }
}

// ============================================================================
// Widgets - The declarative description of UI (like React elements / VDOM)
// ============================================================================

public abstract record CustardWidget;

public sealed record TextBlockWidget(string Text) : CustardWidget;

public static class CustardWidgets
{
    public static Task<CustardWidget> TextBlockAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult<CustardWidget>(new TextBlockWidget(text));
}

// ============================================================================
// Nodes - The actual rendered state (like the real DOM)
// ============================================================================

public abstract class CustardNode
{
    public abstract void Render(CustardRenderContext context);
}

public sealed class TextBlockNode : CustardNode
{
    public string Text { get; set; } = "";

    public override void Render(CustardRenderContext context)
    {
        context.Write(Text);
    }
}

// ============================================================================
// Render Context - Abstraction for writing to the terminal
// ============================================================================

public class CustardRenderContext
{
    private readonly ICustardTerminalOutput _output;

    public CustardRenderContext(ICustardTerminalOutput output)
    {
        _output = output;
    }

    public void EnterAlternateScreen() => _output.EnterAlternateScreen();
    public void ExitAlternateScreen() => _output.ExitAlternateScreen();
    public void Write(string text) => _output.Write(text);
    public void Clear() => _output.Clear();
    public void SetCursorPosition(int left, int top) => _output.SetCursorPosition(left, top);
    public int Width => _output.Width;
    public int Height => _output.Height;
}

// ============================================================================
// App - The orchestrator that runs the render loop
// ============================================================================

public class CustardApp : IDisposable
{
    private readonly Func<CancellationToken, Task<CustardWidget>> _rootComponent;
    private readonly ICustardTerminal _terminal;
    private readonly CustardRenderContext _context;
    private readonly bool _ownsTerminal;
    private CustardNode? _rootNode;

    /// <summary>
    /// Creates a CustardApp with a custom terminal implementation.
    /// </summary>
    public CustardApp(Func<CancellationToken, Task<CustardWidget>> rootComponent, ICustardTerminal terminal, bool ownsTerminal = false)
    {
        _rootComponent = rootComponent;
        _terminal = terminal;
        _context = new CustardRenderContext(terminal);
        _ownsTerminal = ownsTerminal;
    }

    /// <summary>
    /// Creates a CustardApp with the default console terminal.
    /// </summary>
    public CustardApp(Func<CancellationToken, Task<CustardWidget>> rootComponent)
        : this(rootComponent, new ConsoleCustardTerminal(), ownsTerminal: true)
    {
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _context.EnterAlternateScreen();
        try
        {
            // Main render loop
            while (!cancellationToken.IsCancellationRequested)
            {
                // Process any pending input events
                while (_terminal.InputEvents.TryRead(out var inputEvent))
                {
                    // For now, just ignore input. Later we'll dispatch to focused widgets.
                }

                // Render the current frame
                await RenderFrameAsync(cancellationToken);

                // Delay to control frame rate (~60 FPS)
                await Task.Delay(16, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal cancellation, exit gracefully
        }
        finally
        {
            // Always exit alternate buffer, even on error
            _context.ExitAlternateScreen();
        }
    }

    private async Task RenderFrameAsync(CancellationToken cancellationToken)
    {
        // Step 1: Call the root component to get the widget tree
        var widgetTree = await _rootComponent(cancellationToken);

        // Step 2: Reconcile - update the node tree to match the widget tree
        _rootNode = Reconcile(_rootNode, widgetTree);

        // Step 3: Render the node tree to the terminal
        _context.Clear();
        _rootNode?.Render(_context);
    }

    /// <summary>
    /// Reconciles a node with a widget, creating/updating/replacing as needed.
    /// This is the core of the "diffing" algorithm.
    /// </summary>
    private static CustardNode? Reconcile(CustardNode? existingNode, CustardWidget? widget)
    {
        if (widget is null)
        {
            return null;
        }

        // For now, simple reconciliation:
        // If the node type matches the widget type, update it.
        // Otherwise, create a new node.

        return widget switch
        {
            TextBlockWidget textWidget => ReconcileTextBlock(existingNode as TextBlockNode, textWidget),
            _ => throw new NotSupportedException($"Unknown widget type: {widget.GetType()}")
        };
    }

    private static TextBlockNode ReconcileTextBlock(TextBlockNode? existingNode, TextBlockWidget widget)
    {
        var node = existingNode ?? new TextBlockNode();
        node.Text = widget.Text;
        return node;
    }

    public void Dispose()
    {
        if (_ownsTerminal && _terminal is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}