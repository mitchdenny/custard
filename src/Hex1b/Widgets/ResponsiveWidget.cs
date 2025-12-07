namespace Hex1b.Widgets;

/// <summary>
/// A widget that wraps content with a condition that determines whether it should be displayed.
/// Used as part of a ResponsiveWidget to create conditional UI layouts.
/// </summary>
/// <param name="Condition">A function that receives (availableWidth, availableHeight) and returns true if this content should be displayed.</param>
/// <param name="Content">The content to display when the condition is met.</param>
public sealed record ConditionalWidget(Func<int, int, bool> Condition, Hex1bWidget Content) : Hex1bWidget;

/// <summary>
/// A widget that displays the first child whose condition evaluates to true.
/// Conditions are evaluated during layout with the available size from parent constraints.
/// </summary>
/// <param name="Branches">The list of conditional widgets to evaluate. The first matching branch is displayed.</param>
public sealed record ResponsiveWidget(IReadOnlyList<ConditionalWidget> Branches) : Hex1bWidget;
