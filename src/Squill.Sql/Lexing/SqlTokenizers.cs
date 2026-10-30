using Squill.Core;

namespace Squill.Sql.Lexing;

/// <summary>Factory for the tokenizer of a database dialect.</summary>
public static class SqlTokenizers
{
    private static readonly AnsiTokenizer Ansi = new();
    private static readonly PostgresTokenizer Postgres = new();
    private static readonly SqliteTokenizer Sqlite = new();
    private static readonly MySqlTokenizer MySql = new();
    private static readonly TSqlTokenizer TSql = new();

    /// <summary>Gets the tokenizer for a dialect.</summary>
    /// <param name="dialect">The database engine.</param>
    /// <returns>A shared, stateless tokenizer.</returns>
    public static ISqlTokenizer For(DatabaseDialect dialect) => dialect switch
    {
        DatabaseDialect.PostgreSql => Postgres,
        DatabaseDialect.Sqlite => Sqlite,
        DatabaseDialect.MySql => MySql,
        DatabaseDialect.SqlServer => TSql,
        _ => Ansi,
    };
}
