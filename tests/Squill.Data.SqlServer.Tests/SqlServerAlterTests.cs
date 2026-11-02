using Squill.Core;
using Squill.Core.Design;
using Squill.Sql.Ddl;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class SqlServerAlterTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Alter_EditedDesign_ProducesTheEditedTable()
    {
        await using var connection = await new SqlServerDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "ms", DatabaseDialect.SqlServer, server.Host, server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
            SqlServerFixture.Password,
            CancellationToken.None);

        async Task RunAsync(IEnumerable<string> statements)
        {
            foreach (var sql in statements)
            {
                await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
                Assert.Null(await reader.ReadBatchAsync(1, CancellationToken.None));
            }
        }

        var generator = DdlGenerators.For(DatabaseDialect.SqlServer);
        await RunAsync(["drop table if exists dbo.alter_child", "drop table if exists dbo.alter_parent"]);
        await RunAsync(
        [
            "create table dbo.alter_parent (id int not null primary key)",
            "create table dbo.alter_child (id int not null primary key, qty smallint not null constraint df_qty default 3, parent int null, note nvarchar(10) null)",
        ]);

        var original = TableDesign.FromTable((await connection.ReadSchemaAsync(CancellationToken.None)).FindTable("dbo", "alter_child")!);
        var edited = original with
        {
            Columns =
            [
                original.Columns[0],
                original.Columns[1] with { Name = "amount", DataType = "bigint", DefaultExpression = "7" },
                original.Columns[2],
                original.Columns[3],
            ],
            ForeignKeys = [new ForeignKeyDesign(null, "fk_alter_parent", ["parent"], "dbo", "alter_parent", ["id"], ReferentialAction.Cascade, ReferentialAction.Restrict)],
        };

        await RunAsync(generator.Alter(original, edited));

        var actual = TableDesign.FromTable((await connection.ReadSchemaAsync(CancellationToken.None)).FindTable("dbo", "alter_child")!);
        Assert.Equal(["id", "amount", "parent", "note"], actual.Columns.Select(c => c.Name));
        Assert.Equal("bigint", actual.Columns[1].DataType);
        Assert.Contains("7", actual.Columns[1].DefaultExpression);
        var fk = Assert.Single(actual.ForeignKeys);
        Assert.Equal(("fk_alter_parent", ReferentialAction.Cascade, ReferentialAction.NoAction), (fk.Name, fk.OnDelete, fk.OnUpdate));
    }
}
