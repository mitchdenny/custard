using Hex1b.Theming;

namespace Hex1b;

public class Hex1bRenderContext
{
    private readonly IHex1bTerminalOutput _output;

    public Hex1bRenderContext(IHex1bTerminalOutput output, Hex1bTheme? theme = null)
    {
        _output = output;
        Theme = theme ?? Hex1bThemes.Default;
    }

    public Hex1bTheme Theme { get; set; }
    
    /// <summary>
    /// The inherited foreground color from parent containers (e.g., Panel).
    /// Nodes should use this when rendering text if they don't have their own color.
    /// </summary>
    public Hex1bColor InheritedForeground { get; set; } = Hex1bColor.Default;
    
    /// <summary>
    /// The inherited background color from parent containers (e.g., Panel).
    /// Nodes should use this when rendering to maintain visual continuity.
    /// </summary>
    public Hex1bColor InheritedBackground { get; set; } = Hex1bColor.Default;

    /// <summary>
    /// Gets the ANSI codes to apply inherited colors, or empty string if default.
    /// </summary>
    public string GetInheritedColorCodes()
    {
        var result = "";
        if (!InheritedForeground.IsDefault)
            result += InheritedForeground.ToForegroundAnsi();
        if (!InheritedBackground.IsDefault)
            result += InheritedBackground.ToBackgroundAnsi();
        return result;
    }
    
    /// <summary>
    /// Gets the ANSI codes to reset colors back to inherited values (or default if none).
    /// Use this after applying temporary color changes.
    /// </summary>
    public string GetResetToInheritedCodes()
    {
        if (InheritedForeground.IsDefault && InheritedBackground.IsDefault)
            return "\x1b[0m";
        
        var result = "\x1b[0m"; // Reset all first
        if (!InheritedForeground.IsDefault)
            result += InheritedForeground.ToForegroundAnsi();
        if (!InheritedBackground.IsDefault)
            result += InheritedBackground.ToBackgroundAnsi();
        return result;
    }

    public void EnterAlternateScreen() => _output.EnterAlternateScreen();
    public void ExitAlternateScreen() => _output.ExitAlternateScreen();
    public void Write(string text) => _output.Write(text);
    public void Clear() => _output.Clear();
    public void SetCursorPosition(int left, int top) => _output.SetCursorPosition(left, top);
    public int Width => _output.Width;
    public int Height => _output.Height;
}
