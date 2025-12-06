namespace Custard.Widgets;

/// <summary>
/// A vertical splitter/divider that separates left and right panes.
/// </summary>
public sealed record SplitterWidget(CustardWidget Left, CustardWidget Right, int LeftWidth = 30) : CustardWidget;
