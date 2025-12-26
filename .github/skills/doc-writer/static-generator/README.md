# Static Generator Template

This template generates static SVG and interactive HTML files for terminal widget previews in documentation.

## Output Formats

### SVG Files
- Crisp, scalable vector graphics
- Works in any browser
- Perfect for simple static previews

### HTML Files
- Interactive cell inspector
- Hover over cells to see character, color, and attribute details
- Supports minimal mode (`?minimal=true`) for embedding in iframes
- Click cells to pin tooltips

## Quick Start

1. Copy this directory to a temporary location
2. Update the project reference path
3. Modify `GenerateSnapshots()` in Program.cs
4. Run and copy outputs to `src/content/public/svg/`

See the main [SKILL.md](../SKILL.md) for detailed workflow instructions.

## Usage in Documentation

```markdown
<StaticTerminalPreview htmlPath="/svg/your-file.html">

```csharp
v.YourCodeHere()
```

</StaticTerminalPreview>
```

The `StaticTerminalPreview` component displays code with a "View Output" button that opens a floating overlay with the interactive HTML preview.

## Minimal Mode

When the HTML is loaded with `?minimal=true` query parameter:
- Header, theme controls, and info bar are hidden
- Only the SVG with hover tooltips is shown
- A subtle glow effect highlights the terminal boundary
- Perfect for iframe embedding

## TerminalSvgOptions

| Option | Default | Description |
|--------|---------|-------------|
| `ShowCellGrid` | `false` | Show grid lines between cells |
| `ShowPixelGrid` | `false` | Show pixel-level grid |
| `DefaultBackground` | `#0f0f1a` | Background color |
| `DefaultForeground` | `#e0e0e0` | Text color |
| `FontFamily` | Cascadia Code, etc. | Monospace font stack |
| `FontSize` | `14` | Font size in pixels |
| `CellWidth` | `9` | Cell width in pixels |
| `CellHeight` | `18` | Cell height in pixels |
