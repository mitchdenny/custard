namespace Hex1b;

// A state transition is applied before this item's bytes, after all earlier items.
internal readonly record struct Hmp1WorkloadOutput(
    ReadOnlyMemory<byte> Bytes, Hmp1TerminalState? State = null,
    Hmp1KgpAnimationState? AnimationState = null);
