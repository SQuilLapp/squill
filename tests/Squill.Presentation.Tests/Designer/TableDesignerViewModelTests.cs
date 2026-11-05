using Squill.Core;
using Squill.Core.Design;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Presentation.Designer;
using Squill.Presentation.Localization;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Designer;

public sealed class TableDesignerViewModelTests
{
    private static readonly TableInfo People = new(
        "public", "people", TableKind.Table,
        [new ColumnDefinition("id", "integer", false, true, null, true), new ColumnDefinition("name", "text", true, false, null)],
        [], [], "people_pkey");

    private sealed class FakeReader : IResultReader
    {
        public IReadOnlyList<ColumnInfo> Columns => [];
        public long RecordsAffected => -1;
        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken) => ValueTask.FromResult<object?[][]?>(null);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeConnection : IDatabaseConnection
    {
        public List<string> Executed { get; } = [];
        public string? FailOn { get; set; }
        public SchemaSnapshot Next { get; set; } = SchemaSnapshot.Empty;
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            Executed.Add(sql);
            if (FailOn is not null && sql.Contains(FailOn, StringComparison.Ordinal))
                throw new InvalidOperationException("boom failed");
            return ValueTask.FromResult<IResultReader>(new FakeReader());
        }

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Next);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Fixture
    {
        public Fixture(DatabaseDialect dialect = DatabaseDialect.PostgreSql, TableInfo? existing = null, params TableInfo[] others)
        {
            var snapshot = new SchemaSnapshot(dialect == DatabaseDialect.Sqlite ? "main" : "public", existing is null ? others : [existing, .. others]);
            Session = new ConnectionSession(
                new ConnectionProfile(Guid.NewGuid(), "p", dialect, "h", 1, "d", "u", null, null),
                Connection, snapshot, null);
            Vm = new TableDesignerViewModel(Session, existing, Dialogs, new ImmediateDispatcher());
        }

        public FakeConnection Connection { get; } = new();
        public FakeDialogService Dialogs { get; } = new();
        public ConnectionSession Session { get; }
        public TableDesignerViewModel Vm { get; }
    }

    [Theory]
    [InlineData(UiLanguage.French, "Nouvelle table")]
    [InlineData(UiLanguage.English, "New table")]
    public void NewTable_Title_IsInTheChosenLanguage(UiLanguage language, string title)
    {
        using var _ = UiText.Override(language);
        var f = new Fixture();

        Assert.Equal(title, f.Vm.Title);
    }

    [Fact]
    public void New_Table_StartsWithIdColumn()
    {
        var f = new Fixture();

        Assert.Equal("Nouvelle table", f.Vm.Title);
        Assert.Equal("new_table", f.Vm.Name);
        Assert.Equal("public", f.Vm.Schema);
        var id = Assert.Single(f.Vm.Columns);
        Assert.Equal(("id", "integer", false, true, true), (id.Name, id.DataType, id.IsNullable, id.IsPrimaryKey, id.IsAutoIncrement));
        Assert.Null(f.Vm.Original);
        Assert.True(f.Vm.HasChanges);
        Assert.Contains("CREATE TABLE", f.Vm.Script, StringComparison.Ordinal);
        Assert.True(f.Vm.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void Edits_RebuildDesignAndScript()
    {
        var f = new Fixture();

        f.Vm.Name = "orders";
        f.Vm.AddColumnCommand.Execute(null);
        f.Vm.Columns[1].Name = "total";
        f.Vm.AddIndexCommand.Execute(null);
        f.Vm.Indexes[0].ColumnsText = " total , id ,";

        Assert.Equal("orders", f.Vm.Design.Name);
        Assert.Equal(["id", "total"], f.Vm.Design.Columns.Select(c => c.Name));
        Assert.Equal(["id"], f.Vm.Design.PrimaryKey);
        Assert.Equal(["total", "id"], f.Vm.Design.Indexes[0].Columns);
        Assert.Contains("orders", f.Vm.Script, StringComparison.Ordinal);
        Assert.Contains("total", f.Vm.Script, StringComparison.Ordinal);

        f.Vm.MoveColumnUpCommand.Execute(f.Vm.Columns[1]);
        Assert.Equal(["total", "id"], f.Vm.Design.Columns.Select(c => c.Name));
        f.Vm.RemoveColumnCommand.Execute(f.Vm.Columns[0]);
        Assert.Equal(["id"], f.Vm.Design.Columns.Select(c => c.Name));
    }

    [Fact]
    public async Task Error_BlocksApply()
    {
        var f = new Fixture();

        f.Vm.Name = "";

        Assert.True(f.Vm.HasErrors);
        Assert.Equal(DesignIssueSeverity.Error, f.Vm.Issues[0].Severity);
        Assert.Equal("", f.Vm.Script);
        Assert.False(f.Vm.ApplyCommand.CanExecute(null));
        await f.Vm.ApplyCommand.ExecuteAsync(null);
        Assert.Empty(f.Connection.Executed);
    }

    [Fact]
    public void PrimaryKeyFlag_ForcesNonNullable()
    {
        var f = new Fixture();
        f.Vm.AddColumnCommand.Execute(null);
        var row = f.Vm.Columns[1];
        Assert.True(row.IsNullable);

        row.IsPrimaryKey = true;

        Assert.False(row.IsNullable);
        Assert.Equal(["id", row.Name], f.Vm.Design.PrimaryKey);
    }

    [Fact]
    public void ForeignKey_MissingTarget_IsError_SelfReferenceIsNot()
    {
        var f = new Fixture(DatabaseDialect.PostgreSql, null, People);
        f.Vm.AddForeignKeyCommand.Execute(null);
        var fk = f.Vm.ForeignKeys[0];
        fk.ColumnsText = "id";
        fk.ReferencedColumnsText = "id";

        fk.ReferencedTable = "ghosts";
        Assert.Contains(f.Vm.Issues, i => i.Severity == DesignIssueSeverity.Error && i.Message.Contains("ghosts", StringComparison.Ordinal));
        Assert.True(f.Vm.HasErrors);

        fk.ReferencedTable = "people";
        Assert.DoesNotContain(f.Vm.Issues, i => i.Message.Contains("ghosts", StringComparison.Ordinal));

        fk.ReferencedTable = f.Vm.Name;
        Assert.DoesNotContain(f.Vm.Issues, i => i.Message.Contains("ghosts", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "BEGIN", "COMMIT")]
    [InlineData(DatabaseDialect.SqlServer, "BEGIN TRANSACTION", "COMMIT TRANSACTION")]
    public async Task Apply_Transactional_BracketsStatements(DatabaseDialect dialect, string begin, string commit)
    {
        var f = new Fixture(dialect);

        await f.Vm.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(begin, f.Connection.Executed[0]);
        Assert.StartsWith("CREATE TABLE", f.Connection.Executed[1], StringComparison.Ordinal);
        Assert.Equal(commit, f.Connection.Executed[^1]);
    }

    [Fact]
    public async Task Apply_MySql_HasNoBracket()
    {
        var f = new Fixture(DatabaseDialect.MySql);

        await f.Vm.ApplyCommand.ExecuteAsync(null);

        Assert.All(f.Connection.Executed, s => Assert.StartsWith("CREATE TABLE", s, StringComparison.Ordinal));
    }

    private static TableInfo SqliteTable() => new(
        "main", "people", TableKind.Table,
        [new ColumnDefinition("id", "INTEGER", false, true, null, true), new ColumnDefinition("name", "TEXT", true, false, null)],
        [], [], null);

    [Fact]
    public async Task Apply_SqliteRebuild_KeepsPragmaOutsideTransaction()
    {
        var f = new Fixture(DatabaseDialect.Sqlite, SqliteTable());
        f.Vm.Columns[1].DataType = "INTEGER";

        await f.Vm.ApplyCommand.ExecuteAsync(null);

        var run = f.Connection.Executed;
        Assert.StartsWith("PRAGMA", run[0], StringComparison.Ordinal);
        Assert.Equal("BEGIN", run[1]);
        Assert.Equal("COMMIT", run[^3]);
        Assert.StartsWith("PRAGMA", run[^2], StringComparison.Ordinal);
        Assert.StartsWith("PRAGMA", run[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_Failure_RollsBackAndKeepsDraft()
    {
        var f = new Fixture();
        f.Vm.Name = "orders";
        f.Connection.FailOn = "CREATE TABLE";

        await f.Vm.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(["BEGIN", f.Connection.Executed[1], "ROLLBACK"], f.Connection.Executed);
        var error = Assert.Single(f.Dialogs.Errors);
        Assert.Equal("Échec de l'application", error.Title);
        Assert.Contains("boom failed", error.Message, StringComparison.Ordinal);
        Assert.Equal("orders", f.Vm.Name);
        Assert.True(f.Vm.HasChanges);
        Assert.False(f.Vm.IsApplying);
    }

    [Fact]
    public async Task Apply_MySqlFailure_MentionsNoRollback()
    {
        var f = new Fixture(DatabaseDialect.MySql);
        f.Connection.FailOn = "CREATE TABLE";

        await f.Vm.ApplyCommand.ExecuteAsync(null);

        Assert.DoesNotContain("ROLLBACK", f.Connection.Executed);
        Assert.Contains("MySQL n'annule pas le DDL", Assert.Single(f.Dialogs.Errors).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(UiLanguage.French, "Échec de l'application", "MySQL n'annule pas le DDL", "boom failed")]
    [InlineData(UiLanguage.English, "Apply failed", "MySQL does not roll back DDL", "boom failed")]
    public async Task Apply_Failure_HasTranslatedHeadingAndRawEngineMessage(UiLanguage language, string heading, string note, string raw)
    {
        using var _ = UiText.Override(language);
        var f = new Fixture(DatabaseDialect.MySql);
        f.Connection.FailOn = "CREATE TABLE";

        await f.Vm.ApplyCommand.ExecuteAsync(null);

        var (title, message) = Assert.Single(f.Dialogs.Errors);
        Assert.Equal(heading, title);
        Assert.Equal(heading, f.Vm.StatusText);
        Assert.Contains(raw, message, StringComparison.Ordinal);
        Assert.Contains(note, message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(UiLanguage.French, "Appliquer les modifications", "Les vues qui dépendent de cette table ne sont pas recréées.", "instruction")]
    [InlineData(UiLanguage.English, "Apply changes", "Views depending on this table are not recreated.", "statement")]
    public async Task Apply_Warning_Confirmation_IsInTheChosenLanguage(UiLanguage language, string heading, string warning, string unit)
    {
        using var _ = UiText.Override(language);
        var view = new TableInfo("main", "v", TableKind.View, [], [], [], null);
        var f = new Fixture(DatabaseDialect.Sqlite, SqliteTable(), view);
        f.Vm.Columns[1].Name = "title";

        await f.Vm.ApplyCommand.ExecuteAsync(null);

        var (title, message) = Assert.Single(f.Dialogs.Confirms);
        Assert.Equal(heading, title);
        Assert.Equal(warning, message);
        Assert.Contains(unit, f.Vm.StatusText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(UiLanguage.French, "La table n'a aucune colonne.")]
    [InlineData(UiLanguage.English, "The table has no column.")]
    public void ErrorIssue_IsInTheChosenLanguage(UiLanguage language, string text)
    {
        using var _ = UiText.Override(language);
        var f = new Fixture();

        f.Vm.Columns.Clear();

        Assert.Contains(f.Vm.Issues, i => i.Message == text);
    }

    [Theory]
    [InlineData(UiLanguage.French, "La clé étrangère 'fk' référence la table manquante 'ghosts'.")]
    [InlineData(UiLanguage.English, "The foreign key 'fk' references the missing table 'ghosts'.")]
    public void MissingTargetIssue_IsInTheChosenLanguage(UiLanguage language, string text)
    {
        using var _ = UiText.Override(language);
        var f = new Fixture();
        f.Vm.AddForeignKeyCommand.Execute(null);
        var fk = f.Vm.ForeignKeys[0];
        fk.Name = "fk";
        fk.ColumnsText = "id";
        fk.ReferencedColumnsText = "id";
        fk.ReferencedTable = "ghosts";

        Assert.Contains(f.Vm.Issues, i => i.Message == text);
    }

    [Fact]
    public async Task Apply_Warnings_RequireConfirmation()
    {
        var view = new TableInfo("main", "v", TableKind.View, [], [], [], null);
        var f = new Fixture(DatabaseDialect.Sqlite, SqliteTable(), view);
        f.Vm.Columns[1].Name = "title";
        Assert.Contains(f.Vm.Issues, i => i.Severity == DesignIssueSeverity.Warning && i.Code == CoreMessageCodes.DesignViewsNotRecreated);

        f.Dialogs.ConfirmResult = false;
        await f.Vm.ApplyCommand.ExecuteAsync(null);
        Assert.Empty(f.Connection.Executed);

        f.Dialogs.ConfirmResult = true;
        await f.Vm.ApplyCommand.ExecuteAsync(null);
        Assert.NotEmpty(f.Connection.Executed);
    }

    [Fact]
    public async Task Apply_Success_RefreshesSchemaAndResetsChanges()
    {
        var f = new Fixture();
        f.Connection.Next = new SchemaSnapshot("public", [new TableInfo(
            "public", "new_table", TableKind.Table, [new ColumnDefinition("id", "integer", false, true, null, true)], [], [], "new_table_pkey")]);

        await f.Vm.ApplyCommand.ExecuteAsync(null);

        Assert.Equal("1 instruction appliquée", f.Vm.StatusText);
        Assert.NotNull(f.Vm.Original);
        Assert.Equal("Concevoir new_table", f.Vm.Title);
        Assert.False(f.Vm.HasChanges);
        Assert.False(f.Vm.ApplyCommand.CanExecute(null));
        Assert.Same(f.Connection.Next, f.Session.Schema);
    }

    [Fact]
    public void Revert_RestoresOriginal()
    {
        var f = new Fixture(DatabaseDialect.PostgreSql, People);
        Assert.Equal("Concevoir people", f.Vm.Title);
        Assert.False(f.Vm.HasChanges);

        f.Vm.Name = "humans";
        f.Vm.Columns[1].Name = "label";
        f.Vm.RemoveColumnCommand.Execute(f.Vm.Columns[0]);
        Assert.True(f.Vm.HasChanges);
        f.Vm.RevertCommand.Execute(null);

        Assert.Equal("people", f.Vm.Name);
        Assert.Equal(["id", "name"], f.Vm.Columns.Select(c => c.Name));
        Assert.False(f.Vm.HasChanges);
    }

    [Fact]
    public void OpenScript_RaisesScriptRequested()
    {
        var f = new Fixture();
        string? received = null;
        f.Vm.ScriptRequested += s => received = s;

        f.Vm.OpenScriptCommand.Execute(null);

        Assert.Equal(f.Vm.Script, received);
        Assert.NotEmpty(received!);
    }

    [Fact]
    public async Task Sqlite_EndToEnd_RebuildKeepsRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"squill-designer-{Guid.NewGuid():N}.db");
        var profile = new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.Sqlite, null, null, null, null, path, null);
        var connection = await new SqliteDatabaseProvider().OpenAsync(profile, null, CancellationToken.None);
        try
        {
            var session = new ConnectionSession(profile, connection, await connection.ReadSchemaAsync(CancellationToken.None), null);
            var dialogs = new FakeDialogService();
            var create = new TableDesignerViewModel(session, null, dialogs, new ImmediateDispatcher()) { Name = "people" };
            create.AddColumnCommand.Execute(null);
            create.Columns[1].Name = "name";
            create.Columns[1].DataType = "TEXT";
            await create.ApplyCommand.ExecuteAsync(null);
            Assert.Empty(dialogs.Errors);
            Assert.NotNull(session.Schema.FindTable(null, "people"));

            await Drain(connection, "INSERT INTO people (name) VALUES ('ada'), ('grace')");

            var edit = new TableDesignerViewModel(session, session.Schema.FindTable(null, "people"), dialogs, new ImmediateDispatcher());
            edit.Columns[1].Name = "full_name";
            edit.Columns[1].DataType = "VARCHAR(50)";
            await edit.ApplyCommand.ExecuteAsync(null);

            Assert.Empty(dialogs.Errors);
            var table = session.Schema.FindTable(null, "people")!;
            Assert.Equal(["id", "full_name"], table.Columns.Select(c => c.Name));
            var reader = await connection.ExecuteAsync("SELECT full_name FROM people ORDER BY id", [], CancellationToken.None);
            await using (reader)
            {
                var rows = await reader.ReadBatchAsync(10, CancellationToken.None);
                Assert.Equal(["ada", "grace"], rows!.Select(r => (string)r[0]!));
            }
        }
        finally
        {
            await connection.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static async Task Drain(IDatabaseConnection connection, string sql)
    {
        await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
    }
}
