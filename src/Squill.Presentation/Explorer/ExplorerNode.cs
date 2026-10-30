using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Squill.Core;

namespace Squill.Presentation.Explorer;

/// <summary>What an <see cref="ExplorerNode"/> represents.</summary>
public enum ExplorerNodeKind
{
    /// <summary>A database schema.</summary>
    Schema,

    /// <summary>A table.</summary>
    Table,

    /// <summary>A view.</summary>
    View,

    /// <summary>A column.</summary>
    Column,

    /// <summary>An index.</summary>
    Index,

    /// <summary>A foreign key.</summary>
    ForeignKey,

    /// <summary>A group of columns, indexes or foreign keys.</summary>
    Folder,
}

/// <summary>One node of the object explorer tree.</summary>
/// <param name="title">The text shown for the node.</param>
/// <param name="kind">What the node represents.</param>
/// <param name="detail">Secondary text, such as a column type.</param>
/// <param name="table">The table or view behind a table or view node.</param>
public sealed partial class ExplorerNode(string title, ExplorerNodeKind kind, string? detail = null, TableInfo? table = null) : ViewModelBase
{
    /// <summary>Gets the text shown for the node.</summary>
    public string Title { get; } = title;

    /// <summary>Gets the secondary text, or <see langword="null"/>.</summary>
    public string? Detail { get; } = detail;

    /// <summary>Gets what the node represents.</summary>
    public ExplorerNodeKind Kind { get; } = kind;

    /// <summary>Gets the table or view behind a table or view node; <see langword="null"/> otherwise.</summary>
    public TableInfo? Table { get; } = table;

    /// <summary>Gets the child nodes.</summary>
    public ObservableCollection<ExplorerNode> Children { get; } = [];

    /// <summary>Gets or sets whether the node is expanded.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>Gets or sets whether the node passes the current filter.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;
}
