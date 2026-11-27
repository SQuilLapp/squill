using Squill.Core;
using Xunit;

namespace Squill.Data.Informix.Tests;

/// <summary>The schema reader against a real Informix developer server holding a sample schema.</summary>
public sealed class InformixSchemaTests(InformixFixture server) : IClassFixture<InformixFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task RunAsync(InformixDatabaseConnection c, string sql)
    {
        await using var reader = await c.ExecuteAsync(sql, [], Token);
        while (await reader.ReadBatchAsync(500, Token) is not null)
        {
        }
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchema_SampleSchema_GivesTablesKeysIndexesViewsRoutinesAndTriggers()
    {
        Assert.SkipWhen(InformixFixture.FindClidriver() is null, "IBM clidriver not found: set SQUILL_CLIDRIVER or restore the Net.IBM.Data.Db2 package (see InformixFixture).");
        await using var connection = await InformixDatabaseConnection.OpenAsync(server.ConnectionString(database: InformixFixture.SchemaDatabase), Token);
        await RunAsync(connection, "create table parent (id serial not null, name varchar(40) not null, note lvarchar(500), primary key (id) constraint pk_parent)");
        await RunAsync(connection, "create table child (id integer not null, parent_id integer, label char(8), unique (label) constraint u_label, foreign key (parent_id) references parent (id) on delete cascade constraint fk_child_parent)");
        await RunAsync(connection, "create index ix_child_label_parent on child (parent_id, label desc)");
        await RunAsync(connection, "create table alltypes (c char(10), si smallint, i integer, f float, sf smallfloat, d decimal(12,3), dt date, m money(10,2), ts datetime year to fraction(5), t datetime hour to second, b byte, tx text, v varchar(30) not null, iv interval day(3) to second, nc nchar(5), nv nvarchar(20), i8 int8, lv lvarchar(1000), bo boolean, bi bigint, bl blob, cl clob)");
        await RunAsync(connection, "create table serials (s serial8)");
        await RunAsync(connection, "create table bigserials (b bigserial)");
        await RunAsync(connection, "create view v_child as select id, label from child where id > 0");
        await RunAsync(connection, "create procedure add_one(a integer) returning integer; return a + 1; end procedure;");
        await RunAsync(connection, "create trigger tg_parent_ins insert on parent after (insert into child (id) values (0))");

        var snapshot = await connection.ReadSchemaAsync(Token);

        Assert.Equal("informix", snapshot.DefaultSchema);
        Assert.Equal(["alltypes", "bigserials", "child", "parent", "serials", "v_child"], snapshot.Tables.Select(t => t.Name).Order());
        Assert.All(snapshot.Tables, t => Assert.Equal("informix", t.Schema));
        var parent = snapshot.FindTable(null, "parent")!;
        Assert.Equal("pk_parent", parent.PrimaryKeyName);
        Assert.Equal("id:SERIAL:False:True:True,name:VARCHAR(40):False:False:False,note:LVARCHAR(500):True:False:False",
            string.Join(',', parent.Columns.Select(c => $"{c.Name}:{c.DataType}:{c.IsNullable}:{c.IsPrimaryKey}:{c.IsAutoIncrement}")));
        Assert.Empty(parent.Indexes);

        var child = snapshot.FindTable(null, "child")!;
        Assert.Equal("u_label,True,label|ix_child_label_parent,False,parent_id+label",
            string.Join('|', child.Indexes.OrderByDescending(i => i.IsUnique).Select(i => $"{i.Name},{i.IsUnique},{string.Join('+', i.Columns)}")));
        var fk = Assert.Single(child.ForeignKeys);
        Assert.Equal("fk_child_parent,informix,parent,parent_id,id,Cascade",
            $"{fk.Name},{fk.ReferencedSchema},{fk.ReferencedTable},{string.Join('+', fk.Columns)},{string.Join('+', fk.ReferencedColumns)},{fk.OnDelete}");

        Assert.Equal(
            "CHAR(10),SMALLINT,INTEGER,FLOAT,SMALLFLOAT,DECIMAL(12,3),DATE,MONEY(10,2),DATETIME YEAR TO FRACTION(5),DATETIME HOUR TO SECOND,BYTE,TEXT,VARCHAR(30),INTERVAL DAY(3) TO SECOND,NCHAR(5),NVARCHAR(20),INT8,LVARCHAR(1000),BOOLEAN,BIGINT,BLOB,CLOB",
            string.Join(',', snapshot.FindTable(null, "alltypes")!.Columns.Select(c => c.DataType)));

        Assert.Equal("SERIAL8,BIGSERIAL", snapshot.FindTable(null, "serials")!.Columns[0].DataType + "," + snapshot.FindTable(null, "bigserials")!.Columns[0].DataType);

        var view = snapshot.FindTable(null, "v_child")!;
        Assert.Equal(TableKind.View, view.Kind);
        Assert.Contains("select", view.Definition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("child", view.Definition, StringComparison.OrdinalIgnoreCase);

        var routine = Assert.Single(snapshot.Routines);
        Assert.Equal("add_one", routine.Name);
        Assert.Contains("add_one", routine.Definition, StringComparison.OrdinalIgnoreCase);
        var trigger = Assert.Single(snapshot.Triggers);
        Assert.Equal("informix,parent,tg_parent_ins,AFTER,INSERT", $"{trigger.Schema},{trigger.Table},{trigger.Name},{trigger.Timing},{trigger.Events}");
        Assert.Contains("insert", trigger.Definition, StringComparison.OrdinalIgnoreCase);
    }
}
