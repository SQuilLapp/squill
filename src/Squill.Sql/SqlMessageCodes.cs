namespace Squill.Sql;

/// <summary>Stable identifiers of the messages produced by the SQL parser, <see cref="Analysis.SqlDiagnosticsProvider"/> and <see cref="Analysis.SqlHoverProvider"/>; the UI maps each to a translated text.</summary>
public static class SqlMessageCodes
{
    /// <summary>The <c>sql.unexpected</c> message.</summary>
    public const string Unexpected = "sql.unexpected";

    /// <summary>The <c>sql.unexpectedToken</c> message.</summary>
    public const string UnexpectedToken = "sql.unexpectedToken";

    /// <summary>The <c>sql.expectedIdentifierAfterDot</c> message.</summary>
    public const string ExpectedIdentifierAfterDot = "sql.expectedIdentifierAfterDot";

    /// <summary>The <c>sql.expectedChar</c> message.</summary>
    public const string ExpectedChar = "sql.expectedChar";

    /// <summary>The <c>sql.expectedKeyword</c> message.</summary>
    public const string ExpectedKeyword = "sql.expectedKeyword";

    /// <summary>The <c>sql.expectedValuesOrQuery</c> message.</summary>
    public const string ExpectedValuesOrQuery = "sql.expectedValuesOrQuery";

    /// <summary>The <c>sql.expectedColumnName</c> message.</summary>
    public const string ExpectedColumnName = "sql.expectedColumnName";

    /// <summary>The <c>sql.expectedNameAfterDot</c> message.</summary>
    public const string ExpectedNameAfterDot = "sql.expectedNameAfterDot";

    /// <summary>The <c>sql.expectedCteName</c> message.</summary>
    public const string ExpectedCteName = "sql.expectedCteName";

    /// <summary>The <c>sql.expectedSelectItem</c> message.</summary>
    public const string ExpectedSelectItem = "sql.expectedSelectItem";

    /// <summary>The <c>sql.expectedAlias</c> message.</summary>
    public const string ExpectedAlias = "sql.expectedAlias";

    /// <summary>The <c>sql.expectedExpression</c> message.</summary>
    public const string ExpectedExpression = "sql.expectedExpression";

    /// <summary>The <c>sql.expectedTypeName</c> message.</summary>
    public const string ExpectedTypeName = "sql.expectedTypeName";

    /// <summary>The <c>sql.expectedTableName</c> message.</summary>
    public const string ExpectedTableName = "sql.expectedTableName";

    /// <summary>The <c>sql.unknownTable</c> message.</summary>
    public const string UnknownTable = "sql.unknownTable";

    /// <summary>The <c>sql.unknownColumn</c> message.</summary>
    public const string UnknownColumn = "sql.unknownColumn";

    /// <summary>The <c>sql.hover.table</c> message.</summary>
    public const string HoverTable = "sql.hover.table";

    /// <summary>The <c>sql.hover.view</c> message.</summary>
    public const string HoverView = "sql.hover.view";

    /// <summary>The <c>sql.hover.column.nullable</c> message.</summary>
    public const string HoverColumnNullable = "sql.hover.column.nullable";

    /// <summary>The <c>sql.hover.column.notNull</c> message.</summary>
    public const string HoverColumnNotNull = "sql.hover.column.notNull";

    /// <summary>The <c>sql.hover.column.nullablePk</c> message.</summary>
    public const string HoverColumnNullablePk = "sql.hover.column.nullablePk";

    /// <summary>The <c>sql.hover.column.notNullPk</c> message.</summary>
    public const string HoverColumnNotNullPk = "sql.hover.column.notNullPk";

    /// <summary>Every code of this class.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Unexpected,
        UnexpectedToken,
        ExpectedIdentifierAfterDot,
        ExpectedChar,
        ExpectedKeyword,
        ExpectedValuesOrQuery,
        ExpectedColumnName,
        ExpectedNameAfterDot,
        ExpectedCteName,
        ExpectedSelectItem,
        ExpectedAlias,
        ExpectedExpression,
        ExpectedTypeName,
        ExpectedTableName,
        UnknownTable,
        UnknownColumn,
        HoverTable,
        HoverView,
        HoverColumnNullable,
        HoverColumnNotNull,
        HoverColumnNullablePk,
        HoverColumnNotNullPk,
    ];
}
