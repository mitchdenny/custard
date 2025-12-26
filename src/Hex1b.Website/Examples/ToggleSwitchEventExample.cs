using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// ToggleSwitch Widget Documentation: Event Handler Example
/// Demonstrates toggle switch with selection changed event handler.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the eventCode sample in:
/// src/content/guide/widgets/toggle-switch.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ToggleSwitchEventExample(ILogger<ToggleSwitchEventExample> logger) : Hex1bExample
{
    private readonly ILogger<ToggleSwitchEventExample> _logger = logger;

    public override string Id => "toggle-switch-event";
    public override string Title => "ToggleSwitch Widget - Event Handling";
    public override string Description => "Demonstrates toggle switch with selection changed event handling";

    private class SettingsState
    {
        public ToggleSwitchState ThemeToggle { get; } = new()
        {
            Options = ["Light", "Dark"],
            SelectedIndex = 1
        };

        public ToggleSwitchState NotificationToggle { get; } = new()
        {
            Options = ["Off", "On"],
            SelectedIndex = 1
        };

        public List<string> EventLog { get; } = [];
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating toggle switch event example widget builder");

        var state = new SettingsState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Border(b => [
                b.VStack(v => [
                    v.Text("Settings Panel"),
                    v.Text(""),
                    v.HStack(h => [
                        h.Text("Theme:         ").FixedWidth(16),
                        h.ToggleSwitch(state.ThemeToggle)
                            .OnSelectionChanged(args => 
                            {
                                state.EventLog.Add($"Theme changed to: {args.SelectedOption}");
                            })
                    ]),
                    v.Text(""),
                    v.HStack(h => [
                        h.Text("Notifications: ").FixedWidth(16),
                        h.ToggleSwitch(state.NotificationToggle)
                            .OnSelectionChanged(args => 
                            {
                                state.EventLog.Add($"Notifications: {args.SelectedOption}");
                            })
                    ]),
                    v.Text(""),
                    v.Text("Event Log:"),
                    ..state.EventLog.TakeLast(3).Select(log => v.Text($"  • {log}"))
                ])
            ], title: "User Preferences");
        };
    }
}
