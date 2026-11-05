namespace Squill.Core;

/// <summary>Stable identifiers of the messages produced by <see cref="Design.DesignValidator"/>, the designer view-model and the edit model; the UI maps each to a translated text.</summary>
public static class CoreMessageCodes
{
    /// <summary>The <c>design.tableNameEmpty</c> message.</summary>
    public const string DesignTableNameEmpty = "design.tableNameEmpty";

    /// <summary>The <c>design.noColumn</c> message.</summary>
    public const string DesignNoColumn = "design.noColumn";

    /// <summary>The <c>design.columnNameEmpty</c> message.</summary>
    public const string DesignColumnNameEmpty = "design.columnNameEmpty";

    /// <summary>The <c>design.columnNameDuplicate</c> message.</summary>
    public const string DesignColumnNameDuplicate = "design.columnNameDuplicate";

    /// <summary>The <c>design.columnTypeMissing</c> message.</summary>
    public const string DesignColumnTypeMissing = "design.columnTypeMissing";

    /// <summary>The <c>design.pkColumnMissing</c> message.</summary>
    public const string DesignPkColumnMissing = "design.pkColumnMissing";

    /// <summary>The <c>design.pkColumnDuplicate</c> message.</summary>
    public const string DesignPkColumnDuplicate = "design.pkColumnDuplicate";

    /// <summary>The <c>design.pkColumnNullable</c> message.</summary>
    public const string DesignPkColumnNullable = "design.pkColumnNullable";

    /// <summary>The <c>design.autoIncrementMultiple</c> message.</summary>
    public const string DesignAutoIncrementMultiple = "design.autoIncrementMultiple";

    /// <summary>The <c>design.autoIncrementNotOnlyPk</c> message.</summary>
    public const string DesignAutoIncrementNotOnlyPk = "design.autoIncrementNotOnlyPk";

    /// <summary>The <c>design.autoIncrementNotInPk</c> message.</summary>
    public const string DesignAutoIncrementNotInPk = "design.autoIncrementNotInPk";

    /// <summary>The <c>design.autoIncrementChange</c> message.</summary>
    public const string DesignAutoIncrementChange = "design.autoIncrementChange";

    /// <summary>The <c>design.indexNameEmpty</c> message.</summary>
    public const string DesignIndexNameEmpty = "design.indexNameEmpty";

    /// <summary>The <c>design.indexNameDuplicate</c> message.</summary>
    public const string DesignIndexNameDuplicate = "design.indexNameDuplicate";

    /// <summary>The <c>design.indexNoColumn</c> message.</summary>
    public const string DesignIndexNoColumn = "design.indexNoColumn";

    /// <summary>The <c>design.indexColumnMissing</c> message.</summary>
    public const string DesignIndexColumnMissing = "design.indexColumnMissing";

    /// <summary>The <c>design.foreignKeyNameEmpty</c> message.</summary>
    public const string DesignForeignKeyNameEmpty = "design.foreignKeyNameEmpty";

    /// <summary>The <c>design.foreignKeyNameDuplicate</c> message.</summary>
    public const string DesignForeignKeyNameDuplicate = "design.foreignKeyNameDuplicate";

    /// <summary>The <c>design.foreignKeyNoColumn</c> message.</summary>
    public const string DesignForeignKeyNoColumn = "design.foreignKeyNoColumn";

    /// <summary>The <c>design.foreignKeyColumnMissing</c> message.</summary>
    public const string DesignForeignKeyColumnMissing = "design.foreignKeyColumnMissing";

    /// <summary>The <c>design.foreignKeyColumnCount</c> message.</summary>
    public const string DesignForeignKeyColumnCount = "design.foreignKeyColumnCount";

    /// <summary>The <c>design.foreignKeyNoTable</c> message.</summary>
    public const string DesignForeignKeyNoTable = "design.foreignKeyNoTable";

    /// <summary>The <c>design.indexNeedsLength</c> message.</summary>
    public const string DesignIndexNeedsLength = "design.indexNeedsLength";

    /// <summary>The <c>design.schemaMove</c> message.</summary>
    public const string DesignSchemaMove = "design.schemaMove";

    /// <summary>The <c>design.sqliteSchema</c> message.</summary>
    public const string DesignSqliteSchema = "design.sqliteSchema";

    /// <summary>The <c>design.dropColumnDataLoss</c> message.</summary>
    public const string DesignDropColumnDataLoss = "design.dropColumnDataLoss";

    /// <summary>The <c>design.typeChangeRisk</c> message.</summary>
    public const string DesignTypeChangeRisk = "design.typeChangeRisk";

    /// <summary>The <c>design.nullsBlockNotNull</c> message.</summary>
    public const string DesignNullsBlockNotNull = "design.nullsBlockNotNull";

    /// <summary>The <c>design.foreignKeyTableMissing</c> message.</summary>
    public const string DesignForeignKeyTableMissing = "design.foreignKeyTableMissing";

    /// <summary>The <c>design.viewsNotRecreated</c> message.</summary>
    public const string DesignViewsNotRecreated = "design.viewsNotRecreated";

    /// <summary>The <c>edit.columnNull</c> message.</summary>
    public const string EditColumnNull = "edit.columnNull";

    /// <summary>The <c>edit.columnRequired</c> message.</summary>
    public const string EditColumnRequired = "edit.columnRequired";

    /// <summary>The <c>edit.duplicateKey</c> message.</summary>
    public const string EditDuplicateKey = "edit.duplicateKey";

    /// <summary>The <c>connection.connectTimeoutRange</c> message.</summary>
    public const string ConnectionConnectTimeoutRange = "connection.connectTimeoutRange";

    /// <summary>The <c>connection.commandTimeoutRange</c> message.</summary>
    public const string ConnectionCommandTimeoutRange = "connection.commandTimeoutRange";

    /// <summary>Every code of this class.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        DesignTableNameEmpty,
        DesignNoColumn,
        DesignColumnNameEmpty,
        DesignColumnNameDuplicate,
        DesignColumnTypeMissing,
        DesignPkColumnMissing,
        DesignPkColumnDuplicate,
        DesignPkColumnNullable,
        DesignAutoIncrementMultiple,
        DesignAutoIncrementNotOnlyPk,
        DesignAutoIncrementNotInPk,
        DesignAutoIncrementChange,
        DesignIndexNameEmpty,
        DesignIndexNameDuplicate,
        DesignIndexNoColumn,
        DesignIndexColumnMissing,
        DesignForeignKeyNameEmpty,
        DesignForeignKeyNameDuplicate,
        DesignForeignKeyNoColumn,
        DesignForeignKeyColumnMissing,
        DesignForeignKeyColumnCount,
        DesignForeignKeyNoTable,
        DesignIndexNeedsLength,
        DesignSchemaMove,
        DesignSqliteSchema,
        DesignDropColumnDataLoss,
        DesignTypeChangeRisk,
        DesignNullsBlockNotNull,
        DesignForeignKeyTableMissing,
        DesignViewsNotRecreated,
        EditColumnNull,
        EditColumnRequired,
        EditDuplicateKey,
        ConnectionConnectTimeoutRange,
        ConnectionCommandTimeoutRange,
    ];
}
