using Hex1b.Widgets;

namespace Hex1b.Fluent;

/// <summary>
/// Interface for builders that collect child widgets.
/// </summary>
public interface IChildBuilder
{
    /// <summary>
    /// Adds a widget to the children collection.
    /// </summary>
    void Add(Hex1bWidget widget);
}
