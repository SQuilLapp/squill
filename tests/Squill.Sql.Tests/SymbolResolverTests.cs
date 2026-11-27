using System.Diagnostics;
using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Syntax;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class SymbolResolverTests
{
    private static ColumnDefinition Col(string name) => new(name, "int", true, false, null);

    private static readonly SchemaSnapshot Schema = new("public",
    [
        new TableInfo("public", "Orders", TableKind.Table, [Col("id"), Col("customer_id"), Col("Total")], [], []),
        new TableInfo("public", "customers", TableKind.Table, [Col("id"), Col("name")], [], []),
        new TableInfo("public", "order_totals", TableKind.View, [Col("customer_id"), Col("total")], [], []),
    ]);

    public static TheoryData<DatabaseDialect> Dialects() =>
        [DatabaseDialect.PostgreSql, DatabaseDialect.MySql, DatabaseDialect.SqlServer, DatabaseDialect.Sqlite, DatabaseDialect.Oracle, DatabaseDialect.Informix];

    /// <summary>Resolves at the <c>|</c> marker, which is removed from the text.</summary>
    private static (SqlSymbol? Symbol, SqlScript Script) At(string marked, DatabaseDialect dialect = DatabaseDialect.PostgreSql, SchemaSnapshot? schema = null)
    {
        var offset = marked.IndexOf('|');
        var script = SqlParser.Parse(marked.Remove(offset, 1), dialect);
        return (SqlSymbolResolver.SymbolAt(script, offset, schema ?? Schema), script);
    }

    /// <summary>The text of the declaration and its offset, or a dash.</summary>
    private static string Def(SqlSymbol symbol, SqlScript script)
        => symbol.DefinitionInScript is { } r ? script.Text.Substring(r.Start, r.Length) + "@" + r.Start : "-";

    [Theory]
    [MemberData(nameof(Dialects))]
    public void Alias_ResolvesToItsDeclaration(DatabaseDialect dialect)
    {
        var (symbol, script) = At("select o|.id from orders o", dialect);
        Assert.Equal((SqlSymbolKind.Alias, "o", "o@24"), (symbol!.Kind, symbol.Name, Def(symbol, script)));
        Assert.True(symbol.DefinitionInSchema);
    }

    [Theory]
    [MemberData(nameof(Dialects))]
    public void QualifiedColumn_ResolvesToTheTableColumn(DatabaseDialect dialect)
    {
        var (symbol, _) = At("select o.cust|omer_id from orders o", dialect);
        Assert.Equal(new SqlSymbol(SqlSymbolKind.Column, "customer_id", "public", "Orders", null, true), symbol);
    }

    [Theory]
    [MemberData(nameof(Dialects))]
    public void Table_IsTableOrViewOrUnknown(DatabaseDialect dialect)
    {
        Assert.Equal(new SqlSymbol(SqlSymbolKind.Table, "Orders", "public", null, null, true), At("select 1 from ord|ers o", dialect).Symbol);
        Assert.Equal(SqlSymbolKind.View, At("select 1 from public.order_t|otals", dialect).Symbol!.Kind);
        Assert.Equal(new SqlSymbol(SqlSymbolKind.Table, "ghost", null, null, null, false), At("select 1 from gh|ost", dialect).Symbol);
    }

    [Theory]
    [MemberData(nameof(Dialects))]
    public void Cte_AndItsColumns_ResolveInsideTheScript(DatabaseDialect dialect)
    {
        const string sql = "with c as (select id as k, name from customers) select c.k from c";
        var (cte, script) = At(sql.Replace("k from c", "k from |c"), dialect);
        Assert.Equal((SqlSymbolKind.Cte, "c", "c@5"), (cte!.Kind, cte.Name, Def(cte, script)));

        var (column, script2) = At(sql.Replace("c.k", "c.|k"), dialect);
        Assert.Equal((SqlSymbolKind.DerivedColumn, "k", "c", "k@" + (sql.IndexOf("as k", StringComparison.Ordinal) + 3)), (column!.Kind, column.Name, column.Table, Def(column, script2)));
    }

    [Fact]
    public void CteDeclaredColumns_AndNestedCtes_AreFollowedOneHopAtATime()
    {
        var (declared, script) = At("with c(p, q) as (select 1, 2) select c.q| from c");
        Assert.Equal("q@10", Def(declared!, script));

        const string nested = "with a as (select id as x from customers), b as (select x as y from a) select y from b";
        var (column, s3) = At(nested.Replace("select y", "select y|"));
        Assert.Equal((SqlSymbolKind.DerivedColumn, "b"), (column!.Kind, column.Table));
        Assert.Equal("y@" + (nested.IndexOf("x as y", StringComparison.Ordinal) + 5), Def(column, s3));

        var (inner, s4) = At(nested.Replace("(select x as", "(select x| as"));
        Assert.Equal(("x", "a"), (inner!.Name, inner.Table));
        Assert.Equal("x@" + (nested.IndexOf("id as x", StringComparison.Ordinal) + 6), Def(inner, s4));
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "with recursive r(n) as (select 1 union all select n + 1 from r where n < 5) select n from r")]
    [InlineData(DatabaseDialect.Sqlite, "with recursive r(n) as (select 1 union all select n + 1 from r where n < 5) select n from r")]
    [InlineData(DatabaseDialect.MySql, "with recursive r(n) as (select 1 union all select n + 1 from r where n < 5) select n from r")]
    [InlineData(DatabaseDialect.SqlServer, "with r(n) as (select 1 union all select n + 1 from r where n < 5) select n from r")]
    [InlineData(DatabaseDialect.Oracle, "with r(n) as (select 1 from dual union all select n + 1 from r where n < 5) select n from r")]
    public void RecursiveCte_ReferencesItself(DatabaseDialect dialect, string sql)
    {
        var inner = sql.IndexOf("from r where", StringComparison.Ordinal) + 5;
        var (self, script) = At(sql.Insert(inner, "|"), dialect);
        Assert.Equal((SqlSymbolKind.Cte, "r@" + (sql.IndexOf("r(n)", StringComparison.Ordinal))), (self!.Kind, Def(self, script)));
        var (column, s2) = At(sql.Insert(sql.IndexOf("n < 5", StringComparison.Ordinal), "|"), dialect);
        Assert.Equal((SqlSymbolKind.DerivedColumn, "r"), (column!.Kind, column.Table));
        Assert.Equal("n@" + (s2.Text.IndexOf("r(n)", StringComparison.Ordinal) + 2), Def(column, s2));
    }

    [Theory]
    [MemberData(nameof(Dialects))]
    public void SubqueryInFrom_ResolvesAliasAndColumn(DatabaseDialect dialect)
    {
        const string sql = "select s.total from (select customer_id, Total as total from orders) s";
        var (alias, _) = At(sql.Replace("s.total", "|s.total"), dialect);
        Assert.Equal((SqlSymbolKind.DerivedTable, sql.LastIndexOf('s')), (alias!.Kind, alias.DefinitionInScript!.Value.Start));
        var (column, _) = At(sql.Replace("s.total", "s.tot|al"), dialect);
        Assert.Equal((SqlSymbolKind.DerivedColumn, "total", "s"), (column!.Kind, column.Name, column.Table));
        Assert.Equal(sql.IndexOf("as total", StringComparison.Ordinal) + 3, column.DefinitionInScript!.Value.Start);
    }

    [Fact]
    public void CorrelatedSubquery_ResolvesTheOuterAlias()
    {
        var (symbol, script) = At("select (select count(*) from customers c where c.id = o|.customer_id) from orders o");
        Assert.Equal((SqlSymbolKind.Alias, script.Text.LastIndexOf('o')), (symbol!.Kind, symbol.DefinitionInScript!.Value.Start));
        var (column, _) = At("select (select count(*) from customers c where c.id = o.cust|omer_id) from orders o");
        Assert.Equal(new SqlSymbol(SqlSymbolKind.Column, "customer_id", "public", "Orders", null, true), column);
    }

    [Fact]
    public void JoinUsing_AmbiguousColumnIsNullButQualifiedIsNot()
    {
        Assert.Null(At("select i|d from orders o join customers c using (id)").Symbol);
        Assert.Equal("customers", At("select c.i|d from orders o join customers c using (id)").Symbol!.Table);
        Assert.Equal("customers", At("select na|me from orders o join customers c using (id)").Symbol!.Table);
    }

    [Theory]
    [MemberData(nameof(Dialects))]
    public void SelectAlias_WinsInOrderBy(DatabaseDialect dialect)
    {
        var (symbol, script) = At("select Total as t, id from orders order by t|", dialect);
        Assert.Equal((SqlSymbolKind.SelectAlias, script.Text.IndexOf("as t", StringComparison.Ordinal) + 3), (symbol!.Kind, symbol.DefinitionInScript!.Value.Start));
        // the alias shadows the column of the same name in ORDER BY only
        Assert.Equal(SqlSymbolKind.SelectAlias, At("select id as total from orders order by tot|al", dialect).Symbol!.Kind);
        Assert.Equal(SqlSymbolKind.Column, At("select id as total from orders where tot|al > 1", dialect).Symbol!.Kind);
    }

    [Fact]
    public void QuotedMixedCaseNames_MatchIgnoringCase()
    {
        Assert.Equal(new SqlSymbol(SqlSymbolKind.Column, "Total", "public", "Orders", null, true), At("select o.\"to|tal\" from \"Orders\" o").Symbol);
        Assert.Equal(SqlSymbolKind.Table, At("select 1 from \"Ord|ers\"").Symbol!.Kind);
        Assert.Equal(SqlSymbolKind.Column, At("select [o].[to|tal] from [Orders] o", DatabaseDialect.SqlServer).Symbol!.Kind);
        Assert.Equal(SqlSymbolKind.Column, At("select o.`to|tal` from `Orders` o", DatabaseDialect.MySql).Symbol!.Kind);
    }

    [Fact]
    public void UnqualifiedColumn_NeedsTheSchemaOrASingleSource()
    {
        Assert.Equal("customers", At("select na|me from customers").Symbol!.Table);
        Assert.Null(At("select na|me from customers, orders", schema: SchemaSnapshot.Empty).Symbol);
        var lone = At("select na|me from customers", schema: SchemaSnapshot.Empty).Symbol!;
        Assert.Equal((SqlSymbolKind.Column, "customers", false), (lone.Kind, lone.Table, lone.DefinitionInSchema));
        Assert.Null(At("select zz|z from customers").Symbol);
    }

    [Fact]
    public void NoSymbol_OffNames()
    {
        Assert.Null(At("sel|ect 1").Symbol);
        Assert.Null(At("select 1 |").Symbol);
        Assert.Null(At("|").Symbol);
    }

    [Fact]
    public void Usages_OfAnAlias_StayInTheirStatement()
    {
        var (symbol, script) = At("select o|.id, o.total from orders o; select o.id from customers o");
        var usages = SqlSymbolResolver.Usages(script, symbol!, Schema);
        Assert.Equal(3, usages.Count);
        Assert.All(usages, u => Assert.True(u.Start < script.Text.IndexOf(';', StringComparison.Ordinal)));
    }

    [Fact]
    public void Usages_OfATableAndItsColumn_SpanTheScript()
    {
        const string sql = "select o.id from orders o; select id from public.orders ; select x.id from customers x";
        var (table, script) = At(sql.Replace("orders o", "ord|ers o"));
        Assert.Equal([sql.IndexOf("orders o", StringComparison.Ordinal), sql.IndexOf("orders ;", StringComparison.Ordinal)],
            SqlSymbolResolver.Usages(script, table!, Schema).Select(u => u.Start));

        var (column, s2) = At(sql.Replace("o.id", "o.i|d"));
        Assert.Equal([sql.IndexOf("id", StringComparison.Ordinal), sql.IndexOf("id from public", StringComparison.Ordinal)],
            SqlSymbolResolver.Usages(s2, column!, Schema).Select(u => u.Start));
    }

    [Fact]
    public void Usages_OfACteAndItsColumn()
    {
        var (cte, script) = At("with c as (select id as k from customers) select c.k from |c join c d on 1 = 1");
        Assert.Equal(["c", "c", "c", "c"], SqlSymbolResolver.Usages(script, cte!, Schema).Select(u => script.Text.Substring(u.Start, u.Length)));
        var (column, s2) = At("with c as (select id as k from customers) select c.k|, d.k from c join c d on 1 = 1 order by c.k");
        Assert.Equal(4, SqlSymbolResolver.Usages(s2, column!, Schema).Count);
    }

    [Fact]
    public void Usages_AreEmptyOfWhatTheScriptDoesNotName()
    {
        var script = SqlParser.Parse("select 1", DatabaseDialect.PostgreSql);
        Assert.Empty(SqlSymbolResolver.Usages(script, new SqlSymbol(SqlSymbolKind.Table, "x", null, null, null, false)));
    }

    [Fact]
    public void HalfTypedStatements_NeverThrow()
    {
        const string sql = "with c(a) as (select id as k from customers), d as (select * from c) "
            + "select o.id, (select count(*) from customers x where x.id = o.customer_id) n, s.k "
            + "from orders o join (select k from c) s on s.k = o.id where o.total > 1 order by n; update orders set total = 1 where id = 2;";
        foreach (var dialect in Enum.GetValues<DatabaseDialect>())
        {
            for (var length = 0; length <= sql.Length; length += 5)
            {
                var script = SqlParser.Parse(sql[..length], dialect);
                for (var offset = 0; offset <= length; offset += 3)
                {
                    var symbol = SqlSymbolResolver.SymbolAt(script, offset, Schema);
                    if (symbol is not null)
                    {
                        _ = SqlSymbolResolver.Usages(script, symbol, Schema);
                    }
                }
            }
        }
    }

    [Fact]
    public void LargeScript_ResolvesQuickly()
    {
        var body = string.Concat(Enumerable.Range(0, 1250).Select(i => $"select o.id, c.name from orders o join customers c on c.id = o.customer_id where o.total > {i};\n"));
        var text = body + string.Concat(Enumerable.Repeat("-- filler\n", 3750));
        var script = SqlParser.Parse(text, DatabaseDialect.PostgreSql);
        var offset = text.LastIndexOf("o.id", StringComparison.Ordinal) + 2;
        SqlSymbolResolver.SymbolAt(script, offset, Schema);
        var watch = Stopwatch.StartNew();
        var symbol = SqlSymbolResolver.SymbolAt(script, offset, Schema);
        watch.Stop();
        Assert.Equal("id", symbol!.Name);
        Assert.True(watch.ElapsedMilliseconds < 50, $"took {watch.ElapsedMilliseconds} ms");
    }
}
