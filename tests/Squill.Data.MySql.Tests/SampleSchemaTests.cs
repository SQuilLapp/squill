using Squill.Core;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class SampleSchemaTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    private async Task<IDatabaseConnection> OpenAsync() => await new MySqlDatabaseProvider().OpenAsync(
        new ConnectionProfile(Guid.NewGuid(), "my", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null),
        MySqlFixture.Password,
        CancellationToken.None);

    private static async Task LoadAsync(IDatabaseConnection connection)
    {
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "mysql.sql"));
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
        var schema = snapshot.DefaultSchema;
        Assert.Equal(
            ["all_types", "customers", "fk_actions", "fk_targets", "no_pk", "order_items", "order_totals", "orders"],
            snapshot.Tables.Where(t => t.Schema == schema).Select(t => t.Name).Order());
        Assert.Equal(TableKind.View, snapshot.FindTable(schema, "order_totals")!.Kind);

        Assert.Equal(["order_id", "line_no"], snapshot.FindTable(schema, "order_items")!.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name));
        Assert.DoesNotContain(snapshot.FindTable(schema, "no_pk")!.Columns, c => c.IsPrimaryKey);
        Assert.Contains(snapshot.FindTable(schema, "orders")!.Indexes, i => i.Name == "orders_customer_placed" && i.Columns.SequenceEqual(["customer_id", "placed_on"]));

        var actions = snapshot.FindTable(schema, "fk_actions")!.ForeignKeys.OrderBy(f => f.Columns[0]).Select(f => f.OnDelete).ToList();
        Assert.Equal([ReferentialAction.Cascade, ReferentialAction.SetNull, ReferentialAction.Restrict, ReferentialAction.NoAction], actions);

        var types = snapshot.FindTable(schema, "all_types")!.Columns;
        Assert.Equal(22, types.Count);
        Assert.Contains(types, c => c.Name == "c_auto" && c.IsAutoIncrement);
        Assert.Equal("decimal(18,4)", types.Single(c => c.Name == "c_decimal").DataType);
    }
}
