namespace Hex1b.Fluent;

/// <summary>
/// Provides a context for building widgets with typed state.
/// The context captures state that flows through the widget tree building process.
/// </summary>
/// <typeparam name="TState">The type of state available in this context.</typeparam>
public class WidgetContext<TState>
{
    /// <summary>
    /// The state available in this context.
    /// </summary>
    public TState State { get; }

    /// <summary>
    /// Creates a new widget context with the specified state.
    /// </summary>
    public WidgetContext(TState state)
    {
        State = state;
    }

    /// <summary>
    /// Creates a derived context with the same state.
    /// Useful for passing context to child builders.
    /// </summary>
    public WidgetContext<TState> Derive() => new(State);

    /// <summary>
    /// Creates a derived context with a sub-state selected from the current state.
    /// </summary>
    public WidgetContext<TChildState> Derive<TChildState>(Func<TState, TChildState> selector)
        => new(selector(State));
}

/// <summary>
/// Root context for building the top-level widget tree.
/// </summary>
/// <typeparam name="TState">The application state type.</typeparam>
public class RootContext<TState> : WidgetContext<TState>
{
    public RootContext(TState state) : base(state)
    {
    }
}
