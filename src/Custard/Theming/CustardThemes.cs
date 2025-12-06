namespace Custard.Theming;

/// <summary>
/// Provides pre-built themes for Custard applications.
/// </summary>
public static class CustardThemes
{
    /// <summary>
    /// The default theme with minimal styling.
    /// </summary>
    public static CustardTheme Default { get; } = CreateDefaultTheme();

    /// <summary>
    /// A dark theme with blue accents.
    /// </summary>
    public static CustardTheme Ocean { get; } = CreateOceanTheme();

    /// <summary>
    /// A high-contrast theme.
    /// </summary>
    public static CustardTheme HighContrast { get; } = CreateHighContrastTheme();

    /// <summary>
    /// A warm theme with orange/red accents.
    /// </summary>
    public static CustardTheme Sunset { get; } = CreateSunsetTheme();

    private static CustardTheme CreateDefaultTheme()
    {
        return new CustardTheme("Default");
        // Uses all default values from theme elements
    }

    private static CustardTheme CreateOceanTheme()
    {
        return new CustardTheme("Ocean")
            // Buttons
            .Set(ButtonTheme.FocusedForegroundColor, CustardColor.White)
            .Set(ButtonTheme.FocusedBackgroundColor, CustardColor.FromRgb(0, 100, 180))
            // TextBox
            .Set(TextBoxTheme.CursorForegroundColor, CustardColor.Black)
            .Set(TextBoxTheme.CursorBackgroundColor, CustardColor.FromRgb(100, 200, 255))
            .Set(TextBoxTheme.SelectionBackgroundColor, CustardColor.FromRgb(0, 80, 140))
            .Set(TextBoxTheme.SelectionForegroundColor, CustardColor.White)
            // List
            .Set(ListTheme.SelectedForegroundColor, CustardColor.White)
            .Set(ListTheme.SelectedBackgroundColor, CustardColor.FromRgb(0, 100, 180))
            // Splitter
            .Set(SplitterTheme.DividerColor, CustardColor.FromRgb(0, 120, 200));
    }

    private static CustardTheme CreateHighContrastTheme()
    {
        return new CustardTheme("HighContrast")
            // Buttons
            .Set(ButtonTheme.ForegroundColor, CustardColor.White)
            .Set(ButtonTheme.FocusedForegroundColor, CustardColor.Black)
            .Set(ButtonTheme.FocusedBackgroundColor, CustardColor.Yellow)
            // TextBox
            .Set(TextBoxTheme.CursorForegroundColor, CustardColor.Black)
            .Set(TextBoxTheme.CursorBackgroundColor, CustardColor.Yellow)
            .Set(TextBoxTheme.SelectionBackgroundColor, CustardColor.Yellow)
            .Set(TextBoxTheme.SelectionForegroundColor, CustardColor.Black)
            // List
            .Set(ListTheme.SelectedForegroundColor, CustardColor.Black)
            .Set(ListTheme.SelectedBackgroundColor, CustardColor.Yellow)
            .Set(ListTheme.SelectedIndicator, "► ")
            // Splitter
            .Set(SplitterTheme.DividerColor, CustardColor.White)
            .Set(SplitterTheme.DividerCharacter, "║");
    }

    private static CustardTheme CreateSunsetTheme()
    {
        return new CustardTheme("Sunset")
            // Buttons
            .Set(ButtonTheme.FocusedForegroundColor, CustardColor.White)
            .Set(ButtonTheme.FocusedBackgroundColor, CustardColor.FromRgb(200, 80, 40))
            // TextBox
            .Set(TextBoxTheme.CursorForegroundColor, CustardColor.Black)
            .Set(TextBoxTheme.CursorBackgroundColor, CustardColor.FromRgb(255, 180, 100))
            .Set(TextBoxTheme.SelectionBackgroundColor, CustardColor.FromRgb(180, 60, 30))
            .Set(TextBoxTheme.SelectionForegroundColor, CustardColor.White)
            // List
            .Set(ListTheme.SelectedForegroundColor, CustardColor.White)
            .Set(ListTheme.SelectedBackgroundColor, CustardColor.FromRgb(200, 80, 40))
            // Splitter
            .Set(SplitterTheme.DividerColor, CustardColor.FromRgb(255, 140, 60));
    }
}
