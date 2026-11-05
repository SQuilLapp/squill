using Microsoft.Data.SqlClient;
using Squill.Core;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class SampleSchemaTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    private async Task<IDatabaseConnection> OpenAsync() => await new SqlServerDatabaseProvider().OpenAsync(
        new ConnectionProfile(Guid.NewGuid(), "ms", DatabaseDialect.SqlServer, server.Host, server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
        SqlServerFixture.Password,
        CancellationToken.None);

    // GO is a client convention, not T-SQL: split the script on the lines that hold only GO.
    private static async Task LoadAsync(IDatabaseConnection connection)
    {
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "sqlserver.sql"));
        foreach (var batch in script.Split('\n').Aggregate(new List<string> { "" }, (list, line) =>
        {
            if (line.Trim() == "GO")
                list.Add("");
            else
                list[^1] += line + "\n";
            return list;
        }))
        {
            await using var reader = await connection.ExecuteAsync(batch, [], CancellationToken.None);
            Assert.Null(await reader.ReadBatchAsync(1, CancellationToken.None));
        }
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task SampleSchema_LoadsAndIsReadBack()
    {
        await using var connection = await OpenAsync();
        await LoadAsync(connection);
        await LoadAsync(connection); // idempotent

        var snapshot = await connection.ReadSchemaAsync(CancellationToken.None);
        const string schema = "sample";
        Assert.Equal(
            ["all_types", "customers", "fk_actions", "fk_targets", "fk_targets_b", "fk_targets_c", "fk_targets_d", "no_pk", "order_items", "order_totals", "orders"],
            snapshot.Tables.Where(t => t.Schema == schema).Select(t => t.Name).Order());
        Assert.Equal(TableKind.View, snapshot.FindTable(schema, "order_totals")!.Kind);
        Assert.Equal(["add_numbers", "customer_orders", "touch_customers"], snapshot.Routines.Where(r => r.Schema == schema).Select(r => r.Name).Order());
        Assert.Single(snapshot.Triggers, t => t.Schema == schema && t.Name == "orders_stamp");

        Assert.Equal(["order_id", "line_no"], snapshot.FindTable(schema, "order_items")!.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name));
        Assert.DoesNotContain(snapshot.FindTable(schema, "no_pk")!.Columns, c => c.IsPrimaryKey);
        Assert.Contains(snapshot.FindTable(schema, "orders")!.Indexes, i => i.Name == "orders_customer_placed" && i.Columns.SequenceEqual(["customer_id", "placed_on"]));

        var actions = snapshot.FindTable(schema, "fk_actions")!.ForeignKeys.OrderBy(f => f.Columns[0]).Select(f => f.OnDelete).ToList();
        Assert.Equal([ReferentialAction.Cascade, ReferentialAction.SetNull, ReferentialAction.NoAction, ReferentialAction.SetDefault], actions);

        var types = snapshot.FindTable(schema, "all_types")!.Columns;
        Assert.Equal(22, types.Count);
        Assert.Contains(types, c => c.Name == "c_identity" && c.IsAutoIncrement);
        Assert.Equal("decimal(18,4)", types.Single(c => c.Name == "c_decimal").DataType);
    }

    // Finding for P11-33: the OUTPUT INSERTED read-back the DML generator emits is refused by SQL Server (error 334) on a table with a trigger.
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task OutputInserted_OnTableWithTrigger_IsRefusedByServer()
    {
        await using var connection = await OpenAsync();
        foreach (var sql in new[]
        {
            "drop table if exists dbo.trg_items",
            "create table dbo.trg_items (id int identity(1, 1) primary key, name nvarchar(50) not null)",
            "create trigger dbo.trg_items_ins on dbo.trg_items after insert as begin set nocount on; end",
        })
        {
            await using var setup = await connection.ExecuteAsync(sql, [], CancellationToken.None);
            Assert.Null(await setup.ReadBatchAsync(1, CancellationToken.None));
        }

        var error = await Assert.ThrowsAsync<SqlException>(async () =>
        {
            await using var reader = await connection.ExecuteAsync("INSERT INTO dbo.trg_items (name) OUTPUT INSERTED.id VALUES (@p0)", [new QueryParameter("p0", "a")], CancellationToken.None);
            await reader.ReadAllAsync(10, CancellationToken.None);
        });
        Assert.Equal(334, error.Number);
    }
}
