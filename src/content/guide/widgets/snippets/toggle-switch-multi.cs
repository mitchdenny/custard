using Hex1b;
using Hex1b.Widgets;

var speedState = new ToggleSwitchState
{
    Options = ["Slow", "Normal", "Fast"],
    SelectedIndex = 1
};

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text("Speed Settings:"),
        v.ToggleSwitch(speedState)
    ])
));

await app.RunAsync();
