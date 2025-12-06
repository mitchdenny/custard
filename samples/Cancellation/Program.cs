using Custard;

// Set up cancellation with Ctrl+C
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // Prevent immediate termination
    cts.Cancel();
};

// Create and run the app
using var app = new CustardApp(App);
await app.RunAsync(cts.Token);

// The root component
static Task<CustardWidget> App(CancellationToken cancellationToken) => CustardWidgets.TextBlockAsync("Hello, Custard!", cancellationToken);