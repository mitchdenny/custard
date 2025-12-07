using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Xunit;

namespace Hex1b.Tests;

public class WebSocketHex1bTerminalTests
{
    [Fact]
    public void Constructor_SetsDefaultDimensions()
    {
        // Arrange & Act
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket);

        // Assert
        Assert.Equal(80, terminal.Width);
        Assert.Equal(24, terminal.Height);
    }

    [Fact]
    public void Constructor_SetsCustomDimensions()
    {
        // Arrange & Act
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket, 120, 40);

        // Assert
        Assert.Equal(120, terminal.Width);
        Assert.Equal(40, terminal.Height);
    }

    [Fact]
    public void Resize_UpdatesDimensions()
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket, 80, 24);

        // Act
        terminal.Resize(132, 50);

        // Assert
        Assert.Equal(132, terminal.Width);
        Assert.Equal(50, terminal.Height);
    }

    [Fact]
    public void Resize_RaisesOnResizeEvent()
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket, 80, 24);
        int? receivedCols = null;
        int? receivedRows = null;
        terminal.OnResize += (cols, rows) =>
        {
            receivedCols = cols;
            receivedRows = rows;
        };

        // Act
        terminal.Resize(100, 30);

        // Assert
        Assert.Equal(100, receivedCols);
        Assert.Equal(30, receivedRows);
    }

    [Fact]
    public void Write_SendsTextToWebSocket()
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket);

        // Act
        terminal.Write("Hello, World!");

        // Assert - give a moment for the async send
        Thread.Sleep(50);
        Assert.Contains("Hello, World!", mockWebSocket.SentData);
    }

    [Fact]
    public void Clear_SendsClearSequence()
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket);

        // Act
        terminal.Clear();

        // Assert
        Thread.Sleep(50);
        Assert.Contains("\x1b[2J\x1b[H", mockWebSocket.SentData);
    }

    [Fact]
    public void SetCursorPosition_SendsAnsiSequence()
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket);

        // Act
        terminal.SetCursorPosition(10, 5);

        // Assert
        Thread.Sleep(50);
        // ANSI position is 1-based, so (10, 5) becomes row 6, col 11
        Assert.Contains("\x1b[6;11H", mockWebSocket.SentData);
    }

    [Fact]
    public void EnterAlternateScreen_SendsCorrectSequence()
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket);

        // Act
        terminal.EnterAlternateScreen();

        // Assert
        Thread.Sleep(50);
        Assert.Contains("\x1b[?1049h", mockWebSocket.SentData);
    }

    [Fact]
    public void ExitAlternateScreen_SendsCorrectSequence()
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket);

        // Act
        terminal.ExitAlternateScreen();

        // Assert
        Thread.Sleep(50);
        Assert.Contains("\x1b[?1049l", mockWebSocket.SentData);
    }

    [Theory]
    [InlineData("\x1b[A", ConsoleKey.UpArrow, false, false, false)]
    [InlineData("\x1b[B", ConsoleKey.DownArrow, false, false, false)]
    [InlineData("\x1b[C", ConsoleKey.RightArrow, false, false, false)]
    [InlineData("\x1b[D", ConsoleKey.LeftArrow, false, false, false)]
    [InlineData("\x1b[1;2A", ConsoleKey.UpArrow, true, false, false)]    // Shift+Up
    [InlineData("\x1b[1;2B", ConsoleKey.DownArrow, true, false, false)]  // Shift+Down
    [InlineData("\x1b[1;2C", ConsoleKey.RightArrow, true, false, false)] // Shift+Right
    [InlineData("\x1b[1;2D", ConsoleKey.LeftArrow, true, false, false)]  // Shift+Left
    [InlineData("\x1b[1;3C", ConsoleKey.RightArrow, false, true, false)] // Alt+Right
    [InlineData("\x1b[1;5C", ConsoleKey.RightArrow, false, false, true)] // Ctrl+Right
    [InlineData("\x1b[1;6C", ConsoleKey.RightArrow, true, false, true)]  // Shift+Ctrl+Right
    [InlineData("\x1b[H", ConsoleKey.Home, false, false, false)]
    [InlineData("\x1b[F", ConsoleKey.End, false, false, false)]
    [InlineData("\x1b[1;2H", ConsoleKey.Home, true, false, false)]       // Shift+Home
    [InlineData("\x1b[1;2F", ConsoleKey.End, true, false, false)]        // Shift+End
    public async Task ProcessInputAsync_ParsesAnsiSequences(string sequence, ConsoleKey expectedKey, bool shift, bool alt, bool control)
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        mockWebSocket.QueueMessage(sequence);
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket);
        
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        
        // Act
        var processTask = terminal.ProcessInputAsync(cts.Token);
        
        KeyInputEvent? receivedEvent = null;
        try
        {
            receivedEvent = await terminal.InputEvents.ReadAsync(cts.Token) as KeyInputEvent;
        }
        catch (OperationCanceledException) { }
        
        // Assert
        Assert.NotNull(receivedEvent);
        Assert.Equal(expectedKey, receivedEvent.Key);
        Assert.Equal(shift, receivedEvent.Shift);
        Assert.Equal(alt, receivedEvent.Alt);
        Assert.Equal(control, receivedEvent.Control);
    }

    [Theory]
    [InlineData("\x1b[3~", ConsoleKey.Delete)]
    [InlineData("\x1b[5~", ConsoleKey.PageUp)]
    [InlineData("\x1b[6~", ConsoleKey.PageDown)]
    public async Task ProcessInputAsync_ParsesTildeSequences(string sequence, ConsoleKey expectedKey)
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        mockWebSocket.QueueMessage(sequence);
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket);
        
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        
        // Act
        var processTask = terminal.ProcessInputAsync(cts.Token);
        
        KeyInputEvent? receivedEvent = null;
        try
        {
            receivedEvent = await terminal.InputEvents.ReadAsync(cts.Token) as KeyInputEvent;
        }
        catch (OperationCanceledException) { }
        
        // Assert
        Assert.NotNull(receivedEvent);
        Assert.Equal(expectedKey, receivedEvent.Key);
    }

    /// <summary>
    /// Mock WebSocket for testing that captures sent data and can queue messages to receive.
    /// </summary>
    private class MockWebSocket : WebSocket
    {
        private readonly StringBuilder _sentData = new();
        private readonly Channel<string> _receiveQueue = Channel.CreateUnbounded<string>();
        private WebSocketState _state = WebSocketState.Open;

        public string SentData => _sentData.ToString();

        public void QueueMessage(string message) => _receiveQueue.Writer.TryWrite(message);

        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => _state;
        public override string? SubProtocol => null;

        public override void Abort() => _state = WebSocketState.Aborted;

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            _state = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }

        public override void Dispose() => _state = WebSocketState.Closed;

        public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            if (_receiveQueue.Reader.TryRead(out var message))
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                Array.Copy(bytes, 0, buffer.Array!, buffer.Offset, bytes.Length);
                return new WebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, true);
            }
            
            // Wait for a message or cancellation
            try
            {
                var msg = await _receiveQueue.Reader.ReadAsync(cancellationToken);
                var bytes = Encoding.UTF8.GetBytes(msg);
                Array.Copy(bytes, 0, buffer.Array!, buffer.Offset, bytes.Length);
                return new WebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, true);
            }
            catch (OperationCanceledException)
            {
                return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
            }
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            if (_state != WebSocketState.Open)
                return Task.CompletedTask;

            var text = Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count);
            _sentData.Append(text);
            return Task.CompletedTask;
        }
    }
}
