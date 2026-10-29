using Squill.Core;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class PostgresSchemaReaderTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    private const string Setup = """
        drop schema if exists app, ref cascade;
        create schema ref;
        create table ref.country (code char(2) primary key, name text not null);
        create schema app;
        create table app.person (
            id int generated always as identity,
            tenant int not null,
            country char(2) not null references ref.country (code),
            nick varchar(20),
            created timestamptz not null default now(),
            primary key (id, tenant));
        create index person_tenant_nick on app.person (tenant, nick);
        create unique index person_nick_lower on app.person (lower(nick), nick);
        create view app.person_nick as select id, nick from app.person;
        create materialized view app.person_count as select count(*) as n from app.person;
        """;

    private async Task<SchemaSnapshot> ReadAsync()
    {
        await using var connection = await new PostgresDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "pg", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
            PostgresFixture.Password,
            CancellationToken.None);
        await using (var reader = await connection.ExecuteAsync(Setup, [], CancellationToken.None))
            Assert.Null(await reader.ReadBatchAsync(1, CancellationToken.None));
        return await connection.ReadSchemaAsync(CancellationToken.None);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_Schemas_ExcludesSystemAndSetsDefault()
    {
        var snapshot = await ReadAsync();
        Assert.Equal("public", snapshot.DefaultSchema);
        Assert.Equal(["app.person", "app.person_count", "app.person_nick", "ref.country"], snapshot.Tables.Select(t => $"{t.Schema}.{t.Name}").Order());
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_Columns_HaveTypesNullabilityDefaultsAndKeys()
    {
        var person = (await ReadAsync()).FindTable("app", "person")!;
        Assert.Equal(TableKind.Table, person.Kind);
        Assert.Equal(["id", "tenant", "country", "nick", "created"], person.Columns.Select(c => c.Name));
        Assert.Equal(["integer", "integer", "character(2)", "character varying(20)", "timestamp with time zone"], person.Columns.Select(c => c.DataType));
        Assert.Equal([false, false, false, true, false], person.Columns.Select(c => c.IsNullable));
        Assert.Equal([true, true, false, false, false], person.Columns.Select(c => c.IsPrimaryKey));
        Assert.Equal("now()", person.Columns[4].DefaultExpression);
        Assert.Null(person.Columns[3].DefaultExpression);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_Indexes_ExcludePrimaryKeyAndSkipExpressions()
    {
        var person = (await ReadAsync()).FindTable("app", "person")!;
        Assert.Equal(["person_nick_lower", "person_tenant_nick"], person.Indexes.Select(i => i.Name));
        Assert.Equal(["nick"], person.Indexes[0].Columns);
        Assert.True(person.Indexes[0].IsUnique);
        Assert.Equal(["tenant", "nick"], person.Indexes[1].Columns);
        Assert.False(person.Indexes[1].IsUnique);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_ForeignKeyAcrossSchemas_IsResolved()
    {
        var foreignKey = Assert.Single((await ReadAsync()).FindTable("app", "person")!.ForeignKeys);
        Assert.Equal(["country"], foreignKey.Columns);
        Assert.Equal("ref", foreignKey.ReferencedSchema);
        Assert.Equal("country", foreignKey.ReferencedTable);
        Assert.Equal(["code"], foreignKey.ReferencedColumns);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_ViewsAndMaterializedViews_AreViews()
    {
        var snapshot = await ReadAsync();
        Assert.Equal(TableKind.View, snapshot.FindTable("app", "person_nick")!.Kind);
        Assert.Equal(TableKind.View, snapshot.FindTable("app", "person_count")!.Kind);
        Assert.Equal(["id", "nick"], snapshot.FindTable("app", "person_nick")!.Columns.Select(c => c.Name));
    }
}
