using System.Net.WebSockets;
using Hex1b;
using Hex1b.Widgets;

namespace Gallery;

/// <summary>
/// Represents a gallery exhibit that can be displayed in the terminal gallery.
/// </summary>
public interface IGalleryExhibit
{
    /// <summary>
    /// Unique identifier for this exhibit (used in URLs).
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display title for this exhibit.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Brief description of what this exhibit demonstrates.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Source code snippet to display for this exhibit.
    /// </summary>
    string SourceCode { get; }

    /// <summary>
    /// Indicates whether this exhibit uses the Hex1b widget system.
    /// If true, CreateWidgetBuilder will be called instead of HandleSessionAsync.
    /// </summary>
    bool UsesHex1b => false;

    /// <summary>
    /// Creates the Hex1b widget builder for this exhibit.
    /// Only called if UsesHex1b returns true.
    /// </summary>
    Func<CancellationToken, Task<Hex1bWidget>>? CreateWidgetBuilder() => null;

    /// <summary>
    /// Handle a WebSocket terminal session for this exhibit.
    /// Only called if UsesHex1b returns false.
    /// </summary>
    Task HandleSessionAsync(WebSocket webSocket, TerminalSession session, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for exhibits that use the Hex1b widget system.
/// </summary>
public abstract class Hex1bExhibit : IGalleryExhibit
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract string Description { get; }
    public abstract string SourceCode { get; }

    public bool UsesHex1b => true;

    public abstract Func<CancellationToken, Task<Hex1bWidget>>? CreateWidgetBuilder();

    public Task HandleSessionAsync(WebSocket webSocket, TerminalSession session, CancellationToken cancellationToken)
    {
        // This won't be called for Hex1b exhibits
        throw new NotSupportedException("This exhibit uses Hex1b widgets. Use CreateWidgetBuilder instead.");
    }
}

/// <summary>
/// Represents a terminal session with size information.
/// </summary>
public class TerminalSession
{
    public int Cols { get; set; } = 80;
    public int Rows { get; set; } = 24;
    
    public event Action<int, int>? OnResize;

    public void Resize(int cols, int rows)
    {
        Cols = cols;
        Rows = rows;
        OnResize?.Invoke(cols, rows);
    }
}
