using Squill.Core;
using Squill.Core.Design;
using Squill.Sql.Ddl;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class MySqlAlterTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Alter_EditedDesign_ProducesTheEditedTable()
    {
        await using var connection = await new MySqlDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "my", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null),
            MySqlFixture.Password,
            CancellationToken.None);

        async Task RunAsync(IEnumerable<string> statements)
        {
            foreach (var sql in statements)
            {
                await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
                Assert.Null(await reader.ReadBatchAsync(1, CancellationToken.None));
            }
        }

        var db = MySqlFixture.Database;
        var generator = DdlGenerators.For(DatabaseDialect.MySql);
        await RunAsync(["drop table if exists alter_child", "drop table if exists alter_parent"]);
        await RunAsync(generator.CreateTable(new TableDesign(
            db, "alter_parent", [new(null, "id", "int", false, null, false)], ["id"], null, [], [])));
        await RunAsync(generator.CreateTable(new TableDesign(
            db, "alter_child",
            [new(null, "id", "int", false, null, false), new(null, "qty", "smallint", true, null, false), new(null, "parent", "int", true, null, false)],
            ["id"], null, [], [])));

        var original = TableDesign.FromTable((await connection.ReadSchemaAsync(CancellationToken.None)).FindTable(db, "alter_child")!);
        var edited = original with
        {
            Columns =
            [
                original.Columns[0],
                original.Columns[1] with { Name = "amount", DataType = "bigint" },
                original.Columns[2],
                new ColumnDesign(null, "note", "text", true, null, false),
            ],
            PrimaryKey = [],
            Indexes = [new IndexDesign(null, "alter_child_amount", false, ["amount"])],
            ForeignKeys = [new ForeignKeyDesign(null, "fk_alter_parent", ["parent"], db, "alter_parent", ["id"], ReferentialAction.Cascade, ReferentialAction.NoAction)],
        };

        await RunAsync(generator.Alter(original, edited));

        var actual = TableDesign.FromTable((await connection.ReadSchemaAsync(CancellationToken.None)).FindTable(db, "alter_child")!);
        Assert.Equal(["id", "amount", "parent", "note"], actual.Columns.Select(c => c.Name));
        Assert.Equal(["int", "bigint", "int", "text"], actual.Columns.Select(c => c.DataType));
        Assert.Empty(actual.PrimaryKey);
        Assert.Contains("alter_child_amount", actual.Indexes.Select(i => i.Name));
        var fk = Assert.Single(actual.ForeignKeys);
        Assert.Equal(("fk_alter_parent", ReferentialAction.Cascade), (fk.Name, fk.OnDelete));
    }
}
