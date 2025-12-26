namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Hyperlink widgets.
/// </summary>
public static class HyperlinkTheme
{
    /// <summary>
    /// Foreground color for normal (unfocused) hyperlinks.
    /// Defaults to blue, the traditional hyperlink color.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new($"{nameof(HyperlinkTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.FromRgb(0, 120, 212)); // Blue
    
    /// <summary>
    /// Foreground color when the hyperlink is focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedForegroundColor = 
        new($"{nameof(HyperlinkTheme)}.{nameof(FocusedForegroundColor)}", () => Hex1bColor.FromRgb(0, 180, 255)); // Bright cyan
    
    /// <summary>
    /// Foreground color when the hyperlink is hovered.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HoveredForegroundColor = 
        new($"{nameof(HyperlinkTheme)}.{nameof(HoveredForegroundColor)}", () => Hex1bColor.FromRgb(100, 160, 255)); // Light blue
}

