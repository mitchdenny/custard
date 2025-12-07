using Hex1b.Fluent;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Builder for HStackWidget children with typed state context.
/// </summary>
/// <typeparam name="TState">The state type available in this builder.</typeparam>
public class HStackBuilder<TState> : IChildBuilder
{
    private readonly WidgetContext<TState> _context;
    private readonly List<Hex1bWidget> _children = [];
    private readonly List<SizeHint> _sizeHints = [];

    public HStackBuilder(WidgetContext<TState> context)
    {
        _context = context;
    }

    /// <summary>
    /// The context available to this builder.
    /// </summary>
    public WidgetContext<TState> Context => _context;

    /// <summary>
    /// The state available to this builder (convenience property).
    /// </summary>
    public TState State => _context.State;

    /// <summary>
    /// Adds a widget to the children with optional size hint.
    /// </summary>
    public void Add(Hex1bWidget widget, SizeHint? sizeHint = null)
    {
        _children.Add(widget);
        _sizeHints.Add(sizeHint ?? SizeHint.Content);
    }

    /// <summary>
    /// Adds a widget to the children (IChildBuilder implementation).
    /// </summary>
    void IChildBuilder.Add(Hex1bWidget widget)
    {
        Add(widget);
    }

    /// <summary>
    /// Builds the HStackWidget from the accumulated children.
    /// </summary>
    public HStackWidget Build()
    {
        return new HStackWidget(_children, _sizeHints);
    }
}

/// <summary>
/// Extension methods for building HStackWidget using the fluent API.
/// </summary>
public static class HStackWidgetExtensions
{
    /// <summary>
    /// Creates an HStackWidget using a builder action.
    /// </summary>
    public static HStackWidget HStack<TState>(
        this WidgetContext<TState> context,
        Action<HStackBuilder<TState>> builderAction)
    {
        var builder = new HStackBuilder<TState>(context);
        builderAction(builder);
        return builder.Build();
    }

    /// <summary>
    /// Adds a child widget with a Fill size hint (takes remaining space).
    /// </summary>
    public static void AddFill<TState>(this HStackBuilder<TState> builder, Hex1bWidget widget)
        => builder.Add(widget, SizeHint.Fill);

    /// <summary>
    /// Adds a child widget with a Content size hint (takes only needed space).
    /// </summary>
    public static void AddContent<TState>(this HStackBuilder<TState> builder, Hex1bWidget widget)
        => builder.Add(widget, SizeHint.Content);

    /// <summary>
    /// Adds a child widget with a Fixed size hint.
    /// </summary>
    public static void AddFixed<TState>(this HStackBuilder<TState> builder, Hex1bWidget widget, int size)
        => builder.Add(widget, SizeHint.Fixed(size));

    /// <summary>
    /// Adds a nested HStack as a child.
    /// </summary>
    public static void HStack<TState>(
        this HStackBuilder<TState> builder,
        Action<HStackBuilder<TState>> nestedBuilderAction,
        SizeHint? sizeHint = null)
    {
        var nestedBuilder = new HStackBuilder<TState>(builder.Context);
        nestedBuilderAction(nestedBuilder);
        builder.Add(nestedBuilder.Build(), sizeHint);
    }

    /// <summary>
    /// Adds an HStackWidget to a VStackBuilder.
    /// </summary>
    public static void HStack<TState>(
        this VStackBuilder<TState> builder,
        Action<HStackBuilder<TState>> nestedBuilderAction,
        SizeHint? sizeHint = null)
    {
        var nestedBuilder = new HStackBuilder<TState>(builder.Context);
        nestedBuilderAction(nestedBuilder);
        builder.Add(nestedBuilder.Build(), sizeHint);
    }
}
