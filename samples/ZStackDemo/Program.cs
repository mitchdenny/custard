using Hex1b;
using Hex1b.Terminal;
using Hex1b.Theming;
using Hex1b.Widgets;

// PopupStack Demo - Anchored menus with automatic positioning
// Run with: dotnet run --project samples/ZStackDemo

var selectedAction = "None selected";

try
{
    var presentation = new ConsolePresentationAdapter(enableMouse: true);
    var workload = new Hex1bAppWorkloadAdapter(presentation.Capabilities);
    
    var terminalOptions = new Hex1bTerminalOptions
    {
        PresentationAdapter = presentation,
        WorkloadAdapter = workload
    };
    terminalOptions.AddHex1bAppRenderOptimization();
    
    using var terminal = new Hex1bTerminal(terminalOptions);

    await using var app = new Hex1bApp(
        ctx => ctx.ThemePanel(
            theme => theme.Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(40, 40, 40)),
            ctx.VStack(main => [
                // Menu bar - buttons use PushAnchored for positioned menus
                main.HStack(menuBar => [
                    menuBar.Button(" File ")
                        .OnClick(e => e.PushAnchored(AnchorPosition.Below, () => BuildFileMenu(ctx, e.Popups))),
                    menuBar.Button(" Edit ")
                        .OnClick(e => e.PushAnchored(AnchorPosition.Below, () => BuildEditMenu(ctx, e.Popups, a => selectedAction = a))),
                    menuBar.Button(" View ")
                        .OnClick(e => e.PushAnchored(AnchorPosition.Below, () => BuildViewMenu(ctx, e.Popups, a => selectedAction = a))),
                    menuBar.Button(" Help ")
                        .OnClick(e => e.PushAnchored(AnchorPosition.Below, () => BuildHelpMenu(ctx, e.Popups, a => selectedAction = a))),
                    menuBar.Text("").Fill(),
                ]).ContentHeight(),
                
                // Main content area
                main.Border(
                    main.VStack(content => [
                        content.Text("Anchored PopupStack Demo"),
                        content.Text("════════════════════════════════════════"),
                        content.Text(""),
                        content.Text("Menus are now positioned relative to their trigger button!"),
                        content.Text("Use e.PushAnchored(AnchorPosition.Below, ...) for automatic positioning."),
                        content.Text(""),
                        content.Text($"Selected action: {selectedAction}"),
                        content.Text(""),
                        content.Text("Try clicking different menu buttons - each menu appears"),
                        content.Text("directly below its trigger button."),
                    ]),
                    title: "Main Content"
                ).Fill(),
                
                main.InfoBar([
                    "Tab", "Navigate",
                    "Enter/Click", "Activate",
                    "Ctrl+C", "Exit"
                ]),
            ])
        ),
        new Hex1bAppOptions
        {
            WorkloadAdapter = workload,
            EnableMouse = true
        }
    );

    await app.RunAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey(true);
}

// Menu builders - cascading uses AnchorPosition.Right
Hex1bWidget BuildFileMenu<TParent>(WidgetContext<TParent> ctx, PopupStack popups)
    where TParent : Hex1bWidget
{
    return ctx.ThemePanel(
        theme => theme.Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(50, 50, 80)),
        ctx.Border(
            ctx.VStack(m => [
                m.Button(" New         ").OnClick(_ => popups.Clear()),
                m.Button(" Open        ").OnClick(_ => popups.Clear()),
                m.Button(" Recent    ► ").OnClick(e => e.Popups.PushAnchored(e.Node, AnchorPosition.Right, () => BuildRecentMenu(ctx, popups))),
                m.Text("─────────────"),
                m.Button(" Save        ").OnClick(_ => popups.Clear()),
                m.Button(" Save As...  ").OnClick(_ => popups.Clear()),
                m.Text("─────────────"),
                m.Button(" Exit        ").OnClick(_ => popups.Clear()),
            ]),
            title: "File"
        ).FixedWidth(17)
    );
}

Hex1bWidget BuildRecentMenu<TParent>(WidgetContext<TParent> ctx, PopupStack popups)
    where TParent : Hex1bWidget
{
    return ctx.ThemePanel(
        theme => theme.Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(50, 80, 50)),
        ctx.Border(
            ctx.VStack(m => [
                m.Button(" document1.txt  ").OnClick(_ => popups.Clear()),
                m.Button(" report.md      ").OnClick(_ => popups.Clear()),
                m.Button(" code.cs        ").OnClick(_ => popups.Clear()),
                m.Text("────────────────"),
                m.Button(" More...      ► ").OnClick(e => e.Popups.PushAnchored(e.Node, AnchorPosition.Right, () => BuildMoreRecentMenu(ctx, popups))),
            ]),
            title: "Recent"
        ).FixedWidth(20)
    );
}

Hex1bWidget BuildMoreRecentMenu<TParent>(WidgetContext<TParent> ctx, PopupStack popups)
    where TParent : Hex1bWidget
{
    return ctx.ThemePanel(
        theme => theme.Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(80, 80, 50)),
        ctx.Border(
            ctx.VStack(m => [
                m.Button(" project.sln  ").OnClick(_ => popups.Clear()),
                m.Button(" notes.txt    ").OnClick(_ => popups.Clear()),
                m.Button(" config.json  ").OnClick(_ => popups.Clear()),
            ]),
            title: "More Recent"
        ).FixedWidth(18)
    );
}

Hex1bWidget BuildEditMenu<TParent>(WidgetContext<TParent> ctx, PopupStack popups, Action<string> onAction)
    where TParent : Hex1bWidget
{
    return ctx.ThemePanel(
        theme => theme.Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(50, 50, 80)),
        ctx.Border(
            ctx.VStack(m => [
                m.Button(" Undo        ").OnClick(_ => { onAction("Undo"); popups.Clear(); }),
                m.Button(" Redo        ").OnClick(_ => { onAction("Redo"); popups.Clear(); }),
                m.Text("─────────────"),
                m.Button(" Cut         ").OnClick(_ => { onAction("Cut"); popups.Clear(); }),
                m.Button(" Copy        ").OnClick(_ => { onAction("Copy"); popups.Clear(); }),
                m.Button(" Paste       ").OnClick(_ => { onAction("Paste"); popups.Clear(); }),
            ]),
            title: "Edit"
        ).FixedWidth(17)
    );
}

Hex1bWidget BuildViewMenu<TParent>(WidgetContext<TParent> ctx, PopupStack popups, Action<string> onAction)
    where TParent : Hex1bWidget
{
    return ctx.ThemePanel(
        theme => theme.Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(50, 50, 80)),
        ctx.Border(
            ctx.VStack(m => [
                m.Button(" Zoom In     ").OnClick(_ => { onAction("Zoom In"); popups.Clear(); }),
                m.Button(" Zoom Out    ").OnClick(_ => { onAction("Zoom Out"); popups.Clear(); }),
                m.Text("─────────────"),
                m.Button(" Full Screen ").OnClick(_ => { onAction("Full Screen"); popups.Clear(); }),
            ]),
            title: "View"
        ).FixedWidth(17)
    );
}

Hex1bWidget BuildHelpMenu<TParent>(WidgetContext<TParent> ctx, PopupStack popups, Action<string> onAction)
    where TParent : Hex1bWidget
{
    return ctx.ThemePanel(
        theme => theme.Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(50, 50, 80)),
        ctx.Border(
            ctx.VStack(m => [
                m.Button(" Documentation ").OnClick(_ => { onAction("Documentation"); popups.Clear(); }),
                m.Button(" About         ").OnClick(_ => { onAction("About"); popups.Clear(); }),
            ]),
            title: "Help"
        ).FixedWidth(18)
    );
}
