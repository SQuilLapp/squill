using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Squill.Core;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;
using Squill.Presentation.Sessions;

namespace Squill.Presentation.Explorer;

/// <summary>What an <see cref="ExplorerNode"/> represents.</summary>
public enum ExplorerNodeKind
{
    /// <summary>A saved connection, the root of its objects.</summary>
    Connection,

    /// <summary>The database of a connected connection.</summary>
    Database,

    /// <summary>A database schema.</summary>
    Schema,

    /// <summary>A table.</summary>
    Table,

    /// <summary>A view.</summary>
    View,

    /// <summary>A materialized view.</summary>
    MaterializedView,

    /// <summary>A stored function.</summary>
    Function,

    /// <summary>A stored procedure.</summary>
    Procedure,

    /// <summary>A trigger.</summary>
    Trigger,

    /// <summary>A column.</summary>
    Column,

    /// <summary>An index.</summary>
    Index,

    /// <summary>A primary or unique key.</summary>
    Key,

    /// <summary>A foreign key.</summary>
    ForeignKey,

    /// <summary>A group of tables, views, columns, indexes or foreign keys.</summary>
    Folder,

    /// <summary>A folder that groups saved connections.</summary>
    ConnectionFolder,
}

/// <summary>One node of the object explorer tree.</summary>
/// <param name="title">The text shown for the node.</param>
/// <param name="kind">What the node represents.</param>
/// <param name="detail">Secondary text, such as a column type.</param>
/// <param name="table">The table or view behind a table or view node.</param>
/// <param name="count">The number of objects under a schema or folder node.</param>
public sealed partial class ExplorerNode(string title, ExplorerNodeKind kind, string? detail = null, TableInfo? table = null, int? count = null) : ViewModelBase
{
    /// <summary>Gets the text shown for the node.</summary>
    public string Title { get; } = title;

    /// <summary>Gets or sets the secondary text, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Trailing))]
    public partial string? Detail { get; set; } = detail;

    /// <summary>Gets or sets how many tables and views of a database node the user hid.</summary>
    [ObservableProperty]
    public partial int HiddenCount { get; set; }

    /// <summary>Gets the number of objects under a schema or folder node, or <see langword="null"/>. Static: the filter does not change it.</summary>
    public int? Count { get; } = count;

    /// <summary>Gets the one text the heads draw right-aligned: the <see cref="ErrorText"/>, else <see cref="Detail"/>, else the formatted <see cref="Count"/>, else <see langword="null"/>.</summary>
    public string? Trailing => ErrorText ?? Detail ?? (Count is { } n ? DisplayFormat.Count(n) : null);

    /// <summary>Gets or sets the message of the last failure to open the database behind a database node, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Trailing))]
    public partial string? ErrorText { get; set; }

    /// <summary>Gets what the node represents.</summary>
    public ExplorerNodeKind Kind { get; } = kind;

    /// <summary>Gets the table or view behind a table or view node; <see langword="null"/> otherwise.</summary>
    public TableInfo? Table { get; } = table;

    /// <summary>Gets or sets whether the connection behind this node is being opened, so the explorer can show a progress indicator.</summary>
    [ObservableProperty]
    public partial bool IsConnecting { get; set; }

    /// <summary>Gets the function or procedure behind a routine node; <see langword="null"/> otherwise.</summary>
    public RoutineInfo? Routine { get; init; }

    /// <summary>Gets the trigger behind a trigger node; <see langword="null"/> otherwise.</summary>
    public TriggerInfo? Trigger { get; init; }

    /// <summary>Gets the stored definition text of a view, routine or trigger node, or <see langword="null"/>; read-only text for a properties panel.</summary>
    public string? DefinitionText => Table?.Definition ?? Routine?.Definition ?? Trigger?.Definition;

    /// <summary>Gets the saved connection the node belongs to; <see langword="null"/> for connection folders.</summary>
    public ConnectionProfile? Profile { get; init; }

    /// <summary>Gets or sets the open session of the connection; <see langword="null"/> while a connection node is disconnected.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    public partial ConnectionSession? Session { get; set; }

    /// <summary>Gets whether this is a connection node with an open session.</summary>
    public bool IsConnected => Kind == ExplorerNodeKind.Connection && Session is not null;

    /// <summary>Gets the two-letter engine badge text of a connection node.</summary>
    public string EngineLetters => Profile?.Dialect switch
    {
        DatabaseDialect.PostgreSql => "PG",
        DatabaseDialect.MySql => "My",
        DatabaseDialect.SqlServer => "MS",
        DatabaseDialect.Sqlite => "SL",
        _ => "",
    };

    /// <summary>Gets whether the engine is PostgreSQL.</summary>
    public bool IsPostgres => Profile?.Dialect == DatabaseDialect.PostgreSql;

    /// <summary>Gets whether the engine is MySQL.</summary>
    public bool IsMySql => Profile?.Dialect == DatabaseDialect.MySql;

    /// <summary>Gets whether the engine is SQL Server.</summary>
    public bool IsSqlServer => Profile?.Dialect == DatabaseDialect.SqlServer;

    /// <summary>Gets whether the engine is SQLite.</summary>
    public bool IsSqlite => Profile?.Dialect == DatabaseDialect.Sqlite;

    /// <summary>Gets whether the node is a connection with an environment to show as a pill.</summary>
    public bool HasEnvironment => Kind == ExplorerNodeKind.Connection && Profile is { Environment: not ConnectionEnvironment.None };

    /// <summary>Gets whether the environment is production.</summary>
    public bool IsProd => Profile?.Environment == ConnectionEnvironment.Production;

    /// <summary>Gets whether the environment is staging.</summary>
    public bool IsStaging => Profile?.Environment == ConnectionEnvironment.Staging;

    /// <summary>Gets whether the environment is development.</summary>
    public bool IsDev => Profile?.Environment == ConnectionEnvironment.Development;

    /// <summary>Gets whether the environment is local.</summary>
    public bool IsLocal => Profile?.Environment == ConnectionEnvironment.Local;

    /// <summary>Gets the caps label of the environment pill.</summary>
    public string EnvironmentText => Profile?.Environment switch
    {
        ConnectionEnvironment.Production => UiText.Get("status.production"),
        ConnectionEnvironment.Staging => UiText.Get("status.staging"),
        ConnectionEnvironment.Development => UiText.Get("status.dev"),
        ConnectionEnvironment.Local => UiText.Get("status.local"),
        _ => "",
    };

    /// <summary>Gets the child nodes.</summary>
    public ObservableCollection<ExplorerNode> Children { get; } = [];

    /// <summary>Gets or sets whether the node is expanded.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>Gets or sets whether the node passes the current filter.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;
}
