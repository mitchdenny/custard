namespace Custard.Theming;

/// <summary>
/// Represents a theme element with a typed value.
/// </summary>
public class CustardThemeElement<T>
{
    public string Name { get; }
    public Func<T> DefaultValue { get; }

    public CustardThemeElement(string name, Func<T> defaultValue)
    {
        Name = name;
        DefaultValue = defaultValue;
    }

    public override string ToString() => Name;
    
    public override int GetHashCode() => Name.GetHashCode();
    
    public override bool Equals(object? obj) => 
        obj is CustardThemeElement<T> other && Name == other.Name;
}
