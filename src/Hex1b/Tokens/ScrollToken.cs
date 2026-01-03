namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI Scroll Up (SU) command: ESC [ n S
/// Scrolls the content up by n lines, inserting blank lines at the bottom.
/// </summary>
/// <param name="Count">Number of lines to scroll. Default is 1.</param>
public sealed record ScrollUpToken(int Count = 1) : AnsiToken;

/// <summary>
/// Represents a CSI Scroll Down (SD) command: ESC [ n T
/// Scrolls the content down by n lines, inserting blank lines at the top.
/// </summary>
/// <param name="Count">Number of lines to scroll. Default is 1.</param>
public sealed record ScrollDownToken(int Count = 1) : AnsiToken;

/// <summary>
/// Represents a CSI Insert Line (IL) command: ESC [ n L
/// Inserts n blank lines at the cursor position, pushing existing lines down.
/// </summary>
/// <param name="Count">Number of lines to insert. Default is 1.</param>
public sealed record InsertLinesToken(int Count = 1) : AnsiToken;

/// <summary>
/// Represents a CSI Delete Line (DL) command: ESC [ n M
/// Deletes n lines at the cursor position, pulling lines up from below.
/// </summary>
/// <param name="Count">Number of lines to delete. Default is 1.</param>
public sealed record DeleteLinesToken(int Count = 1) : AnsiToken;

/// <summary>
/// Represents an Index (IND) command: ESC D
/// Moves the cursor down one line, scrolling if at the bottom margin.
/// </summary>
public sealed record IndexToken : AnsiToken
{
    /// <summary>Singleton instance.</summary>
    public static readonly IndexToken Instance = new();
    private IndexToken() { }
}

/// <summary>
/// Represents a Reverse Index (RI) command: ESC M
/// Moves the cursor up one line, scrolling if at the top margin.
/// </summary>
public sealed record ReverseIndexToken : AnsiToken
{
    /// <summary>Singleton instance.</summary>
    public static readonly ReverseIndexToken Instance = new();
    private ReverseIndexToken() { }
}
