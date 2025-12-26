using Hex1b;
using Hex1b.Widgets;

var themeState = new ToggleSwitchState
{
    Options = ["Light", "Dark"],
    SelectedIndex = 1
};

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text("Unfocused:"),
        v.ToggleSwitch(new ToggleSwitchState { Options = ["Off", "On"], SelectedIndex = 1 }),
        v.Text(""),
        v.Text("Focused:"),
        v.ToggleSwitch(themeState)
    ])
));

await app.RunAsync();
