namespace Hex1b.Input;

/// <summary>
/// A handler for drag operations, returned by a drag binding's factory.
/// Receives move and end events for the duration of the drag.
/// </summary>
public sealed class DragHandler
{
    /// <summary>
    /// Called when the mouse moves during the drag.
    /// Parameters are (deltaX, deltaY) from the drag start position.
    /// </summary>
    public Action<int, int>? OnMove { get; }
    
    /// <summary>
    /// Called when the drag ends (mouse button released).
    /// </summary>
    public Action? OnEnd { get; }

    public DragHandler(Action<int, int>? onMove = null, Action? onEnd = null)
    {
        OnMove = onMove;
        OnEnd = onEnd;
    }
}

/// <summary>
/// A binding that initiates a drag operation on mouse down.
/// The factory is called at drag start and returns a DragHandler that receives subsequent events.
/// </summary>
public sealed class DragBinding
{
    /// <summary>
    /// The mouse button that initiates the drag.
    /// </summary>
    public MouseButton Button { get; }
    
    /// <summary>
    /// Required modifier keys.
    /// </summary>
    public Hex1bModifiers Modifiers { get; }
    
    /// <summary>
    /// Factory that creates a DragHandler when the drag starts.
    /// Receives the local (x, y) coordinates where the drag began.
    /// </summary>
    public Func<int, int, DragHandler> Factory { get; }
    
    /// <summary>
    /// Human-readable description of what this drag binding does.
    /// </summary>
    public string? Description { get; }

    public DragBinding(MouseButton button, Hex1bModifiers modifiers, Func<int, int, DragHandler> factory, string? description)
    {
        Button = button;
        Modifiers = modifiers;
        Factory = factory;
        Description = description;
    }

    /// <summary>
    /// Checks if this binding matches the given mouse down event.
    /// </summary>
    public bool Matches(Hex1bMouseEvent mouseEvent)
    {
        return mouseEvent.Button == Button && 
               mouseEvent.Action == MouseAction.Down && 
               mouseEvent.Modifiers == Modifiers;
    }

    /// <summary>
    /// Starts the drag by invoking the factory with the local coordinates.
    /// </summary>
    public DragHandler StartDrag(int localX, int localY) => Factory(localX, localY);
}

/// <summary>
/// Fluent builder for constructing a drag binding.
/// </summary>
public sealed class DragStepBuilder
{
    private readonly InputBindingsBuilder _parent;
    private readonly MouseButton _button;
    private Hex1bModifiers _modifiers = Hex1bModifiers.None;

    internal DragStepBuilder(InputBindingsBuilder parent, MouseButton button)
    {
        _parent = parent;
        _button = button;
    }

    /// <summary>
    /// Requires Ctrl modifier.
    /// </summary>
    public DragStepBuilder Ctrl()
    {
        _modifiers |= Hex1bModifiers.Control;
        return this;
    }

    /// <summary>
    /// Requires Shift modifier.
    /// </summary>
    public DragStepBuilder Shift()
    {
        _modifiers |= Hex1bModifiers.Shift;
        return this;
    }

    /// <summary>
    /// Requires Alt modifier.
    /// </summary>
    public DragStepBuilder Alt()
    {
        _modifiers |= Hex1bModifiers.Alt;
        return this;
    }

    /// <summary>
    /// Binds the drag factory. The factory receives (startX, startY) local coordinates
    /// and returns a DragHandler that will receive move and end events.
    /// </summary>
    public InputBindingsBuilder Action(Func<int, int, DragHandler> factory, string? description = null)
    {
        var binding = new DragBinding(_button, _modifiers, factory, description);
        _parent.AddDragBinding(binding);
        return _parent;
    }
}
