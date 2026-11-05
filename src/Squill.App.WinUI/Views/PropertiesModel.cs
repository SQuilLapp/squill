using Squill.Core;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;

namespace Squill.App.WinUI.Views;

/// <summary>One column line of the properties panel.</summary>
/// <param name="Name">Column name.</param>
/// <param name="DataType">Data type as the engine reports it.</param>
/// <param name="IsPrimaryKey">Whether the column is part of the primary key.</param>
/// <param name="IsForeignKey">Whether the column is part of a foreign key.</param>
public sealed record ColumnRow(string Name, string DataType, bool IsPrimaryKey, bool IsForeignKey)
{
    /// <summary>Gets whether the column is neither a primary nor a foreign key.</summary>
    public bool IsPlain => !IsPrimaryKey && !IsForeignKey;

    /// <summary>Gets whether the column is a foreign key and not a primary key.</summary>
    public bool IsForeignOnly => IsForeignKey && !IsPrimaryKey;
}

/// <summary>What the properties panel shows for the selected table or view.</summary>
/// <param name="Table">The described table or view.</param>
public sealed record PropertiesModel(TableInfo Table)
{
    /// <summary>Gets whether the object is a view.</summary>
    public bool IsView => Table.Kind.IsViewLike();

    /// <summary>Gets the subtitle: the kind, then the schema when there is one.</summary>
    public string Subtitle
    {
        get
        {
            var kind = UiText.Get(IsView ? "properties.kindView" : "properties.kindTable");
            return string.IsNullOrEmpty(Table.Schema) ? kind : $"{kind} · {Table.Schema}";
        }
    }

    /// <summary>Gets the header above the column list.</summary>
    public string ColumnsHeader => UiText.Format("properties.columns", Table.Columns.Count);

    /// <summary>Gets the header above the index list.</summary>
    public string IndexesHeader => UiText.Format("properties.indexes", Table.Indexes.Count);

    /// <summary>Gets the column lines in table order.</summary>
    public ColumnRow[] Columns =>
        Table.Columns.Select(c => new ColumnRow(c.Name, c.DataType, c.IsPrimaryKey, Table.ForeignKeys.Any(k => k.Columns.Contains(c.Name)))).ToArray();

    /// <summary>Gets the index names.</summary>
    public string[] Indexes => Table.Indexes.Select(i => i.Name).ToArray();

    /// <summary>Builds the model of an explorer node.</summary>
    /// <param name="node">The selected node, or <see langword="null"/>.</param>
    /// <returns>The model for a table or view node; <see langword="null"/> for any other node, which hides the panel.</returns>
    public static PropertiesModel? For(ExplorerNode? node) =>
        node is { Kind: ExplorerNodeKind.Table or ExplorerNodeKind.View or ExplorerNodeKind.MaterializedView, Table: { } table } ? new PropertiesModel(table) : null;
}
