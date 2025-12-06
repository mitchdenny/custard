namespace Custard.Theming;

/// <summary>
/// Theme elements for Button widgets.
/// </summary>
public static class ButtonTheme
{
    public static readonly CustardThemeElement<int> MinimumWidth = 
        new("Button.MinimumWidth", () => 10);
    
    public static readonly CustardThemeElement<CustardColor> ForegroundColor = 
        new("Button.ForegroundColor", () => CustardColor.Default);
    
    public static readonly CustardThemeElement<CustardColor> BackgroundColor = 
        new("Button.BackgroundColor", () => CustardColor.Default);
    
    public static readonly CustardThemeElement<CustardColor> FocusedForegroundColor = 
        new("Button.FocusedForegroundColor", () => CustardColor.Black);
    
    public static readonly CustardThemeElement<CustardColor> FocusedBackgroundColor = 
        new("Button.FocusedBackgroundColor", () => CustardColor.White);
    
    public static readonly CustardThemeElement<string> LeftBracket = 
        new("Button.LeftBracket", () => "[ ");
    
    public static readonly CustardThemeElement<string> RightBracket = 
        new("Button.RightBracket", () => " ]");
}

/// <summary>
/// Theme elements for TextBox widgets.
/// </summary>
public static class TextBoxTheme
{
    public static readonly CustardThemeElement<CustardColor> ForegroundColor = 
        new("TextBox.ForegroundColor", () => CustardColor.Default);
    
    public static readonly CustardThemeElement<CustardColor> BackgroundColor = 
        new("TextBox.BackgroundColor", () => CustardColor.Default);
    
    public static readonly CustardThemeElement<CustardColor> FocusedForegroundColor = 
        new("TextBox.FocusedForegroundColor", () => CustardColor.Default);
    
    public static readonly CustardThemeElement<CustardColor> CursorForegroundColor = 
        new("TextBox.CursorForegroundColor", () => CustardColor.Black);
    
    public static readonly CustardThemeElement<CustardColor> CursorBackgroundColor = 
        new("TextBox.CursorBackgroundColor", () => CustardColor.White);
    
    public static readonly CustardThemeElement<CustardColor> SelectionForegroundColor = 
        new("TextBox.SelectionForegroundColor", () => CustardColor.Black);
    
    public static readonly CustardThemeElement<CustardColor> SelectionBackgroundColor = 
        new("TextBox.SelectionBackgroundColor", () => CustardColor.Cyan);
    
    public static readonly CustardThemeElement<string> LeftBracket = 
        new("TextBox.LeftBracket", () => "[");
    
    public static readonly CustardThemeElement<string> RightBracket = 
        new("TextBox.RightBracket", () => "]");
}

/// <summary>
/// Theme elements for List widgets.
/// </summary>
public static class ListTheme
{
    public static readonly CustardThemeElement<CustardColor> ForegroundColor = 
        new("List.ForegroundColor", () => CustardColor.Default);
    
    public static readonly CustardThemeElement<CustardColor> BackgroundColor = 
        new("List.BackgroundColor", () => CustardColor.Default);
    
    public static readonly CustardThemeElement<CustardColor> SelectedForegroundColor = 
        new("List.SelectedForegroundColor", () => CustardColor.White);
    
    public static readonly CustardThemeElement<CustardColor> SelectedBackgroundColor = 
        new("List.SelectedBackgroundColor", () => CustardColor.Blue);
    
    public static readonly CustardThemeElement<string> SelectedIndicator = 
        new("List.SelectedIndicator", () => "> ");
    
    public static readonly CustardThemeElement<string> UnselectedIndicator = 
        new("List.UnselectedIndicator", () => "  ");
}

/// <summary>
/// Theme elements for Splitter widgets.
/// </summary>
public static class SplitterTheme
{
    public static readonly CustardThemeElement<CustardColor> DividerColor = 
        new("Splitter.DividerColor", () => CustardColor.Gray);
    
    public static readonly CustardThemeElement<string> DividerCharacter = 
        new("Splitter.DividerCharacter", () => "│");
}

/// <summary>
/// Theme elements for general/global settings.
/// </summary>
public static class GlobalTheme
{
    public static readonly CustardThemeElement<CustardColor> ForegroundColor = 
        new("Global.ForegroundColor", () => CustardColor.Default);
    
    public static readonly CustardThemeElement<CustardColor> BackgroundColor = 
        new("Global.BackgroundColor", () => CustardColor.Default);
}
