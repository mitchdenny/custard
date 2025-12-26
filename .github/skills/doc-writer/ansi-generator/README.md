# ANSI Generator Template

This is a template console application for AI agents to generate static ANSI screenshots of Hex1b widgets for documentation.

## Purpose

The ANSI generator creates `.ansi` files that can be embedded in VitePress documentation using the `<StaticTerminal>` component. This allows documentation to show exactly how widgets render without requiring a live WebSocket connection.

## Agent Workflow

**This template is designed for AI coding agents.** Follow these steps exactly:

### Step 1: Create Working Directory and Copy Template

```bash
# Create a temporary working directory
mkdir -p /tmp/ansi-gen-work

# Copy the template files
cp .github/skills/doc-writer/ansi-generator/AnsiGenerator.csproj /tmp/ansi-gen-work/
cp .github/skills/doc-writer/ansi-generator/Program.cs /tmp/ansi-gen-work/
```

### Step 2: Modify Program.cs

Edit `/tmp/ansi-gen-work/Program.cs` and replace the `GenerateSnapshots()` method with your specific widgets:

```csharp
static async Task GenerateSnapshots(string outputDir)
{
    // Replace this with the widget you want to screenshot
    await GenerateSnapshot(outputDir, "your-filename", "Description", 80, 24,
        ctx => ctx.YourWidget(...));
}
```

### Step 3: Run the Generator

```bash
cd /tmp/ansi-gen-work
dotnet run -- output
```

### Step 4: Copy Output to Static Site

```bash
# Copy the generated .ansi files to the public assets folder
cp /tmp/ansi-gen-work/output/*.ansi /path/to/workspace/src/content/public/ansi/
```

### Step 5: Use in Documentation

Reference the ANSI file in your markdown:

```markdown
<StaticTerminal file="ansi/your-filename.ansi" title="Description" :cols="80" :rows="24" />
```

### Step 6: Clean Up

```bash
rm -rf /tmp/ansi-gen-work
```

## GenerateSnapshot Parameters

```csharp
await GenerateSnapshot(
    outputDir,      // Output directory (use "output")
    "name",         // Filename without .ansi extension (e.g., "text-wrap-demo")
    "Description",  // Description for console output
    80,             // Terminal width in columns
    24,             // Terminal height in rows
    ctx => widget   // Widget builder using RootContext
);
```

## Important Notes for Agents

1. **Always use a temporary directory** - Don't modify the template in place
2. **The csproj uses a relative path** - The template references `../../../../src/Hex1b/Hex1b.csproj`, so you may need to adjust this in the copy if your temp directory is elsewhere
3. **Match dimensions to content** - Use smaller sizes (e.g., 60x12) for focused examples
4. **Use descriptive filenames** - Format: `widget-feature-state.ansi`
5. **Verify output before copying** - Check that the .ansi file was created successfully

## Alternative: Simpler Approach

If the relative project reference is problematic, you can also create the temp directory inside the workspace:

```bash
# Create temp dir inside workspace (simpler path resolution)
mkdir -p /path/to/workspace/.tmp-ansi-gen
cp .github/skills/doc-writer/ansi-generator/* /path/to/workspace/.tmp-ansi-gen/

# Edit and run
cd /path/to/workspace/.tmp-ansi-gen
dotnet run -- output

# Copy output and clean up
cp output/*.ansi ../src/content/public/ansi/
cd ..
rm -rf .tmp-ansi-gen
```

## Example Widget Patterns

### Simple Text
```csharp
await GenerateSnapshot(outputDir, "text-simple", "Simple Text", 40, 3,
    ctx => ctx.Text("Hello, World!"));
```

### Text with Wrapping
```csharp
await GenerateSnapshot(outputDir, "text-wrap", "Text Wrapping", 60, 8,
    ctx => ctx.Text("This is a long paragraph that demonstrates how text wrapping works in Hex1b when the content exceeds the available width.".Wrap());
```

### Layout with Multiple Widgets
```csharp
await GenerateSnapshot(outputDir, "layout-vstack", "VStack Layout", 60, 10,
    ctx => ctx.VStack(v => [
        v.Text("Title"),
        v.Text(""),
        v.Text("Content goes here")
    ]));
```

### Border with Content
```csharp
await GenerateSnapshot(outputDir, "border-example", "Border Example", 40, 8,
    ctx => ctx.Border(b => [
        b.Text("Inside a border")
    ], title: "My Box"));
```

