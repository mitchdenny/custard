namespace Custard.Widgets;

public sealed record ButtonWidget(string Label, Action OnClick) : CustardWidget;
