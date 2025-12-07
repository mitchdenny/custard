namespace Hex1b;

/// <summary>
/// Represents an input event from the terminal.
/// </summary>
public abstract record Hex1bInputEvent;

public sealed record KeyInputEvent(ConsoleKey Key, char KeyChar, bool Shift, bool Alt, bool Control) : Hex1bInputEvent;

/// <summary>
/// Represents a terminal resize event.
/// </summary>
public sealed record ResizeInputEvent(int Width, int Height) : Hex1bInputEvent;

/// <summary>
/// Represents a terminal capability response (e.g., DA1 response for Sixel detection).
/// This event triggers a re-render so widgets can update based on detected capabilities.
/// </summary>
public sealed record CapabilityResponseEvent(string Response) : Hex1bInputEvent;
