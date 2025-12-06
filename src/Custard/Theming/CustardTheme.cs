namespace Custard.Theming;

/// <summary>
/// A theme containing values for various UI elements.
/// </summary>
public class CustardTheme
{
    private readonly Dictionary<string, object> _values = new();
    private readonly string _name;

    public CustardTheme(string name)
    {
        _name = name;
    }

    public string Name => _name;

    /// <summary>
    /// Gets the value for a theme element, or its default if not set.
    /// </summary>
    public T Get<T>(CustardThemeElement<T> element)
    {
        if (_values.TryGetValue(element.Name, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return element.DefaultValue();
    }

    /// <summary>
    /// Sets a value for a theme element.
    /// </summary>
    public CustardTheme Set<T>(CustardThemeElement<T> element, T value)
    {
        _values[element.Name] = value!;
        return this;
    }

    /// <summary>
    /// Creates a copy of this theme that can be modified.
    /// </summary>
    public CustardTheme Clone(string? newName = null)
    {
        var clone = new CustardTheme(newName ?? _name);
        foreach (var kvp in _values)
        {
            clone._values[kvp.Key] = kvp.Value;
        }
        return clone;
    }
}
