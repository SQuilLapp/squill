using Squill.Core;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class SqlServerSchemaReaderTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    private static readonly string[] Setup =
    [
        "drop view if exists dbo.person_nick",
        "drop function if exists dbo.add_two",
        "drop function if exists dbo.person_ids",
        "drop procedure if exists dbo.bump",
        "drop table if exists dbo.actions",
        "drop table if exists dbo.ident",
        "drop table if exists dbo.named_pk",
        "drop table if exists dbo.country3",
        "drop table if exists dbo.country2",
        "drop table if exists dbo.person",
        "drop table if exists dbo.country",
        "create table dbo.country (code char(2) primary key, name nvarchar(50) not null)",
        """
        create table dbo.person (
            id int not null,
            tenant int not null,
            country char(2) not null,
            nick nvarchar(20) null,
            created datetime2(3) not null constraint df_person_created default sysutcdatetime(),
            constraint pk_person primary key (id, tenant),
            constraint fk_person_country foreign key (country) references dbo.country (code))
        """,
        "create table dbo.country2 (id int primary key)",
        "create table dbo.country3 (id int primary key)",
        "create table dbo.ident (id int identity(1,1) primary key, plain int null)",
        "create table dbo.named_pk (id int not null, constraint pk_named primary key (id))",
        """
        create table dbo.actions (
            id int primary key,
            a int references dbo.country2 (id) on delete cascade on update set null,
            b int default 1 references dbo.country3 (id) on delete set default on update cascade,
            c int references dbo.country2 (id))
        """,
        "create unique index person_tenant_nick on dbo.person (tenant, nick)",
        "create index person_country on dbo.person (country)",
        "create view dbo.person_nick as select id, nick from dbo.person",
        "create function dbo.add_two(@a int, @b nvarchar(10)) returns int as begin return @a + 1; end",
        "create function dbo.person_ids(@tenant int) returns table as return select id from dbo.person where tenant = @tenant",
        "create procedure dbo.bump @n int, @total int output as set @total = @n + 1",
        "create trigger dbo.person_stamp on dbo.person instead of insert, update as begin set nocount on; end",
    ];

    [Theory]
    [InlineData("int", 4, 10, 0, "int")]
    [InlineData("varchar", 20, 0, 0, "varchar(20)")]
    [InlineData("varchar", -1, 0, 0, "varchar(max)")]
    [InlineData("nvarchar", 40, 0, 0, "nvarchar(20)")]
    [InlineData("nvarchar", -1, 0, 0, "nvarchar(max)")]
    [InlineData("varbinary", 16, 0, 0, "varbinary(16)")]
    [InlineData("decimal", 9, 10, 2, "decimal(10,2)")]
    [InlineData("datetime2", 8, 27, 3, "datetime2(3)")]
    public void FormatDataType_Type_ReturnsDeclaration(string type, int maxLength, int precision, int scale, string expected) =>
        Assert.Equal(expected, SqlServerSchemaReader.FormatDataType(type, maxLength, precision, scale));

    private async Task<SchemaSnapshot> ReadAsync()
    {
        await using var connection = await new SqlServerDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "ms", DatabaseDialect.SqlServer, server.Host, server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
            SqlServerFixture.Password,
            CancellationToken.None);
        foreach (var sql in Setup)
        {
            await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
            Assert.Null(await reader.ReadBatchAsync(1, CancellationToken.None));
        }
        return await connection.ReadSchemaAsync(CancellationToken.None);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_Database_ExcludesSystemSchemas()
    {
        var snapshot = await ReadAsync();
        Assert.Equal("dbo", snapshot.DefaultSchema);
        Assert.Equal(["actions", "country", "country2", "country3", "ident", "named_pk", "person", "person_nick"], snapshot.Tables.Select(t => t.Name).Order());
        Assert.All(snapshot.Tables, t => Assert.Equal("dbo", t.Schema));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_Columns_HaveTypesNullabilityDefaultsAndKeys()
    {
        var person = (await ReadAsync()).FindTable("dbo", "person")!;
        Assert.Equal(TableKind.Table, person.Kind);
        Assert.Equal(["id", "tenant", "country", "nick", "created"], person.Columns.Select(c => c.Name));
        Assert.Equal(["int", "int", "char(2)", "nvarchar(20)", "datetime2(3)"], person.Columns.Select(c => c.DataType));
        Assert.Equal([false, false, false, true, false], person.Columns.Select(c => c.IsNullable));
        Assert.Equal([true, true, false, false, false], person.Columns.Select(c => c.IsPrimaryKey));
        Assert.NotNull(person.Columns[4].DefaultExpression);
        Assert.Null(person.Columns[3].DefaultExpression);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_Indexes_ExcludePrimaryKeyAndKeepColumnOrder()
    {
        var person = (await ReadAsync()).FindTable("dbo", "person")!;
        Assert.Contains(person.Indexes, i => i.Name == "person_tenant_nick" && i.IsUnique && i.Columns.SequenceEqual(["tenant", "nick"]));
        Assert.Contains(person.Indexes, i => i.Name == "person_country" && !i.IsUnique && i.Columns.SequenceEqual(["country"]));
        Assert.DoesNotContain(person.Indexes, i => i.Name == "pk_person");
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_ForeignKey_IsResolved()
    {
        var foreignKey = Assert.Single((await ReadAsync()).FindTable("dbo", "person")!.ForeignKeys);
        Assert.Equal("fk_person_country", foreignKey.Name);
        Assert.Equal(["country"], foreignKey.Columns);
        Assert.Equal("dbo", foreignKey.ReferencedSchema);
        Assert.Equal("country", foreignKey.ReferencedTable);
        Assert.Equal(["code"], foreignKey.ReferencedColumns);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_View_IsViewWithColumns()
    {
        var view = (await ReadAsync()).FindTable("dbo", "person_nick")!;
        Assert.Equal(TableKind.View, view.Kind);
        Assert.Equal(["id", "nick"], view.Columns.Select(c => c.Name));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_IdentityColumn_IsAutoIncrement()
    {
        var ident = (await ReadAsync()).FindTable("dbo", "ident")!;
        Assert.True(ident.Columns[0].IsAutoIncrement);
        Assert.False(ident.Columns[1].IsAutoIncrement);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_ForeignKeyActions_AreRead()
    {
        var keys = (await ReadAsync()).FindTable("dbo", "actions")!.ForeignKeys.ToDictionary(k => k.Columns[0]);
        Assert.Equal((ReferentialAction.Cascade, ReferentialAction.SetNull), (keys["a"].OnDelete, keys["a"].OnUpdate));
        Assert.Equal((ReferentialAction.SetDefault, ReferentialAction.Cascade), (keys["b"].OnDelete, keys["b"].OnUpdate));
        Assert.Equal((ReferentialAction.NoAction, ReferentialAction.NoAction), (keys["c"].OnDelete, keys["c"].OnUpdate));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_PrimaryKeyName_IsNamedAutoOrNull()
    {
        var snapshot = await ReadAsync();
        Assert.Equal("pk_named", snapshot.FindTable("dbo", "named_pk")!.PrimaryKeyName);
        Assert.StartsWith("PK__", snapshot.FindTable("dbo", "ident")!.PrimaryKeyName);
        Assert.Null(snapshot.FindTable("dbo", "person_nick")!.PrimaryKeyName);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_ViewDefinition_IsFilledOnViewsOnly()
    {
        var snapshot = await ReadAsync();
        Assert.Contains("dbo.person", snapshot.FindTable("dbo", "person_nick")!.Definition);
        Assert.Null(snapshot.FindTable("dbo", "person")!.Definition);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_Routines_HaveSignaturesReturnTypesAndDefinitions()
    {
        var routines = (await ReadAsync()).Routines.Where(r => r.Schema == "dbo").ToList();
        Assert.Equal(["add_two", "bump", "person_ids"], routines.Select(r => r.Name));
        var scalar = routines.Single(r => r.Name == "add_two");
        Assert.Equal((RoutineKind.Function, "add_two(@a int, @b nvarchar(10))", "int"), (scalar.Kind, scalar.Signature, scalar.ReturnType));
        Assert.Contains("create function dbo.add_two", scalar.Definition, StringComparison.OrdinalIgnoreCase);
        var table = routines.Single(r => r.Name == "person_ids");
        Assert.Equal((RoutineKind.Function, "person_ids(@tenant int)", "TABLE"), (table.Kind, table.Signature, table.ReturnType));
        var procedure = routines.Single(r => r.Name == "bump");
        Assert.Equal((RoutineKind.Procedure, "bump(@n int, @total int)"), (procedure.Kind, procedure.Signature));
        Assert.Null(procedure.ReturnType);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_Triggers_HaveTimingEventsAndDefinition()
    {
        var trigger = Assert.Single((await ReadAsync()).Triggers, t => t.Schema == "dbo");
        Assert.Equal(("person", "person_stamp", "INSTEAD OF", "INSERT OR UPDATE"), (trigger.Table, trigger.Name, trigger.Timing, trigger.Events));
        Assert.Contains("instead of insert", trigger.Definition, StringComparison.OrdinalIgnoreCase);
    }
}
