using Squill.Platform.Storage;

namespace Squill.Presentation.Explorer;

/// <summary>Mutable visibility of one explorer level (databases of a connection, schemas of a database, or tables of a database): hidden names plus wildcard patterns.</summary>
public sealed class NameVisibility
{
    private readonly HashSet<string> _hidden = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _patterns = [];

    /// <summary>Initializes the state.</summary>
    /// <param name="hidden">Names hidden one by one.</param>
    /// <param name="patterns">Wildcard patterns; at least one restricts the level to matching names.</param>
    public NameVisibility(IEnumerable<string>? hidden = null, IEnumerable<string>? patterns = null)
    {
        foreach (var h in hidden ?? [])
        {
            _hidden.Add(h);
        }

        foreach (var p in patterns ?? [])
        {
            AddPattern(p);
        }
    }

    /// <summary>Names hidden one by one.</summary>
    public IReadOnlyCollection<string> Hidden => _hidden;

    /// <summary>Wildcard patterns, in the order added.</summary>
    public IReadOnlyList<string> Patterns => _patterns;

    /// <summary>Whether nothing is hidden and no pattern restricts the level.</summary>
    public bool ShowsEverything => _hidden.Count == 0 && _patterns.Count == 0;

    /// <summary>Tells whether a name is shown: not hidden, and matching a pattern when there is any.</summary>
    /// <param name="name">Name to test.</param>
    /// <returns><see langword="true"/> when shown.</returns>
    public bool IsVisible(string name) =>
        !_hidden.Contains(name) && (_patterns.Count == 0 || _patterns.Exists(p => WildcardPattern.IsMatch(p, name)));

    /// <summary>Tells whether every name is shown.</summary>
    /// <param name="names">Names to test.</param>
    /// <returns><see langword="true"/> when all are shown.</returns>
    public bool AllVisible(IEnumerable<string> names) => names.All(IsVisible);

    /// <summary>Shows a name, adding it as an exact pattern when patterns would still exclude it.</summary>
    /// <param name="name">Name to show.</param>
    public void Show(string name)
    {
        _hidden.Remove(name);
        if (!IsVisible(name))
        {
            _patterns.Add(name);
        }
    }

    /// <summary>Hides a name: drops its exact pattern, then hides it explicitly if it is still shown.</summary>
    /// <param name="name">Name to hide.</param>
    public void Hide(string name)
    {
        _patterns.RemoveAll(p => !WildcardPattern.HasWildcard(p) && string.Equals(p, name, StringComparison.OrdinalIgnoreCase));
        if (IsVisible(name))
        {
            _hidden.Add(name);
        }
    }

    /// <summary>Hides each name.</summary>
    /// <param name="names">Names to hide.</param>
    public void HideAll(IEnumerable<string> names)
    {
        foreach (var n in names)
        {
            Hide(n);
        }
    }

    /// <summary>Shows the given names and clears the patterns in scope.</summary>
    /// <param name="names">Names to show.</param>
    /// <param name="scopePrefix">Only patterns starting with it are removed (and <c>prefix*</c> is added if other patterns remain); <see langword="null"/> clears every pattern.</param>
    public void ShowAll(IEnumerable<string> names, string? scopePrefix = null)
    {
        foreach (var n in names)
        {
            _hidden.Remove(n);
        }

        _patterns.RemoveAll(p => scopePrefix is null || p.StartsWith(scopePrefix, StringComparison.OrdinalIgnoreCase));
        if (scopePrefix is not null && _patterns.Count > 0)
        {
            _patterns.Add(scopePrefix + "*");
        }
    }

    /// <summary>Adds a pattern.</summary>
    /// <param name="pattern">Pattern, trimmed.</param>
    /// <returns><see langword="false"/> for blank text or a duplicate (ignoring case).</returns>
    public bool AddPattern(string pattern)
    {
        pattern = pattern.Trim();
        if (pattern.Length == 0 || _patterns.Exists(p => string.Equals(p, pattern, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        _patterns.Add(pattern);
        return true;
    }

    /// <summary>Removes a pattern.</summary>
    /// <param name="pattern">Pattern to remove (ignoring case).</param>
    public void RemovePattern(string pattern) =>
        _patterns.RemoveAll(p => string.Equals(p, pattern, StringComparison.OrdinalIgnoreCase));

    /// <summary>Builds the saved form.</summary>
    /// <returns><see langword="null"/> when everything is shown.</returns>
    public NameFilter? ToSaved() =>
        ShowsEverything
            ? null
            : new NameFilter([.. _hidden.Order(StringComparer.OrdinalIgnoreCase)], [.. _patterns]);
}
