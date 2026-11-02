using Squill.Core;
using Squill.Core.Design;
using Squill.Sql.Ddl;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class PostgresAlterTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Alter_EditedDesign_ProducesTheEditedTable()
    {
        await using var connection = await new PostgresDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "pg", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
            PostgresFixture.Password,
            CancellationToken.None);

        async Task RunAsync(IEnumerable<string> statements)
        {
            foreach (var sql in statements)
            {
                await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
                Assert.Null(await reader.ReadBatchAsync(1, CancellationToken.None));
            }
        }

        var generator = DdlGenerators.For(DatabaseDialect.PostgreSql);
        await RunAsync(["drop table if exists public.alter_child, public.alter_parent cascade"]);
        await RunAsync(generator.CreateTable(new TableDesign(
            "public", "alter_parent", [new(null, "id", "integer", false, null, false)], ["id"], null, [], [])));
        await RunAsync(generator.CreateTable(new TableDesign(
            "public", "alter_child",
            [new(null, "id", "integer", false, null, false), new(null, "qty", "smallint", true, null, false), new(null, "parent", "integer", true, null, false)],
            ["id"], null, [], [])));

        var original = TableDesign.FromTable((await connection.ReadSchemaAsync(CancellationToken.None)).FindTable("public", "alter_child")!);
        var edited = original with
        {
            Columns =
            [
                original.Columns[0],
                original.Columns[1] with { Name = "amount", DataType = "bigint" },
                original.Columns[2],
                new ColumnDesign(null, "note", "text", true, null, false),
            ],
            Indexes = [new IndexDesign(null, "alter_child_amount", false, ["amount"])],
            ForeignKeys = [new ForeignKeyDesign(null, "fk_alter_parent", ["parent"], "public", "alter_parent", ["id"], ReferentialAction.Cascade, ReferentialAction.NoAction)],
        };

        await RunAsync(generator.Alter(original, edited));

        var actual = TableDesign.FromTable((await connection.ReadSchemaAsync(CancellationToken.None)).FindTable("public", "alter_child")!);
        Assert.Equal(["id", "amount", "parent", "note"], actual.Columns.Select(c => c.Name));
        Assert.Equal(["integer", "bigint", "integer", "text"], actual.Columns.Select(c => c.DataType));
        Assert.Equal(["alter_child_amount"], actual.Indexes.Select(i => i.Name));
        var fk = Assert.Single(actual.ForeignKeys);
        Assert.Equal(("fk_alter_parent", ReferentialAction.Cascade), (fk.Name, fk.OnDelete));
        Assert.Empty(generator.Alter(actual, actual));
    }
}
