using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Presentation.Localization;
using Squill.Presentation.Sessions;

namespace Squill.Presentation.Explorer;

/// <summary>The object kind a filter popup level lists.</summary>
internal enum FilterMode
{
    Databases,
    Schemas,
    Tables,
}

/// <summary>One level of a filter popup: the objects it lists, the session that knows them, and for tables the schema key (empty without schemas).</summary>
internal readonly record struct FilterLevel(FilterMode Mode, ConnectionSession Session, string Schema);

/// <summary>
/// The popup that edits what the explorer shows (databases of a connection, schemas of a database, tables of a schema), DataGrip style.
/// Changes apply at once; the heads draw the flat <see cref="Rows"/>.
/// </summary>
public sealed partial class ExplorerFilterViewModel : ViewModelBase
{
    private readonly ExplorerTreeViewModel _tree;
    private readonly FilterLevel _root;
    private readonly Guid _profileId;
    private readonly HashSet<string> _expanded = new(StringComparer.OrdinalIgnoreCase);

    internal ExplorerFilterViewModel(ExplorerTreeViewModel tree, FilterLevel root)
    {
        _tree = tree;
        _root = root;
        _profileId = root.Session.Profile.Id;
        Rebuild();
    }

    /// <summary>Gets the rows in display order: only the ones under expanded parents and passing <see cref="SearchText"/>. The same instance for the life of the popup.</summary>
    public ObservableCollection<FilterRow> Rows { get; } = [];

    /// <summary>Gets or sets the text item rows must contain, ignoring case; their parent stays, expanded. "All", "Default" and pattern rows always stay.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    partial void OnSearchTextChanged(string value) => Rebuild();

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        var sessions = _root.Mode == FilterMode.Databases ? _root.Session.OpenedDatabases.Prepend(_root.Session).ToList() : [_root.Session];
        foreach (var session in sessions)
        {
            await session.RefreshSchemaAsync(ct);
            _tree.Reload(session);
        }

        Rebuild();
    }

    [RelayCommand]
    private void ExpandAll()
    {
        foreach (var name in Items(_root).Where(n => Child(_root, n) is not null))
            _expanded.Add(name);
        Rebuild();
    }

    [RelayCommand]
    private void CollapseAll()
    {
        _expanded.Clear();
        Rebuild();
    }

    private NameVisibility Vis(FilterLevel level) =>
        (level.Mode switch
        {
            FilterMode.Databases => _tree.DatabaseLevel(_profileId),
            FilterMode.Schemas => _tree.SchemaLevel(level.Session),
            _ => _tree.TableLevel(level.Session),
        }) ?? new NameVisibility();

    private static string Prefix(FilterLevel level) => level.Mode == FilterMode.Tables && level.Schema.Length > 0 ? level.Schema + "." : "";

    private static List<string> Items(FilterLevel level)
    {
        var session = level.Session;
        switch (level.Mode)
        {
            case FilterMode.Databases:
                var own = session.Profile.Database;
                var names = new List<string>();
                if (own is { Length: > 0 })
                    names.Add(own);
                names.AddRange(session.Databases.Where(d => !string.Equals(d, own, StringComparison.OrdinalIgnoreCase)));
                return [.. names.Order(StringComparer.OrdinalIgnoreCase)];
            case FilterMode.Schemas:
                return [.. ExplorerTreeViewModel.SchemaNames(session.Schema).Order(StringComparer.OrdinalIgnoreCase)];
            default:
                return [.. session.Schema.Tables.Where(t => (t.Schema ?? "") == level.Schema).Select(t => t.Name)
                    .OrderBy(n => n.StartsWith('_') ? 0 : 1).ThenBy(n => n, StringComparer.OrdinalIgnoreCase)];
        }
    }

    private static string? DefaultOf(FilterLevel level, List<string> items) =>
        (level.Mode switch
        {
            FilterMode.Databases => level.Session.Profile.Database,
            FilterMode.Schemas => level.Session.Schema.DefaultSchema,
            _ => null,
        }) is { Length: > 0 } d ? items.Find(i => string.Equals(i, d, StringComparison.OrdinalIgnoreCase)) : null;

    private static FilterLevel? Child(FilterLevel level, string name)
    {
        switch (level.Mode)
        {
            case FilterMode.Databases:
                var session = string.Equals(name, level.Session.Profile.Database, StringComparison.OrdinalIgnoreCase) ? level.Session : level.Session.FindDatabase(name);
                if (session is null)
                    return null;
                return ExplorerTreeViewModel.SchemaNames(session.Schema).Count > 0 ? new(FilterMode.Schemas, session, "") : new(FilterMode.Tables, session, "");
            case FilterMode.Schemas:
                return new(FilterMode.Tables, level.Session, name);
            default:
                return null;
        }
    }

    private void Rebuild()
    {
        Rows.Clear();
        foreach (var row in Build(_root, 0, _root.Mode != FilterMode.Tables))
            Rows.Add(row);
    }

    // Applies the change to the tree at once; a toggle only updates the check boxes so the heads keep focus and scroll.
    private void Changed(bool structural)
    {
        _tree.ApplyVisibility(_profileId);
        if (structural)
        {
            Rebuild();
            return;
        }

        foreach (var row in Rows)
        {
            if (row.Evaluate is { } evaluate)
                row.IsChecked = evaluate();
        }
    }

    private static FilterRow Checked(FilterRow row)
    {
        if (row.Evaluate is { } evaluate)
            row.IsChecked = evaluate();
        return row;
    }

    private List<FilterRow> Build(FilterLevel level, int depth, bool twoLevels)
    {
        var rows = new List<FilterRow>();
        var prefix = Prefix(level);
        var items = Items(level);
        var keys = items.Select(i => prefix + i).ToList();
        var def = DefaultOf(level, items);
        var search = SearchText.Trim();
        var (allKey, defaultKey, noteKey) = level.Mode switch
        {
            FilterMode.Databases => ("explorer.filter.allDatabases", "explorer.filter.defaultDatabase", "explorer.filter.defaultDatabaseNote"),
            FilterMode.Schemas => ("explorer.filter.allSchemas", "explorer.filter.defaultSchema", "explorer.filter.defaultSchemaNote"),
            _ => ("explorer.filter.allTables", "", ""),
        };

        List<string> PatternsInScope() => [.. Vis(level).Patterns.Where(p => prefix.Length == 0 || p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))];

        void ToggleKey(string key)
        {
            var v = Vis(level);
            if (v.IsVisible(key))
                v.Hide(key);
            else
                v.Show(key);
            Changed(false);
        }

        bool AllChecked() => Vis(level).AllVisible(keys) && PatternsInScope().Count == 0;

        rows.Add(Checked(new FilterRow
        {
            Kind = FilterRowKind.All,
            Text = UiText.Get(allKey),
            Depth = depth,
            Evaluate = AllChecked,
            OnToggle = () =>
            {
                var v = Vis(level);
                if (AllChecked())
                    v.HideAll(keys);
                else
                    v.ShowAll(keys, prefix.Length > 0 ? prefix : null);
                Changed(false);
            },
            OnCommitPattern = text =>
            {
                text = text.Trim();
                if (text.Length == 0 || !Vis(level).AddPattern(prefix + text))
                    return false;
                Changed(true);
                return true;
            },
        }));

        if (def is not null)
        {
            var key = prefix + def;
            rows.Add(Checked(new FilterRow
            {
                Kind = FilterRowKind.Default,
                Text = UiText.Get(defaultKey),
                Note = UiText.Format("explorer.filter.note", def),
                Depth = depth,
                Evaluate = () => Vis(level).IsVisible(key),
                OnToggle = () => ToggleKey(key),
            }));
        }

        foreach (var stored in PatternsInScope())
        {
            rows.Add(new FilterRow
            {
                Kind = FilterRowKind.Pattern,
                Text = stored[prefix.Length..],
                Depth = depth,
                OnRemovePattern = () =>
                {
                    Vis(level).RemovePattern(stored);
                    Changed(true);
                },
            });
        }

        foreach (var name in items)
        {
            var key = prefix + name;
            var itemMatch = search.Length == 0 || name.Contains(search, StringComparison.OrdinalIgnoreCase);
            var child = Child(level, name);
            var childRows = child is { } c ? Build(c, depth + 1, twoLevels) : [];
            var childMatch = search.Length > 0 && childRows.Exists(r => r.Kind == FilterRowKind.Item);
            if (!itemMatch && !childMatch)
                continue;
            var expanded = child is not null && (childMatch || _expanded.Contains(name));
            FilterRow? self = null;
            self = Checked(new FilterRow
            {
                Kind = FilterRowKind.Item,
                Text = name,
                Note = def is not null && string.Equals(name, def, StringComparison.OrdinalIgnoreCase) ? UiText.Get(noteKey) : null,
                Depth = depth,
                IsStrong = depth == 0 && twoLevels,
                CanExpand = child is not null,
                IsExpanded = expanded,
                Evaluate = () => Vis(level).IsVisible(key),
                OnToggle = () => ToggleKey(key),
                OnToggleExpand = () =>
                {
                    if (self!.IsExpanded)
                        _expanded.Remove(name);
                    else
                        _expanded.Add(name);
                    Rebuild();
                },
            });
            rows.Add(self);
            if (expanded)
                rows.AddRange(childRows);
        }

        return rows;
    }
}
