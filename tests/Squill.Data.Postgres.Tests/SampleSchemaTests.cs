using Squill.Core;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class SampleSchemaTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    private async Task<IDatabaseConnection> OpenAsync() => await new PostgresDatabaseProvider().OpenAsync(
        new ConnectionProfile(Guid.NewGuid(), "pg", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
        PostgresFixture.Password,
        CancellationToken.None);

    private static async Task LoadAsync(IDatabaseConnection connection)
    {
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "postgres.sql"));
        await using var reader = await connection.ExecuteAsync(script, [], CancellationToken.None);
        Assert.Null(await reader.ReadBatchAsync(1, CancellationToken.None));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task SampleSchema_LoadsAndIsReadBack()
    {
        await using var connection = await OpenAsync();
        await LoadAsync(connection);
        await LoadAsync(connection); // idempotent

        var snapshot = await connection.ReadSchemaAsync(CancellationToken.None);
        var sample = snapshot.Tables.Where(t => t.Schema == "sample").ToList();
        Assert.Equal(
            ["all_types", "customer_counts", "customers", "fk_actions", "fk_targets", "no_pk", "order_items", "order_totals", "orders"],
            sample.Select(t => t.Name).Order());
        Assert.Equal(TableKind.View, snapshot.FindTable("sample", "order_totals")!.Kind);
        Assert.Equal(TableKind.MaterializedView, snapshot.FindTable("sample", "customer_counts")!.Kind);
        Assert.Equal(3, snapshot.Routines.Count(r => r.Schema == "sample" && r.Name is "add_numbers" or "touch_customers"));
        Assert.Equal("orders_stamp", Assert.Single(snapshot.Triggers, t => t.Schema == "sample").Name);

        Assert.Equal(["order_id", "line_no"], snapshot.FindTable("sample", "order_items")!.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name));
        Assert.DoesNotContain(snapshot.FindTable("sample", "no_pk")!.Columns, c => c.IsPrimaryKey);
        Assert.Contains(snapshot.FindTable("sample", "orders")!.Indexes, i => i.Name == "orders_customer_placed" && i.Columns.SequenceEqual(["customer_id", "placed_on"]));

        var actions = snapshot.FindTable("sample", "fk_actions")!.ForeignKeys.OrderBy(f => f.Columns[0]).Select(f => f.OnDelete).ToList();
        Assert.Equal([ReferentialAction.Cascade, ReferentialAction.SetNull, ReferentialAction.Restrict, ReferentialAction.NoAction, ReferentialAction.SetDefault], actions);

        var types = snapshot.FindTable("sample", "all_types")!.Columns;
        Assert.Equal(25, types.Count);
        Assert.Contains(types, c => c.Name == "c_serial" && c.IsAutoIncrement);
        Assert.Contains(types, c => c.Name == "c_identity" && c.IsAutoIncrement);
        Assert.Equal("integer[]", types.Single(c => c.Name == "c_int_array").DataType);
        Assert.Equal("numeric(18,4)", types.Single(c => c.Name == "c_numeric").DataType);
    }
}
