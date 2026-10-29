using Squill.Core;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class SqlServerSchemaReaderTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    private static readonly string[] Setup =
    [
        "drop view if exists dbo.person_nick",
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
        "create unique index person_tenant_nick on dbo.person (tenant, nick)",
        "create index person_country on dbo.person (country)",
        "create view dbo.person_nick as select id, nick from dbo.person",
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
        Assert.Equal(["country", "person", "person_nick"], snapshot.Tables.Select(t => t.Name).Order());
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
}
