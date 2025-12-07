using Hex1b;
using Hex1b.Fluent;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Gallery.Exhibits;

/// <summary>
/// An exhibit that demonstrates Hex1b theming by showing a theme selector on the left
/// and widget examples on the right that update dynamically as themes are selected.
/// </summary>
public class ThemingExhibit(ILogger<ThemingExhibit> logger) : Hex1bExhibit
{
    private readonly ILogger<ThemingExhibit> _logger = logger;

    public override string Id => "theming";
    public override string Title => "Theming";
    public override string Description => "Dynamic theme switching with live widget preview.";

    /// <summary>
    /// State for the theming exhibit.
    /// </summary>
    private class ThemingState
    {
        public required Hex1bTheme[] Themes { get; init; }
        public ListState ThemeList { get; } = new();
        public TextBoxState SampleTextBox { get; } = new() { Text = "Sample text" };
        public bool ButtonClicked { get; set; }
    }

    // Thread-local session state for each websocket connection
    [ThreadStatic]
    private static ThemingState? _currentSession;

    public override Func<CancellationToken, Task<Hex1bWidget>> CreateWidgetBuilder()
    {
        var themes = new Hex1bTheme[]
        {
            Hex1bThemes.Default,
            Hex1bThemes.Ocean,
            Hex1bThemes.Sunset,
            Hex1bThemes.HighContrast,
            CreateForestTheme(),
            CreateNeonTheme(),
        };

        var state = new ThemingState
        {
            Themes = themes
        };
        state.ThemeList.Items = themes.Select(t => new ListItem(t.Name, t.Name)).ToList();
        
        _currentSession = state;

        return ct =>
        {
            var ctx = new RootContext<ThemingState>(state);
            
            var widget = ctx.Splitter(
                ctx.VStack(left => [
                    left.Text("═══ Themes ═══"),
                    left.Text(""),
                    left.List(s => s.ThemeList)
                ]),
                ctx.VStack(right => [
                    right.Text("═══ Widget Preview ═══"),
                    right.Text(""),
                    right.Border(b => [
                        b.Text("  Content inside border"),
                        b.Text("  with multiple lines")
                    ], title: "Border"),
                    right.Text(""),
                    right.Panel(p => [
                        p.Text("  Panel with styled background"),
                        p.Text("  (theme-dependent colors)")
                    ]),
                    right.Text(""),
                    right.Text("TextBox (Tab to focus):"),
                    right.TextBox(s => s.SampleTextBox),
                    right.Text(""),
                    right.Text("Button:"),
                    right.Button(
                        state.ButtonClicked ? "Clicked!" : "Click Me",
                        () => state.ButtonClicked = !state.ButtonClicked),
                    right.Text(""),
                    right.Text("─────────────────────────"),
                    right.Text(""),
                    right.Text("Use ↑↓ to change theme"),
                    right.Text("Tab to switch focus"),
                    right.Text("Enter to click button")
                ]),
                leftWidth: 20
            );

            return Task.FromResult<Hex1bWidget>(widget);
        };
    }

    public override Func<Hex1bTheme>? CreateThemeProvider()
    {
        var session = _currentSession!;
        return () => session.Themes[session.ThemeList.SelectedIndex];
    }

    private static Hex1bTheme CreateForestTheme()
    {
        return new Hex1bTheme("Forest")
            // Buttons
            .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.White)
            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(34, 139, 34))
            // TextBox
            .Set(TextBoxTheme.CursorForegroundColor, Hex1bColor.Black)
            .Set(TextBoxTheme.CursorBackgroundColor, Hex1bColor.FromRgb(144, 238, 144))
            .Set(TextBoxTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(0, 100, 0))
            .Set(TextBoxTheme.SelectionForegroundColor, Hex1bColor.White)
            // List
            .Set(ListTheme.SelectedForegroundColor, Hex1bColor.White)
            .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.FromRgb(34, 139, 34))
            .Set(ListTheme.SelectedIndicator, "♣ ")
            // Splitter
            .Set(SplitterTheme.DividerColor, Hex1bColor.FromRgb(34, 139, 34))
            .Set(SplitterTheme.DividerCharacter, "┃")
            // Border
            .Set(BorderTheme.BorderColor, Hex1bColor.FromRgb(34, 139, 34))
            .Set(BorderTheme.TitleColor, Hex1bColor.FromRgb(144, 238, 144))
            // Panel
            .Set(PanelTheme.BackgroundColor, Hex1bColor.FromRgb(0, 50, 0))
            .Set(PanelTheme.ForegroundColor, Hex1bColor.FromRgb(144, 238, 144));
    }

    private static Hex1bTheme CreateNeonTheme()
    {
        return new Hex1bTheme("Neon")
            // Buttons
            .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.Black)
            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(255, 0, 255))
            .Set(ButtonTheme.LeftBracket, "< ")
            .Set(ButtonTheme.RightBracket, " >")
            // TextBox
            .Set(TextBoxTheme.CursorForegroundColor, Hex1bColor.Black)
            .Set(TextBoxTheme.CursorBackgroundColor, Hex1bColor.FromRgb(0, 255, 255))
            .Set(TextBoxTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(255, 0, 255))
            .Set(TextBoxTheme.SelectionForegroundColor, Hex1bColor.White)
            .Set(TextBoxTheme.LeftBracket, "<")
            .Set(TextBoxTheme.RightBracket, ">")
            // List
            .Set(ListTheme.SelectedForegroundColor, Hex1bColor.Black)
            .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.FromRgb(0, 255, 255))
            .Set(ListTheme.SelectedIndicator, "* ")
            // Splitter
            .Set(SplitterTheme.DividerColor, Hex1bColor.FromRgb(255, 0, 255))
            .Set(SplitterTheme.DividerCharacter, "║")
            // Border
            .Set(BorderTheme.BorderColor, Hex1bColor.FromRgb(255, 0, 255))
            .Set(BorderTheme.TitleColor, Hex1bColor.FromRgb(0, 255, 255))
            .Set(BorderTheme.TopLeftCorner, "╔")
            .Set(BorderTheme.TopRightCorner, "╗")
            .Set(BorderTheme.BottomLeftCorner, "╚")
            .Set(BorderTheme.BottomRightCorner, "╝")
            .Set(BorderTheme.HorizontalLine, "═")
            .Set(BorderTheme.VerticalLine, "║")
            // Panel
            .Set(PanelTheme.BackgroundColor, Hex1bColor.FromRgb(30, 0, 30))
            .Set(PanelTheme.ForegroundColor, Hex1bColor.FromRgb(0, 255, 255));
    }
}
