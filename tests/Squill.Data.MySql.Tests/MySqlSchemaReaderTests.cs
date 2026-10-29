using Squill.Core;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class MySqlSchemaReaderTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    private static readonly string[] Setup =
    [
        "drop view if exists person_nick",
        "drop table if exists person",
        "drop table if exists country",
        "create table country (code char(2) primary key, name varchar(50) not null)",
        """
        create table person (
            id int not null auto_increment,
            tenant int not null,
            country char(2) not null,
            nick varchar(20),
            created datetime not null default current_timestamp,
            primary key (id, tenant),
            unique key person_tenant_nick (tenant, nick),
            key person_country (country),
            constraint fk_person_country foreign key (country) references country (code))
        """,
        "create view person_nick as select id, nick from person",
    ];

    private async Task<SchemaSnapshot> ReadAsync()
    {
        await using var connection = await new MySqlDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "my", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null),
            MySqlFixture.Password,
            CancellationToken.None);
        foreach (var sql in Setup)
        {
            await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
            Assert.Null(await reader.ReadBatchAsync(1, CancellationToken.None));
        }
        return await connection.ReadSchemaAsync(CancellationToken.None);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_Database_IsDefaultSchemaOfEveryTable()
    {
        var snapshot = await ReadAsync();
        Assert.Equal(MySqlFixture.Database, snapshot.DefaultSchema);
        Assert.Equal(["country", "person", "person_nick"], snapshot.Tables.Select(t => t.Name).Order());
        Assert.All(snapshot.Tables, t => Assert.Equal(MySqlFixture.Database, t.Schema));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_Columns_HaveTypesNullabilityDefaultsAndKeys()
    {
        var person = (await ReadAsync()).FindTable(MySqlFixture.Database, "person")!;
        Assert.Equal(TableKind.Table, person.Kind);
        Assert.Equal(["id", "tenant", "country", "nick", "created"], person.Columns.Select(c => c.Name));
        Assert.Equal(["int", "int", "char(2)", "varchar(20)", "datetime"], person.Columns.Select(c => c.DataType));
        Assert.Equal([false, false, false, true, false], person.Columns.Select(c => c.IsNullable));
        Assert.Equal([true, true, false, false, false], person.Columns.Select(c => c.IsPrimaryKey));
        Assert.NotNull(person.Columns[4].DefaultExpression);
        Assert.Null(person.Columns[3].DefaultExpression);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_Indexes_ExcludePrimaryKeyAndKeepColumnOrder()
    {
        var person = (await ReadAsync()).FindTable(MySqlFixture.Database, "person")!;
        Assert.Contains(person.Indexes, i => i.Name == "person_tenant_nick" && i.IsUnique && i.Columns.SequenceEqual(["tenant", "nick"]));
        Assert.Contains(person.Indexes, i => i.Name == "person_country" && !i.IsUnique && i.Columns.SequenceEqual(["country"]));
        Assert.DoesNotContain(person.Indexes, i => i.Name == "PRIMARY");
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_ForeignKey_IsResolved()
    {
        var foreignKey = Assert.Single((await ReadAsync()).FindTable(MySqlFixture.Database, "person")!.ForeignKeys);
        Assert.Equal("fk_person_country", foreignKey.Name);
        Assert.Equal(["country"], foreignKey.Columns);
        Assert.Equal(MySqlFixture.Database, foreignKey.ReferencedSchema);
        Assert.Equal("country", foreignKey.ReferencedTable);
        Assert.Equal(["code"], foreignKey.ReferencedColumns);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_View_IsViewWithColumns()
    {
        var view = (await ReadAsync()).FindTable(MySqlFixture.Database, "person_nick")!;
        Assert.Equal(TableKind.View, view.Kind);
        Assert.Equal(["id", "nick"], view.Columns.Select(c => c.Name));
    }
}
