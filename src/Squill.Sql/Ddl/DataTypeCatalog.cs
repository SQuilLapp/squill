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

    private static readonly string[] Oracle =
    [
        "NUMBER(10)", "NUMBER(18,2)", "FLOAT", "BINARY_FLOAT", "BINARY_DOUBLE", "VARCHAR2(255)", "NVARCHAR2(255)", "CHAR(1)",
        "CLOB", "BLOB", "RAW(16)", "DATE", "TIMESTAMP", "TIMESTAMP WITH TIME ZONE", "TIMESTAMP WITH LOCAL TIME ZONE", "JSON",
    ];

    private static readonly string[] Informix =
    [
        "SMALLINT", "INTEGER", "INT8", "BIGINT", "SERIAL", "SERIAL8", "BIGSERIAL", "DECIMAL(16,2)", "MONEY(16,2)", "SMALLFLOAT", "FLOAT",
        "VARCHAR(255)", "LVARCHAR(2048)", "CHAR(1)", "NVARCHAR(255)", "BOOLEAN", "DATE", "DATETIME YEAR TO SECOND",
        "DATETIME YEAR TO FRACTION(5)", "INTERVAL DAY(3) TO SECOND", "BYTE", "TEXT", "BLOB", "CLOB",
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
        DatabaseDialect.Oracle => Oracle,
        DatabaseDialect.Informix => Informix,
        _ => Sqlite,
    };
}
