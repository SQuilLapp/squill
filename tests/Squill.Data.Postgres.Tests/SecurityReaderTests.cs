using System.Diagnostics;
using Squill.Core;
using Squill.Core.Security;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class SecurityReaderTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    private const string Setup = """
        create role grp_read nologin;
        create role analyst nologin in role grp_read;
        create role reader login;
        create schema sec_a;
        create table sec_a.t1(id int);
        create table sec_a.t2(id int);
        create table sec_a.t3(id int);
        create view sec_a.v1 as select id from sec_a.t1;
        create materialized view sec_a.m1 as select id from sec_a.t1;
        grant usage on schema sec_a to grp_read;
        grant select on all tables in schema sec_a to grp_read;
        grant insert on sec_a.t1 to analyst;
        grant select on sec_a.t3 to public;
        create schema sec_big;
        do $$ begin for i in 1..2000 loop execute format('create table sec_big.t%s(id int)', i); end loop; end $$;
        """;

    private async Task<(IDatabaseConnection Connection, ISecurityReader Reader)> OpenAsync()
    {
        var connection = await new PostgresDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "security", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
            PostgresFixture.Password,
            CancellationToken.None);
        bool ready;
        await using (var reader = await connection.ExecuteAsync("select 1 from pg_roles where rolname = 'analyst'", [], CancellationToken.None))
            ready = (await reader.ReadAllAsync(10, CancellationToken.None)).Count > 0;
        if (!ready)
        {
            await using var setup = await connection.ExecuteAsync(Setup, [], CancellationToken.None);
            await setup.ReadAllAsync(10, CancellationToken.None);
        }

        return (connection, (ISecurityReader)connection);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadRolesAsync_Roles_SplitAndMembershipsBothWays()
    {
        var (connection, reader) = await OpenAsync();
        await using var _ = connection;
        var roles = (await reader.ReadRolesAsync(CancellationToken.None)).ToDictionary(r => r.Name);

        Assert.DoesNotContain(roles.Keys, n => n.StartsWith("pg_", StringComparison.Ordinal));
        Assert.False(roles["reader"].IsGroup);
        Assert.True(roles["grp_read"].IsGroup);
        Assert.Equal(["grp_read"], roles["analyst"].MemberOf);
        Assert.Equal(["analyst"], roles["grp_read"].Members);
        Assert.True(roles["postgres"].IsSuperuser);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ListSchemasAsync_SystemSchemasExcluded()
    {
        var (connection, reader) = await OpenAsync();
        await using var _ = connection;
        var schemas = await reader.ListSchemasAsync(CancellationToken.None);

        Assert.Contains("sec_a", schemas);
        Assert.DoesNotContain("pg_catalog", schemas);
        Assert.DoesNotContain("information_schema", schemas);
        Assert.DoesNotContain("pg_toast", schemas);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadPrivilegesAsync_Analyst_DirectAndInherited()
    {
        var (connection, reader) = await OpenAsync();
        await using var _ = connection;
        var objects = (await reader.ReadPrivilegesAsync("analyst", ["sec_a"], CancellationToken.None)).ToDictionary(o => o.Name);

        Assert.Equal(["m1", "t1", "t2", "t3", "v1"], objects.Keys.Order());
        Assert.Equal(ObjectKind.View, objects["v1"].Kind);
        Assert.Equal(ObjectKind.MaterializedView, objects["m1"].Kind);
        Assert.Equal(PrivilegeState.Inherited("grp_read"), objects["t1"].StateOf(PrivilegeKind.Select));
        Assert.Equal(PrivilegeState.Granted, objects["t1"].StateOf(PrivilegeKind.Insert));
        Assert.Equal(PrivilegeState.None, objects["t1"].StateOf(PrivilegeKind.Delete));
        Assert.Equal(PrivilegeState.None, objects["t2"].StateOf(PrivilegeKind.Insert));
        Assert.Equal(PrivilegeState.Inherited("grp_read"), objects["v1"].StateOf(PrivilegeKind.Select));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadPrivilegesAsync_Owner_HasEverythingGranted()
    {
        var (connection, reader) = await OpenAsync();
        await using var _ = connection;
        var t2 = (await reader.ReadPrivilegesAsync("postgres", ["sec_a"], CancellationToken.None)).Single(o => o.Name == "t2");

        Assert.All(Enum.GetValues<PrivilegeKind>(), k => Assert.Equal(PrivilegeState.Granted, t2.StateOf(k)));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadPrivilegesAsync_PublicGrant_InheritedFromPublic()
    {
        var (connection, reader) = await OpenAsync();
        await using var _ = connection;
        var objects = (await reader.ReadPrivilegesAsync("reader", ["sec_a"], CancellationToken.None)).ToDictionary(o => o.Name);

        Assert.Equal(PrivilegeState.Inherited("PUBLIC"), objects["t3"].StateOf(PrivilegeKind.Select));
        Assert.Equal(PrivilegeState.None, objects["t1"].StateOf(PrivilegeKind.Select));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadPrivilegesAsync_SchemaFilterAndBigSchema_OnlyRequestedAndFast()
    {
        var (connection, reader) = await OpenAsync();
        await using var _ = connection;
        Assert.All(await reader.ReadPrivilegesAsync("analyst", ["sec_a"], CancellationToken.None), o => Assert.Equal("sec_a", o.Schema));

        var watch = Stopwatch.StartNew();
        var big = await reader.ReadPrivilegesAsync("analyst", ["sec_big"], CancellationToken.None);
        watch.Stop();

        Assert.Equal(2000, big.Count);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"Took {watch.Elapsed}.");
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task BuiltScript_Applied_PrivilegesMatchTheChanges()
    {
        var (connection, reader) = await OpenAsync();
        await using var _ = connection;
        var before = (await reader.ReadPrivilegesAsync("reader", ["sec_a"], CancellationToken.None)).ToDictionary(o => o.Name);
        var set = new PrivilegeChangeSet("reader");
        set.Toggle(before["t2"].Key, PrivilegeKind.Update, before["t2"].StateOf(PrivilegeKind.Update));
        set.Toggle(before["t2"].Key, PrivilegeKind.Select, before["t2"].StateOf(PrivilegeKind.Select));

        var script = Squill.Sql.Security.PostgresPrivilegeScript.Build("reader", set, "test");
        foreach (var statement in script.Statements)
        {
            await using var run = await connection.ExecuteAsync(statement, [], CancellationToken.None);
            await run.ReadAllAsync(10, CancellationToken.None);
        }

        var t2 = (await reader.ReadPrivilegesAsync("reader", ["sec_a"], CancellationToken.None)).Single(o => o.Name == "t2");
        Assert.Equal(PrivilegeState.Granted, t2.StateOf(PrivilegeKind.Update));
        Assert.Equal(PrivilegeState.Granted, t2.StateOf(PrivilegeKind.Select));

        await using var undo = await connection.ExecuteAsync("revoke select, update on sec_a.t2 from reader", [], CancellationToken.None);
        await undo.ReadAllAsync(10, CancellationToken.None);
    }
}
