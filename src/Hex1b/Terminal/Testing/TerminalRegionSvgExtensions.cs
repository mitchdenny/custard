using System.Text;
using System.Web;

namespace Hex1b.Terminal.Testing;

/// <summary>
/// Extension methods for rendering terminal regions to SVG format.
/// </summary>
public static class TerminalRegionSvgExtensions
{
    /// <summary>
    /// Default options for SVG rendering.
    /// </summary>
    public static readonly TerminalSvgOptions DefaultOptions = new();

    /// <summary>
    /// Renders the terminal region to an SVG string.
    /// </summary>
    /// <param name="region">The terminal region to render.</param>
    /// <param name="options">Optional rendering options.</param>
    /// <returns>An SVG string representation of the terminal region.</returns>
    public static string ToSvg(this IHex1bTerminalRegion region, TerminalSvgOptions? options = null)
    {
        options ??= DefaultOptions;
        return RenderToSvg(region, options, cursorX: null, cursorY: null);
    }

    /// <summary>
    /// Renders the terminal snapshot to an SVG string, including cursor position.
    /// </summary>
    /// <param name="snapshot">The terminal snapshot to render.</param>
    /// <param name="options">Optional rendering options.</param>
    /// <returns>An SVG string representation of the terminal snapshot.</returns>
    public static string ToSvg(this Hex1bTerminalSnapshot snapshot, TerminalSvgOptions? options = null)
    {
        options ??= DefaultOptions;
        return RenderToSvg(snapshot, options, snapshot.CursorX, snapshot.CursorY);
    }

    private static string RenderToSvg(IHex1bTerminalRegion region, TerminalSvgOptions options, int? cursorX, int? cursorY)
    {
        var cellWidth = options.CellWidth;
        var cellHeight = options.CellHeight;
        var width = region.Width * cellWidth;
        var height = region.Height * cellHeight;

        var sb = new StringBuilder();

        // SVG header
        sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">""");

        // Style definitions
        sb.AppendLine("  <style>");
        sb.AppendLine($"    .terminal-text {{ font-family: {options.FontFamily}; font-size: {options.FontSize}px; }}");
        sb.AppendLine($"    .cursor {{ fill: {options.CursorColor}; opacity: 0.7; }}");
        sb.AppendLine("  </style>");

        // Background rectangle
        sb.AppendLine($"""  <rect width="{width}" height="{height}" fill="{options.DefaultBackground}"/>""");

        // Group for cells
        sb.AppendLine("  <g class=\"terminal-text\">");

        // Render background colors first (as rectangles)
        for (int y = 0; y < region.Height; y++)
        {
            for (int x = 0; x < region.Width; x++)
            {
                var cell = region.GetCell(x, y);
                if (cell.Background.HasValue)
                {
                    var bg = cell.Background.Value;
                    var bgColor = $"rgb({bg.R},{bg.G},{bg.B})";
                    var rectX = x * cellWidth;
                    var rectY = y * cellHeight;
                    sb.AppendLine($"""    <rect x="{rectX}" y="{rectY}" width="{cellWidth}" height="{cellHeight}" fill="{bgColor}"/>""");
                }
            }
        }

        // Render text characters
        for (int y = 0; y < region.Height; y++)
        {
            for (int x = 0; x < region.Width; x++)
            {
                var cell = region.GetCell(x, y);
                var ch = cell.Character == '\0' ? ' ' : cell.Character;

                // Skip spaces unless they have a foreground color
                if (ch == ' ' && !cell.Foreground.HasValue)
                    continue;

                var textX = x * cellWidth + (cellWidth / 2.0);
                var textY = y * cellHeight + (cellHeight * 0.75); // Baseline adjustment

                var fgColor = options.DefaultForeground;
                if (cell.Foreground.HasValue)
                {
                    var fg = cell.Foreground.Value;
                    fgColor = $"rgb({fg.R},{fg.G},{fg.B})";
                }

                var escapedChar = HttpUtility.HtmlEncode(ch.ToString());
                sb.AppendLine($"""    <text x="{textX:F1}" y="{textY:F1}" fill="{fgColor}" text-anchor="middle">{escapedChar}</text>""");
            }
        }

        sb.AppendLine("  </g>");

        // Render cursor if within bounds
        if (cursorX.HasValue && cursorY.HasValue &&
            cursorX.Value >= 0 && cursorX.Value < region.Width &&
            cursorY.Value >= 0 && cursorY.Value < region.Height)
        {
            var cursorRectX = cursorX.Value * cellWidth;
            var cursorRectY = cursorY.Value * cellHeight;
            sb.AppendLine($"""  <rect class="cursor" x="{cursorRectX}" y="{cursorRectY}" width="{cellWidth}" height="{cellHeight}"/>""");
        }

        sb.AppendLine("</svg>");

        return sb.ToString();
    }
}

/// <summary>
/// Options for SVG rendering of terminal regions.
/// </summary>
public class TerminalSvgOptions
{
    /// <summary>
    /// The font family to use for rendering. Should be a monospace font.
    /// </summary>
    public string FontFamily { get; set; } = "'Cascadia Code', 'Fira Code', Consolas, Monaco, 'Courier New', monospace";

    /// <summary>
    /// The font size in pixels.
    /// </summary>
    public int FontSize { get; set; } = 14;

    /// <summary>
    /// The width of each cell in pixels.
    /// </summary>
    public int CellWidth { get; set; } = 9;

    /// <summary>
    /// The height of each cell in pixels.
    /// </summary>
    public int CellHeight { get; set; } = 18;

    /// <summary>
    /// The default background color (CSS color string).
    /// </summary>
    public string DefaultBackground { get; set; } = "#1e1e1e";

    /// <summary>
    /// The default foreground color (CSS color string).
    /// </summary>
    public string DefaultForeground { get; set; } = "#d4d4d4";

    /// <summary>
    /// The cursor color (CSS color string).
    /// </summary>
    public string CursorColor { get; set; } = "#ffffff";
}
