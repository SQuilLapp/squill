using Squill.Core;

namespace Squill.Sql.Ddl;

/// <summary>Common column types per dialect, offered as suggestions by the table designer.</summary>
public static class DataTypeCatalog
{
    private static readonly string[] PostgreSql =
    [
        "smallint", "integer", "bigint", "numeric(18,2)", "real", "double precision", "boolean", "text",
        "varchar(255)", "char(1)", "date", "time", "timestamp", "timestamptz", "uuid", "jsonb", "bytea",
    ];

    private static readonly string[] MySql =
    [
        "tinyint", "smallint", "int", "bigint", "decimal(18,2)", "float", "double", "tinyint(1)", "text",
        "varchar(255)", "char(1)", "date", "time", "datetime", "timestamp", "json", "blob",
    ];

    private static readonly string[] SqlServer =
    [
        "smallint", "int", "bigint", "decimal(18,2)", "real", "float", "bit", "nvarchar(max)", "nvarchar(255)",
        "nchar(1)", "date", "time", "datetime2", "datetimeoffset", "uniqueidentifier", "varbinary(max)",
    ];

    private static readonly string[] Sqlite = ["INTEGER", "REAL", "TEXT", "BLOB", "NUMERIC"];

    /// <summary>Gets the ordered type suggestions of a dialect.</summary>
    /// <param name="d">The database engine.</param>
    /// <returns>Valid type texts for that engine.</returns>
    public static IReadOnlyList<string> For(DatabaseDialect d) => d switch
    {
        DatabaseDialect.PostgreSql => PostgreSql,
        DatabaseDialect.MySql => MySql,
        DatabaseDialect.SqlServer => SqlServer,
        _ => Sqlite,
    };
}
