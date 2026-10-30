using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Presentation.Sessions;

namespace Squill.Presentation.Explorer;

/// <summary>Shows the schemas, tables, views and columns of a session as a filterable tree.</summary>
public sealed partial class ObjectExplorerViewModel : ViewModelBase
{
    private readonly ConnectionSession _session;

    /// <summary>Creates the view-model and builds the tree from the session's current schema.</summary>
    /// <param name="session">The session whose schema is shown.</param>
    public ObjectExplorerViewModel(ConnectionSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        Rebuild();
    }

    /// <summary>Raised when the user opens a table or view.</summary>
    public event EventHandler<TableInfo>? OpenTableRequested;

    /// <summary>Gets the top-level nodes: schemas when there are several, otherwise tables and views.</summary>
    public ObservableCollection<ExplorerNode> Roots { get; } = [];

    /// <summary>Gets or sets the text that node titles must contain, ignoring case; empty shows everything.</summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = "";

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        await _session.RefreshSchemaAsync(ct);
        Rebuild();
    }

    [RelayCommand]
    private void Activate(ExplorerNode? node)
    {
        if (node is { Kind: ExplorerNodeKind.Table or ExplorerNodeKind.View, Table: { } table })
            OpenTableRequested?.Invoke(this, table);
    }

    private void Rebuild()
    {
        Roots.Clear();
        var tables = _session.Schema.Tables;
        if (tables.Select(t => t.Schema ?? "").Distinct(StringComparer.Ordinal).Count() > 1)
        {
            foreach (var group in tables.GroupBy(t => t.Schema ?? "", StringComparer.Ordinal))
            {
                var schema = new ExplorerNode(group.Key, ExplorerNodeKind.Schema);
                foreach (var table in group)
                    schema.Children.Add(TableNode(table));
                Roots.Add(schema);
            }
        }
        else
        {
            foreach (var table in tables)
                Roots.Add(TableNode(table));
        }
        ApplyFilter();
    }

    private static ExplorerNode TableNode(TableInfo table)
    {
        var node = new ExplorerNode(table.Name, table.Kind == TableKind.View ? ExplorerNodeKind.View : ExplorerNodeKind.Table, table: table);
        var columns = new ExplorerNode("Columns", ExplorerNodeKind.Folder);
        foreach (var c in table.Columns)
        {
            var detail = c.DataType + (c.IsPrimaryKey ? " PK" : "") + (c.IsNullable ? " NULL" : "");
            columns.Children.Add(new ExplorerNode(c.Name, ExplorerNodeKind.Column, detail));
        }
        var indexes = new ExplorerNode("Indexes", ExplorerNodeKind.Folder);
        foreach (var i in table.Indexes)
            indexes.Children.Add(new ExplorerNode(i.Name, ExplorerNodeKind.Index, (i.IsUnique ? "UNIQUE " : "") + string.Join(", ", i.Columns)));
        var keys = new ExplorerNode("Foreign keys", ExplorerNodeKind.Folder);
        foreach (var k in table.ForeignKeys)
        {
            var target = k.ReferencedSchema is { Length: > 0 } s ? $"{s}.{k.ReferencedTable}" : k.ReferencedTable;
            keys.Children.Add(new ExplorerNode(k.Name, ExplorerNodeKind.ForeignKey, $"{string.Join(", ", k.Columns)} -> {target}"));
        }
        node.Children.Add(columns);
        node.Children.Add(indexes);
        node.Children.Add(keys);
        return node;
    }

    private void ApplyFilter()
    {
        var filter = FilterText.Trim();
        foreach (var root in Roots)
            Apply(root, filter);
    }

    private static bool Apply(ExplorerNode node, string filter)
    {
        var visible = filter.Length == 0 || node.Title.Contains(filter, StringComparison.OrdinalIgnoreCase);
        foreach (var child in node.Children)
            visible |= Apply(child, filter);
        node.IsVisible = visible;
        return visible;
    }
}
