using Hex1b;
using Hex1b.Widgets;

// State stored OUTSIDE the builder so it persists across renders
var todos = new List<(string Task, bool Done)>
{
    ("Build TUI", false),
    ("Add features", false),
    ("Ship it", false)
};

var listState = new ListState
{
    OnItemActivated = item =>
    {
        var i = int.Parse(item.Id);
        todos[i] = todos[i] with { Done = !todos[i].Done };
    }
};

using var cts = new CancellationTokenSource();

// Simple todo list demonstrating Hex1b basics
using var app = new Hex1bApp<object>(
    state: new object(),
    builder: ctx =>
    {
        // Update list items each render, but keep the same ListState instance
        listState.Items = todos.Select((t, i) =>
            new ListItem(i.ToString(), $" {(t.Done ? "✓" : "○")} {t.Task}")).ToList();

        return ctx.Border(b => [
            b.Text("📋 Todo List"),
            b.Text(""),
            b.List(listState),
            b.Text(""),
            b.Button("Quit", () => cts.Cancel()),
            b.Text(""),
            b.Text("↑↓ Navigate | Space: Toggle | Tab: Focus")
        ], "Hex1b Demo");
    }
);

Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
await app.RunAsync(cts.Token);
