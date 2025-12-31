namespace Hex1b;

/// <summary>
/// Interface for nodes that can host popup content.
/// Implemented by ZStackNode to allow popup discovery from anywhere in the tree.
/// </summary>
public interface IPopupHost
{
    /// <summary>
    /// The popup stack for this host.
    /// </summary>
    PopupStack Popups { get; }
}
