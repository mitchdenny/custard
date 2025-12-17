namespace Hex1b.Input;

/// <summary>
/// A trie (prefix tree) for efficient chord lookup.
/// Each node represents a key step; leaf nodes contain actions.
/// </summary>
public sealed class ChordTrie
{
    private readonly Dictionary<KeyStep, ChordTrie> _children = [];
    private InputBinding? _binding;

    /// <summary>
    /// Registers a binding in the trie.
    /// Later registrations with the same key sequence override earlier ones.
    /// </summary>
    public void Register(InputBinding binding)
    {
        Register(binding.Steps, 0, binding);
    }

    private void Register(IReadOnlyList<KeyStep> steps, int index, InputBinding binding)
    {
        if (index >= steps.Count)
        {
            // Leaf node - store the binding
            _binding = binding;
            return;
        }

        var step = steps[index];
        if (!_children.TryGetValue(step, out var child))
        {
            child = new ChordTrie();
            _children[step] = child;
        }
        child.Register(steps, index + 1, binding);
    }

    /// <summary>
    /// Looks up a key step in this trie node.
    /// </summary>
    public ChordLookupResult Lookup(KeyStep step)
    {
        if (_children.TryGetValue(step, out var child))
        {
            return new ChordLookupResult(child);
        }
        return ChordLookupResult.NoMatch;
    }

    /// <summary>
    /// Looks up a key step using a key event.
    /// </summary>
    public ChordLookupResult Lookup(Hex1bKeyEvent evt)
    {
        return Lookup(new KeyStep(evt.Key, evt.Modifiers));
    }

    /// <summary>
    /// Gets whether this node has an action (is a valid endpoint).
    /// </summary>
    public bool HasAction => _binding != null;

    /// <summary>
    /// Gets whether this node has children (more steps possible).
    /// </summary>
    public bool HasChildren => _children.Count > 0;

    /// <summary>
    /// Gets whether this is a leaf node (has action, no children).
    /// </summary>
    public bool IsLeaf => HasAction && !HasChildren;

    /// <summary>
    /// Executes the action if present with the given context.
    /// </summary>
    public Task ExecuteAsync(ActionContext context) => _binding?.ExecuteAsync(context) ?? Task.CompletedTask;

    /// <summary>
    /// Gets the description if present.
    /// </summary>
    public string? Description => _binding?.Description;

    /// <summary>
    /// Builds a trie from a collection of bindings.
    /// </summary>
    public static ChordTrie Build(IEnumerable<InputBinding> bindings)
    {
        var trie = new ChordTrie();
        foreach (var binding in bindings)
        {
            trie.Register(binding);
        }
        return trie;
    }
}

/// <summary>
/// Result of looking up a key step in a chord trie.
/// </summary>
public readonly struct ChordLookupResult
{
    /// <summary>
    /// The matched trie node, or null if no match.
    /// </summary>
    public ChordTrie? Node { get; }

    /// <summary>
    /// Whether the lookup found a match.
    /// </summary>
    public bool IsMatch => Node != null;

    /// <summary>
    /// Whether the lookup found no match.
    /// </summary>
    public bool IsNoMatch => Node == null;

    /// <summary>
    /// Whether the matched node is a leaf (has action, no further children).
    /// </summary>
    public bool IsLeaf => Node?.IsLeaf ?? false;

    /// <summary>
    /// Whether the matched node has an action (could be executed now).
    /// </summary>
    public bool HasAction => Node?.HasAction ?? false;

    /// <summary>
    /// Whether the matched node has children (more steps possible).
    /// </summary>
    public bool HasChildren => Node?.HasChildren ?? false;

    public ChordLookupResult(ChordTrie node)
    {
        Node = node;
    }

    /// <summary>
    /// A result indicating no match was found.
    /// </summary>
    public static ChordLookupResult NoMatch => default;

    /// <summary>
    /// Executes the action if present with the given context.
    /// </summary>
    public Task ExecuteAsync(ActionContext context) => Node?.ExecuteAsync(context) ?? Task.CompletedTask;
}
