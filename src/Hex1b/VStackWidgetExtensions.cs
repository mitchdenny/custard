using Hex1b.Fluent;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Builder for VStackWidget children with typed state context.
/// </summary>
/// <typeparam name="TState">The state type available in this builder.</typeparam>
public class VStackBuilder<TState> : IChildBuilder
{
    private readonly WidgetContext<TState> _context;
    private readonly List<Hex1bWidget> _children = [];
    private readonly List<SizeHint> _sizeHints = [];
    private IReadOnlyList<Shortcut>? _shortcuts;

    public VStackBuilder(WidgetContext<TState> context)
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
    /// Sets shortcuts for this VStack.
    /// </summary>
    public void Shortcuts(params Shortcut[] shortcuts)
    {
        _shortcuts = shortcuts;
    }

    /// <summary>
    /// Builds the VStackWidget from the accumulated children.
    /// </summary>
    public VStackWidget Build()
    {
        return new VStackWidget(_children, _sizeHints)
        {
            Shortcuts = _shortcuts
        };
    }
}

/// <summary>
/// Extension methods for building VStackWidget using the fluent API.
/// </summary>
public static class VStackWidgetExtensions
{
    /// <summary>
    /// Creates a VStackWidget using a builder action.
    /// </summary>
    public static VStackWidget VStack<TState>(
        this WidgetContext<TState> context,
        Action<VStackBuilder<TState>> builderAction)
    {
        var builder = new VStackBuilder<TState>(context);
        builderAction(builder);
        return builder.Build();
    }

    /// <summary>
    /// Adds a child widget with a Fill size hint (takes remaining space).
    /// </summary>
    public static void AddFill<TState>(this VStackBuilder<TState> builder, Hex1bWidget widget)
        => builder.Add(widget, SizeHint.Fill);

    /// <summary>
    /// Adds a child widget with a Content size hint (takes only needed space).
    /// </summary>
    public static void AddContent<TState>(this VStackBuilder<TState> builder, Hex1bWidget widget)
        => builder.Add(widget, SizeHint.Content);

    /// <summary>
    /// Adds a child widget with a Fixed size hint.
    /// </summary>
    public static void AddFixed<TState>(this VStackBuilder<TState> builder, Hex1bWidget widget, int size)
        => builder.Add(widget, SizeHint.Fixed(size));

    /// <summary>
    /// Adds a nested VStack as a child.
    /// </summary>
    public static void VStack<TState>(
        this VStackBuilder<TState> builder,
        Action<VStackBuilder<TState>> nestedBuilderAction,
        SizeHint? sizeHint = null)
    {
        var nestedBuilder = new VStackBuilder<TState>(builder.Context);
        nestedBuilderAction(nestedBuilder);
        builder.Add(nestedBuilder.Build(), sizeHint);
    }

    /// <summary>
    /// Adds a VStackWidget to another builder (like HStack containing VStack).
    /// </summary>
    public static void VStack<TBuilder, TState>(
        this TBuilder builder,
        WidgetContext<TState> context,
        Action<VStackBuilder<TState>> nestedBuilderAction)
        where TBuilder : IChildBuilder
    {
        var nestedBuilder = new VStackBuilder<TState>(context);
        nestedBuilderAction(nestedBuilder);
        builder.Add(nestedBuilder.Build());
    }
}
