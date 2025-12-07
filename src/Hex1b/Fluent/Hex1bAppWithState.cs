using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Fluent;

/// <summary>
/// A Hex1bApp variant that provides typed state management through the fluent API.
/// </summary>
/// <typeparam name="TState">The application state type.</typeparam>
public class Hex1bApp<TState> : IDisposable
{
    private readonly Hex1bApp _innerApp;

    /// <summary>
    /// The application state, accessible for external state mutations.
    /// </summary>
    public TState State { get; }

    /// <summary>
    /// Creates a Hex1bApp with typed state and a fluent widget builder.
    /// </summary>
    /// <param name="state">The application state instance.</param>
    /// <param name="builder">A function that builds the widget tree using the fluent context API.</param>
    /// <param name="terminal">Optional custom terminal implementation.</param>
    /// <param name="theme">Optional theme.</param>
    public Hex1bApp(
        TState state,
        Func<RootContext<TState>, CancellationToken, Task<Hex1bWidget>> builder,
        IHex1bTerminal? terminal = null,
        Hex1bTheme? theme = null)
    {
        State = state;
        var rootContext = new RootContext<TState>(state);

        if (terminal != null)
        {
            _innerApp = new Hex1bApp(
                ct => builder(rootContext, ct),
                terminal,
                theme,
                ownsTerminal: false);
        }
        else
        {
            _innerApp = new Hex1bApp(
                ct => builder(rootContext, ct),
                theme);
        }
    }

    /// <summary>
    /// Creates a Hex1bApp with typed state and a synchronous fluent widget builder.
    /// </summary>
    public Hex1bApp(
        TState state,
        Func<RootContext<TState>, Hex1bWidget> builder,
        IHex1bTerminal? terminal = null,
        Hex1bTheme? theme = null)
        : this(state, (ctx, ct) => Task.FromResult(builder(ctx)), terminal, theme)
    {
    }

    /// <summary>
    /// Runs the application until cancellation is requested.
    /// </summary>
    public Task RunAsync(CancellationToken cancellationToken = default)
        => _innerApp.RunAsync(cancellationToken);

    public void Dispose()
    {
        _innerApp.Dispose();
    }
}
