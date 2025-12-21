using System.Runtime.InteropServices;
using System.Text;
using Hex1b.Input;

namespace Hex1b.Terminal;

/// <summary>
/// Legacy presentation adapter that wraps System.Console for I/O.
/// </summary>
/// <remarks>
/// This adapter extracts the presentation-side I/O from ConsoleHex1bTerminal,
/// allowing the new Hex1bTerminalCore to use the console for display while
/// maintaining backward compatibility.
/// </remarks>
public sealed class LegacyConsolePresentationAdapter : IHex1bTerminalPresentationAdapter
{
    private const string EnterAlternateBuffer = "\x1b[?1049h";
    private const string ExitAlternateBuffer = "\x1b[?1049l";
    private const string ClearScreen = "\x1b[2J";
    private const string MoveCursorHome = "\x1b[H";
    private const string HideCursor = "\x1b[?25l";
    private const string ShowCursor = "\x1b[?25h";

    private readonly bool _enableMouse;
    private readonly CancellationTokenSource _disposeCts = new();
    private PosixSignalRegistration? _sigwinchRegistration;
    private int _lastWidth;
    private int _lastHeight;
    private bool _disposed;
    private bool _inTuiMode;

    /// <summary>
    /// Creates a new console presentation adapter.
    /// </summary>
    /// <param name="enableMouse">Whether to enable mouse tracking.</param>
    public LegacyConsolePresentationAdapter(bool enableMouse = false)
    {
        _enableMouse = enableMouse;
        _lastWidth = Console.WindowWidth;
        _lastHeight = Console.WindowHeight;

        // Register for SIGWINCH on supported platforms
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            _sigwinchRegistration = PosixSignalRegistration.Create(PosixSignal.SIGWINCH, OnSigwinch);
        }
    }

    private void OnSigwinch(PosixSignalContext context)
    {
        context.Cancel = false;

        var newWidth = Console.WindowWidth;
        var newHeight = Console.WindowHeight;

        if (newWidth != _lastWidth || newHeight != _lastHeight)
        {
            _lastWidth = newWidth;
            _lastHeight = newHeight;
            Resized?.Invoke(newWidth, newHeight);
        }
    }

    /// <inheritdoc />
    public int Width => Console.WindowWidth;

    /// <inheritdoc />
    public int Height => Console.WindowHeight;

    /// <inheritdoc />
    public TerminalCapabilities Capabilities => new()
    {
        SupportsMouse = _enableMouse,
        SupportsTrueColor = true,
        Supports256Colors = true,
        SupportsAlternateScreen = true,
        SupportsBracketedPaste = false
    };

    /// <inheritdoc />
    public event Action<int, int>? Resized;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <inheritdoc />
    public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed) return ValueTask.CompletedTask;

        var text = Encoding.UTF8.GetString(data.Span);
        Console.Write(text);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        if (_disposed) return ReadOnlyMemory<byte>.Empty;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);

        try
        {
            // Read input using Console.ReadKey with escape sequence handling
            var buffer = new StringBuilder();

            while (!linkedCts.Token.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(intercept: true);

                    // Check for escape character (start of escape sequence)
                    if (keyInfo.Key == ConsoleKey.Escape || keyInfo.KeyChar == '\x1b')
                    {
                        buffer.Append('\x1b');

                        // Read the rest of the escape sequence with a small timeout
                        var sequenceStart = DateTime.UtcNow;
                        while ((DateTime.UtcNow - sequenceStart).TotalMilliseconds < 50)
                        {
                            if (Console.KeyAvailable)
                            {
                                var nextKey = Console.ReadKey(intercept: true);
                                buffer.Append(nextKey.KeyChar);

                                // Check for sequence terminators
                                if (IsSgrMouseTerminator(nextKey.KeyChar) ||
                                    IsOtherCsiTerminator(buffer.ToString(), nextKey.KeyChar))
                                {
                                    break;
                                }
                            }
                            else
                            {
                                await Task.Delay(1, linkedCts.Token);
                            }
                        }

                        // Return whatever we've collected
                        return Encoding.UTF8.GetBytes(buffer.ToString());
                    }
                    else
                    {
                        // Regular key - encode as appropriate
                        return EncodeKeyPress(keyInfo);
                    }
                }
                else
                {
                    // Small delay to avoid busy-waiting
                    await Task.Delay(10, linkedCts.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }

        return ReadOnlyMemory<byte>.Empty;
    }

    private static bool IsSgrMouseTerminator(char c) => c == 'M' || c == 'm';

    private static bool IsOtherCsiTerminator(string sequence, char c)
    {
        // CSI sequences end with a letter (except for SGR mouse which ends with M/m)
        if (sequence.Length >= 2 && sequence[1] == '[')
        {
            return char.IsLetter(c) && c != '<'; // '<' starts SGR mouse params
        }
        return false;
    }

    private static ReadOnlyMemory<byte> EncodeKeyPress(ConsoleKeyInfo keyInfo)
    {
        // For regular characters, just return the character
        if (keyInfo.KeyChar != '\0' && !char.IsControl(keyInfo.KeyChar))
        {
            return Encoding.UTF8.GetBytes(keyInfo.KeyChar.ToString());
        }

        // Map special keys to ANSI sequences
        var sequence = keyInfo.Key switch
        {
            ConsoleKey.Enter => "\r",
            ConsoleKey.Tab => "\t",
            ConsoleKey.Backspace => "\x7f",
            ConsoleKey.UpArrow => "\x1b[A",
            ConsoleKey.DownArrow => "\x1b[B",
            ConsoleKey.RightArrow => "\x1b[C",
            ConsoleKey.LeftArrow => "\x1b[D",
            ConsoleKey.Home => "\x1b[H",
            ConsoleKey.End => "\x1b[F",
            ConsoleKey.Insert => "\x1b[2~",
            ConsoleKey.Delete => "\x1b[3~",
            ConsoleKey.PageUp => "\x1b[5~",
            ConsoleKey.PageDown => "\x1b[6~",
            ConsoleKey.F1 => "\x1bOP",
            ConsoleKey.F2 => "\x1bOQ",
            ConsoleKey.F3 => "\x1bOR",
            ConsoleKey.F4 => "\x1bOS",
            ConsoleKey.F5 => "\x1b[15~",
            ConsoleKey.F6 => "\x1b[17~",
            ConsoleKey.F7 => "\x1b[18~",
            ConsoleKey.F8 => "\x1b[19~",
            ConsoleKey.F9 => "\x1b[20~",
            ConsoleKey.F10 => "\x1b[21~",
            ConsoleKey.F11 => "\x1b[23~",
            ConsoleKey.F12 => "\x1b[24~",
            _ when keyInfo.KeyChar != '\0' => keyInfo.KeyChar.ToString(),
            _ => null
        };

        return sequence != null
            ? Encoding.UTF8.GetBytes(sequence)
            : ReadOnlyMemory<byte>.Empty;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken ct = default)
    {
        Console.Out.Flush();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask EnterTuiModeAsync(CancellationToken ct = default)
    {
        if (_inTuiMode) return ValueTask.CompletedTask;
        _inTuiMode = true;

        Console.TreatControlCAsInput = true;
        Console.Write(EnterAlternateBuffer);
        Console.Write(HideCursor);
        if (_enableMouse)
        {
            Console.Write(MouseParser.EnableMouseTracking);
        }
        Console.Write(ClearScreen);
        Console.Write(MoveCursorHome);
        Console.Out.Flush();

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ExitTuiModeAsync(CancellationToken ct = default)
    {
        if (!_inTuiMode) return ValueTask.CompletedTask;
        _inTuiMode = false;

        if (_enableMouse)
        {
            Console.Write(MouseParser.DisableMouseTracking);
        }
        Console.Write(ShowCursor);
        Console.Write(ExitAlternateBuffer);
        Console.Out.Flush();

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        Disconnected?.Invoke();

        if (_inTuiMode)
        {
            await ExitTuiModeAsync();
        }

        _sigwinchRegistration?.Dispose();
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}
