using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b.Terminal;

/// <summary>
/// A presentation filter that optimizes output by only transmitting cells that have changed.
/// </summary>
/// <remarks>
/// <para>
/// This filter maintains dual buffers for delta encoding:
/// <list type="bullet">
///   <item><b>Pending buffer</b>: Accumulates changes during the current frame</item>
///   <item><b>Committed buffer</b>: Represents what was last sent to the presentation layer</item>
/// </list>
/// </para>
/// <para>
/// When frame buffering is enabled (via <see cref="FrameBeginToken"/> and <see cref="FrameEndToken"/>),
/// updates are accumulated in the pending buffer without emitting output. When the frame ends,
/// the filter compares pending vs committed and emits only the net changes, eliminating
/// intermediate states (like clear-then-render) that cause flicker.
/// </para>
/// <para>
/// Benefits:
/// <list type="bullet">
///   <item>Reduces bandwidth for remote terminal connections</item>
///   <item>Improves rendering performance by avoiding redundant updates</item>
///   <item>Enables efficient partial screen updates</item>
///   <item>Eliminates flicker from intermediate render states</item>
/// </list>
/// </para>
/// </remarks>
public sealed class DeltaEncodingFilter : IHex1bTerminalPresentationFilter
{
    // Pending buffer: accumulates changes during the current frame
    private ShadowCell[,]? _pendingBuffer;
    
    // Committed buffer: represents what was last sent to the terminal
    private ShadowCell[,]? _committedBuffer;
    
    private int _width;
    private int _height;
    private bool _forceFullRefresh;

    /// <summary>
    /// Represents a cell in the shadow buffer, containing only the visual properties needed for comparison.
    /// </summary>
    private readonly record struct ShadowCell(
        string Character,
        Hex1bColor? Foreground,
        Hex1bColor? Background,
        CellAttributes Attributes)
    {
        public static readonly ShadowCell Empty = new(" ", null, null, CellAttributes.None);

        /// <summary>
        /// Creates a ShadowCell from a TerminalCell, extracting only the visual properties.
        /// </summary>
        public static ShadowCell FromTerminalCell(TerminalCell cell)
            => new(cell.Character, cell.Foreground, cell.Background, cell.Attributes);
    }

    /// <summary>
    /// Represents a changed cell with its position and new state.
    /// </summary>
    private readonly record struct ChangedCell(int X, int Y, ShadowCell Cell);

    /// <inheritdoc />
    public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
    {
        InitializeBuffers(width, height);
        _forceFullRefresh = true; // First frame should pass through
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(
        IReadOnlyList<AppliedToken> appliedTokens,
        TimeSpan elapsed,
        CancellationToken ct = default)
    {
        if (_pendingBuffer is null || _committedBuffer is null || _forceFullRefresh)
        {
            _forceFullRefresh = false;
            
            // Update both buffers with all impacts, then pass through tokens
            foreach (var appliedToken in appliedTokens)
            {
                foreach (var impact in appliedToken.CellImpacts)
                {
                    if (impact.X >= 0 && impact.X < _width && impact.Y >= 0 && impact.Y < _height)
                    {
                        var cell = ShadowCell.FromTerminalCell(impact.Cell);
                        _pendingBuffer![impact.Y, impact.X] = cell;
                        _committedBuffer![impact.Y, impact.X] = cell;
                    }
                }
            }
            
            // Prepend an SGR reset to ensure clean state after resize
            // This prevents leftover colors/attributes from bleeding into new content
            var result = new List<AnsiToken> { new SgrToken("0") };
            result.AddRange(appliedTokens.Select(at => at.Token));
            return ValueTask.FromResult<IReadOnlyList<AnsiToken>>(result);
        }

        // Collect control tokens that should always pass through
        var controlTokens = new List<AnsiToken>();
        var changedCells = new List<ChangedCell>();

        foreach (var appliedToken in appliedTokens)
        {
            var token = appliedToken.Token;
            
            // Handle special tokens that affect display but don't have cell impacts
            switch (token)
            {
                case ClearScreenToken clearToken:
                    // Clear screen - update both buffers and pass through
                    ClearBuffer(_pendingBuffer, clearToken.Mode);
                    ClearBuffer(_committedBuffer, clearToken.Mode);
                    controlTokens.Add(token);
                    continue;
                    
                case ClearLineToken:
                case PrivateModeToken:
                case CursorShapeToken:
                case SaveCursorToken:
                case RestoreCursorToken:
                case ScrollRegionToken:
                case OscToken:
                case DcsToken:
                    // These control tokens should pass through unchanged
                    controlTokens.Add(token);
                    continue;
                    
                case CursorPositionToken when appliedToken.CellImpacts.Count == 0:
                    // Standalone cursor positioning (e.g., for mouse cursor tracking)
                    // should pass through when not associated with cell changes
                    controlTokens.Add(token);
                    continue;
            }
            
            // Process cell impacts for content tokens
            foreach (var impact in appliedToken.CellImpacts)
            {
                // Skip out-of-bounds cells
                if (impact.X < 0 || impact.X >= _width || impact.Y < 0 || impact.Y >= _height)
                    continue;

                var newShadowCell = ShadowCell.FromTerminalCell(impact.Cell);
                var currentCommittedCell = _committedBuffer[impact.Y, impact.X];

                // Update pending buffer
                _pendingBuffer[impact.Y, impact.X] = newShadowCell;

                // Only record if the cell actually changed from committed state
                if (newShadowCell != currentCommittedCell)
                {
                    changedCells.Add(new ChangedCell(impact.X, impact.Y, newShadowCell));
                    // Update committed buffer since we're emitting immediately in Phase 2
                    _committedBuffer[impact.Y, impact.X] = newShadowCell;
                }
            }
        }

        // Build result: control tokens first, then optimized cell updates
        var outputTokens = new List<AnsiToken>(controlTokens);
        
        if (changedCells.Count > 0)
        {
            outputTokens.AddRange(GenerateTokens(changedCells));
        }

        return ValueTask.FromResult<IReadOnlyList<AnsiToken>>(outputTokens);
    }

    /// <inheritdoc />
    public ValueTask OnInputAsync(ReadOnlyMemory<byte> data, TimeSpan elapsed, CancellationToken ct = default)
    {
        // Input doesn't affect delta encoding
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
    {
        // Reinitialize both buffers on resize and force a full refresh
        InitializeBuffers(width, height);
        _forceFullRefresh = true;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
    {
        _pendingBuffer = null;
        _committedBuffer = null;
        _width = 0;
        _height = 0;
        _forceFullRefresh = false;
        return ValueTask.CompletedTask;
    }

    private void InitializeBuffers(int width, int height)
    {
        _width = width;
        _height = height;
        _pendingBuffer = new ShadowCell[height, width];
        _committedBuffer = new ShadowCell[height, width];

        // Initialize both buffers with empty cells
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                _pendingBuffer[y, x] = ShadowCell.Empty;
                _committedBuffer[y, x] = ShadowCell.Empty;
            }
        }
    }

    /// <summary>
    /// Clears a buffer based on the clear mode.
    /// </summary>
    private void ClearBuffer(ShadowCell[,]? buffer, ClearMode mode)
    {
        if (buffer is null) return;
        
        // For simplicity, treat all clear modes as full clear
        // A more sophisticated implementation could track cursor position
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                buffer[y, x] = ShadowCell.Empty;
            }
        }
    }

    /// <summary>
    /// Generates an optimized token stream for the changed cells.
    /// </summary>
    /// <remarks>
    /// Strategy:
    /// 1. Sort cells by position (row-major order) for optimal cursor movement
    /// 2. Group contiguous cells on the same row to minimize cursor repositioning
    /// 3. Track current SGR state to avoid redundant attribute sequences
    /// </remarks>
    private IReadOnlyList<AnsiToken> GenerateTokens(List<ChangedCell> changedCells)
    {
        if (changedCells.Count == 0)
            return Array.Empty<AnsiToken>();

        var tokens = new List<AnsiToken>();

        // Sort by Y, then X for optimal cursor movement
        changedCells.Sort((a, b) =>
        {
            var yCompare = a.Y.CompareTo(b.Y);
            return yCompare != 0 ? yCompare : a.X.CompareTo(b.X);
        });

        // Track current state to minimize redundant tokens
        int cursorX = -1;
        int cursorY = -1;
        Hex1bColor? currentFg = null;
        Hex1bColor? currentBg = null;
        CellAttributes currentAttrs = CellAttributes.None;
        bool stateUnknown = true; // Start with unknown state - need to emit reset first

        foreach (var cell in changedCells)
        {
            // Position cursor if needed
            if (cell.Y != cursorY || cell.X != cursorX)
            {
                // ANSI cursor position is 1-based
                tokens.Add(new CursorPositionToken(cell.Y + 1, cell.X + 1));
                cursorY = cell.Y;
                cursorX = cell.X;
            }

            // Generate SGR if attributes changed or state is unknown
            var needsSgr = stateUnknown ||
                           !Equals(cell.Cell.Foreground, currentFg) ||
                           !Equals(cell.Cell.Background, currentBg) ||
                           cell.Cell.Attributes != currentAttrs;

            if (needsSgr)
            {
                var sgrParams = BuildSgrParameters(cell.Cell, stateUnknown, ref currentFg, ref currentBg, ref currentAttrs);
                tokens.Add(new SgrToken(sgrParams));
                stateUnknown = false;
            }

            // Output the character
            tokens.Add(new TextToken(cell.Cell.Character));
            cursorX++; // Cursor advances after text output
        }

        return tokens;
    }

    /// <summary>
    /// Builds SGR parameters for transitioning to the target cell's attributes.
    /// </summary>
    private static string BuildSgrParameters(
        ShadowCell targetCell,
        bool stateUnknown,
        ref Hex1bColor? currentFg,
        ref Hex1bColor? currentBg,
        ref CellAttributes currentAttrs)
    {
        var parts = new List<string>();

        // Check if we need a reset first
        // Reset if: state is unknown, OR turning OFF any attributes, OR clearing colors
        var turnedOff = currentAttrs & ~targetCell.Attributes;
        bool needsReset = stateUnknown ||
                         turnedOff != CellAttributes.None ||
                         (currentFg is not null && targetCell.Foreground is null) ||
                         (currentBg is not null && targetCell.Background is null);

        if (needsReset)
        {
            parts.Add("0"); // Reset to defaults
            currentAttrs = CellAttributes.None;
            currentFg = null;
            currentBg = null;
        }

        // Add attributes that need to be turned on
        var toTurnOn = targetCell.Attributes & ~currentAttrs;

        if ((toTurnOn & CellAttributes.Bold) != 0)
            parts.Add("1");
        if ((toTurnOn & CellAttributes.Dim) != 0)
            parts.Add("2");
        if ((toTurnOn & CellAttributes.Italic) != 0)
            parts.Add("3");
        if ((toTurnOn & CellAttributes.Underline) != 0)
            parts.Add("4");
        if ((toTurnOn & CellAttributes.Blink) != 0)
            parts.Add("5");
        if ((toTurnOn & CellAttributes.Reverse) != 0)
            parts.Add("7");
        if ((toTurnOn & CellAttributes.Hidden) != 0)
            parts.Add("8");
        if ((toTurnOn & CellAttributes.Strikethrough) != 0)
            parts.Add("9");
        if ((toTurnOn & CellAttributes.Overline) != 0)
            parts.Add("53");

        // Add foreground color if different
        if (!Equals(targetCell.Foreground, currentFg) && targetCell.Foreground is not null)
        {
            parts.Add(BuildColorSgr(targetCell.Foreground.Value, isForeground: true));
        }

        // Add background color if different
        if (!Equals(targetCell.Background, currentBg) && targetCell.Background is not null)
        {
            parts.Add(BuildColorSgr(targetCell.Background.Value, isForeground: false));
        }
        // Update tracked state
        currentAttrs = targetCell.Attributes;
        currentFg = targetCell.Foreground;
        currentBg = targetCell.Background;

        return string.Join(";", parts);
    }

    /// <summary>
    /// Builds the SGR parameter string for a color.
    /// </summary>
    private static string BuildColorSgr(Hex1bColor color, bool isForeground)
    {
        // Use 24-bit color (SGR 38;2;r;g;b for foreground, 48;2;r;g;b for background)
        var prefix = isForeground ? "38;2" : "48;2";
        return $"{prefix};{color.R};{color.G};{color.B}";
    }
}
