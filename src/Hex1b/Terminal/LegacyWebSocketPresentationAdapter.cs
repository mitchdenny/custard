using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Hex1b.Input;

namespace Hex1b.Terminal;

/// <summary>
/// Legacy presentation adapter that wraps a WebSocket for browser-based terminal I/O.
/// </summary>
/// <remarks>
/// This adapter extracts the presentation-side I/O from WebSocketHex1bTerminal,
/// allowing the new Hex1bTerminalCore to use a WebSocket for display while
/// maintaining backward compatibility.
/// </remarks>
public sealed class LegacyWebSocketPresentationAdapter : IHex1bTerminalPresentationAdapter
{
    private readonly WebSocket _webSocket;
    private readonly CancellationTokenSource _disposeCts = new();
    private int _width;
    private int _height;
    private bool _disposed;
    private readonly bool _enableMouse;

    /// <summary>
    /// Creates a new WebSocket presentation adapter.
    /// </summary>
    /// <param name="webSocket">The WebSocket connection to the client.</param>
    /// <param name="width">Initial terminal width in characters.</param>
    /// <param name="height">Initial terminal height in lines.</param>
    /// <param name="enableMouse">Whether to enable mouse tracking.</param>
    public LegacyWebSocketPresentationAdapter(
        WebSocket webSocket,
        int width = 80,
        int height = 24,
        bool enableMouse = false)
    {
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        _width = width;
        _height = height;
        _enableMouse = enableMouse;
    }

    /// <inheritdoc />
    public int Width => _width;

    /// <inheritdoc />
    public int Height => _height;

    /// <inheritdoc />
    public TerminalCapabilities Capabilities => new()
    {
        SupportsMouse = _enableMouse,
        SupportsTrueColor = true,
        Supports256Colors = true,
        SupportsAlternateScreen = true,
        SupportsBracketedPaste = false,
        // WebSocket terminals may support sixel if the browser terminal does
        SupportsSixel = false // Will be detected via DA1 response
    };

    /// <inheritdoc />
    public event Action<int, int>? Resized;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <inheritdoc />
    public async ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed || _webSocket.State != WebSocketState.Open)
            return;

        try
        {
            await _webSocket.SendAsync(data, WebSocketMessageType.Text, true, ct);
        }
        catch (WebSocketException)
        {
            // Connection closed
            Disconnected?.Invoke();
        }
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        if (_disposed || _webSocket.State != WebSocketState.Open)
            return ReadOnlyMemory<byte>.Empty;

        var buffer = new byte[1024];
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);

        try
        {
            var result = await _webSocket.ReceiveAsync(buffer, linkedCts.Token);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                Disconnected?.Invoke();
                return ReadOnlyMemory<byte>.Empty;
            }

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                // Try to parse as JSON control message first
                if (TryParseControlMessage(message))
                {
                    // Control message was handled, read again for actual input
                    return await ReadInputAsync(ct);
                }

                // Return the raw bytes for the terminal core to parse
                return buffer.AsMemory(0, result.Count);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (WebSocketException)
        {
            Disconnected?.Invoke();
        }

        return ReadOnlyMemory<byte>.Empty;
    }

    private bool TryParseControlMessage(string message)
    {
        if (!message.StartsWith('{'))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(message);
            if (doc.RootElement.TryGetProperty("type", out var typeElement))
            {
                var type = typeElement.GetString();
                switch (type)
                {
                    case "resize":
                        var cols = doc.RootElement.GetProperty("cols").GetInt32();
                        var rows = doc.RootElement.GetProperty("rows").GetInt32();
                        _width = cols;
                        _height = rows;
                        Resized?.Invoke(cols, rows);
                        return true;
                }
            }
        }
        catch (JsonException)
        {
            // Not a valid JSON message
        }

        return false;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken ct = default)
    {
        // WebSocket sends are already flushed per-message
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask EnterTuiModeAsync(CancellationToken ct = default)
    {
        if (_disposed || _webSocket.State != WebSocketState.Open)
            return;

        var escapes = "\x1b[?1049h\x1b[?25l";
        if (_enableMouse)
        {
            escapes += MouseParser.EnableMouseTracking;
        }
        escapes += "\x1b[2J\x1b[H";

        await WriteOutputAsync(Encoding.UTF8.GetBytes(escapes), ct);
    }

    /// <inheritdoc />
    public async ValueTask ExitTuiModeAsync(CancellationToken ct = default)
    {
        if (_disposed || _webSocket.State != WebSocketState.Open)
            return;

        var escapes = "";
        if (_enableMouse)
        {
            escapes += MouseParser.DisableMouseTracking;
        }
        escapes += "\x1b[?25h\x1b[?1049l";

        await WriteOutputAsync(Encoding.UTF8.GetBytes(escapes), ct);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        Disconnected?.Invoke();

        _disposeCts.Cancel();
        _disposeCts.Dispose();

        if (_webSocket.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposed", CancellationToken.None);
            }
            catch (WebSocketException)
            {
                // Ignore close errors
            }
        }
    }
}
