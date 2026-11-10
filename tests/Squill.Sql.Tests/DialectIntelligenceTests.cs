using Squill.Core;
using Squill.Corpus;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;
using Squill.Sql.Formatting;
using Squill.Sql.Functions;
using Squill.Sql.Lexing;
using Xunit;

namespace Squill.Sql.Tests;

/// <summary>Completion, hover, signature help and formatting checked per dialect on the shared sample schema (tests/Shared/SampleSchema).</summary>
public sealed class DialectIntelligenceTests
{
    private static readonly DatabaseDialect[] Dialects = [DatabaseDialect.PostgreSql, DatabaseDialect.MySql, DatabaseDialect.SqlServer, DatabaseDialect.Sqlite];

    // Schema (or database) holding the sample objects, as the schema readers report it; SQLite has none.
    private static string? SchemaOf(DatabaseDialect dialect) => dialect switch
    {
        DatabaseDialect.PostgreSql => "sample",
        DatabaseDialect.SqlServer => "sample",
        DatabaseDialect.MySql => "shop",
        _ => null,
    };

    private static ColumnDefinition Col(string name, string type = "int", bool nullable = true, bool pk = false) => new(name, type, nullable, pk, null);

    /// <summary>The sample schema as the readers of the dialect return it.</summary>
    private static SchemaSnapshot Snapshot(DatabaseDialect dialect)
    {
        var s = SchemaOf(dialect);
        var routines = dialect == DatabaseDialect.Sqlite ? [] : new RoutineInfo[]
        {
            new(s!, "add_numbers", RoutineKind.Function, "add_numbers(a integer, b integer)", "integer", null),
            new(s!, "touch_customers", RoutineKind.Procedure, "touch_customers()", null, null),
        };
        return new SchemaSnapshot(s,
        [
            new TableInfo(s, "customers", TableKind.Table, [Col("id", nullable: false, pk: true), Col("name", "text"), Col("email", "text"), Col("created_at", "timestamp", false)], [], []),
            new TableInfo(s, "orders", TableKind.Table,
                [Col("id", nullable: false, pk: true), Col("customer_id", nullable: false), Col("placed_on", "date"), Col("total", "numeric"), Col("Ship To", "text")], [],
                [new ForeignKeyInfo("fk_orders_customer", ["customer_id"], s, "customers", ["id"])]),
            new TableInfo(s, "order_items", TableKind.Table,
                [Col("order_id", nullable: false, pk: true), Col("line_no", nullable: false, pk: true), Col("sku", "text"), Col("quantity")], [],
                [new ForeignKeyInfo("fk_items_order", ["order_id"], s, "orders", ["id"])]),
            new TableInfo(s, "no_pk", TableKind.Table, [Col("label", "text"), Col("amount")], [], []),
            new TableInfo(s, "order_totals", TableKind.View, [Col("customer_id"), Col("name", "text"), Col("orders"), Col("total", "numeric")], [], [],
                Definition: "\n  select c.id as customer_id, c.name\n  from customers c"),
        ])
        { Routines = routines };
    }

    private static CompositeCompletionProvider Provider { get; } =
        new CompositeCompletionProvider(new KeywordCompletionProvider(), new SchemaCompletionProvider(), new FunctionCompletionProvider());

    private static string Quote(string name, DatabaseDialect dialect) => dialect switch
    {
        DatabaseDialect.MySql => "`" + name + "`",
        DatabaseDialect.SqlServer => "[" + name + "]",
        _ => "\"" + name + "\"",
    };

    private sealed record Case(string Name, string Marked, string[] Expected, string[] Unexpected);

    private static Case[] Cases(DatabaseDialect dialect)
    {
        var schema = SchemaOf(dialect);
        var ss = dialect == DatabaseDialect.SqlServer;
        var ship = Quote("Ship To", dialect);
        var fk = "o.customer_id = c.id";
        return
        [
            new("tables", "select * from |", ["customers", "orders", "order_items", "order_totals"], ["id"]),
            schema is null
                ? new("prefix", "select * from cu|", ["customers"], ["id"])
                : new("schema-qualified", $"select * from {schema}.|", ["customers", "orders"], [schema]),
            new("alias-columns", "select c.| from customers c", ["id", "name", "email"], ["placed_on"]),
            new("quoted-column", "select o.| from orders o", [ship, "total"], ["name"]),
            new("cte-columns", "with t as (select id, name from customers) select t.| from t", ["id", "name"], ["email"]),
            new("insert-columns", "insert into customers (|", ["name", "email"], ["orders"]),
            new("fk-join", "select * from orders o join customers c on |", [fk], []),
            new("fk-join-reverse", "select * from customers c join orders o on |", ["c.id = o.customer_id"], []),
            dialect == DatabaseDialect.Sqlite
                ? new("builtin-function", "select | from customers", ["typeof", "strftime"], ["GETDATE", "date_trunc"])
                : new("routine-function", "select | from customers", ["add_numbers"], ["touch_customers"]),
            new("select-top", "select |", ss ? ["TOP"] : [], ss ? [] : ["TOP"]),
            new("after-from", "select * from customers |", ss ? ["OFFSET"] : ["LIMIT"], ss ? ["LIMIT", "TOP"] : ["TOP"]),
            new("like", "select * from customers where name |", ["LIKE"], dialect == DatabaseDialect.PostgreSql ? [] : ["ILIKE"]),
            dialect switch
            {
                DatabaseDialect.PostgreSql => new("null-fallback", "select | from customers", ["COALESCE"], ["ISNULL", "IFNULL", "GETDATE"]),
                DatabaseDialect.SqlServer => new("null-fallback", "select | from customers", ["ISNULL"], ["IFNULL", "date_trunc"]),
                _ => new("null-fallback", "select | from customers", ["IFNULL"], ["GETDATE", "date_trunc"]),
            },
            new("where", "select * from orders where |", ["customer_id", "COALESCE", "orders"], ["email"]),
            new("view-columns", "select v.| from order_totals v", ["customer_id", "total", "orders"], ["email"]),
        ];
    }

    public static TheoryData<DatabaseDialect, string> CompletionCases()
    {
        var data = new TheoryData<DatabaseDialect, string>();
        foreach (var d in Dialects)
            foreach (var c in Cases(d))
                data.Add(d, c.Name);
        return data;
    }

    [Theory]
    [MemberData(nameof(CompletionCases))]
    public void Completion_OffersWhatTheDialectHasAndNothingElse(DatabaseDialect dialect, string name)
    {
        var c = Cases(dialect).Single(x => x.Name == name);
        var offset = c.Marked.IndexOf('|');
        var items = Provider.GetCompletions(new SqlRequest(c.Marked.Remove(offset, 1), offset, dialect, Snapshot(dialect)));
        var inserted = items.Select(i => i.InsertText).ToList();
        foreach (var e in c.Expected)
            Assert.True(inserted.Contains(e, StringComparer.OrdinalIgnoreCase), $"{dialect} {name}: missing '{e}' in [{string.Join(", ", inserted)}]");
        foreach (var u in c.Unexpected)
            Assert.False(inserted.Contains(u, StringComparer.OrdinalIgnoreCase), $"{dialect} {name}: unexpected '{u}'");
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql)]
    [InlineData(DatabaseDialect.MySql)]
    [InlineData(DatabaseDialect.SqlServer)]
    public void Completion_StoredFunctionsShowTheirSignatureAndProceduresAreLeftOut(DatabaseDialect dialect)
    {
        var items = new FunctionCompletionProvider().GetCompletions(new SqlRequest("select ", 7, dialect, Snapshot(dialect)));
        var item = Assert.Single(items, i => i.Label == "add_numbers");
        Assert.Equal(CompletionKind.Function, item.Kind);
        Assert.Equal("add_numbers(a integer, b integer) -> integer", item.Detail);
        Assert.DoesNotContain(items, i => i.Label == "touch_customers");
    }

    // ---- function catalog audit: the 40 most used built-in functions of each engine ----

    private static readonly Dictionary<DatabaseDialect, string[]> TopFunctions = new()
    {
        [DatabaseDialect.PostgreSql] = ["count", "sum", "avg", "min", "max", "coalesce", "nullif", "length", "concat", "concat_ws", "left", "right", "lpad", "rpad", "position", "now",
            "date_trunc", "extract", "to_char", "to_date", "age", "string_agg", "array_agg", "generate_series", "regexp_replace", "split_part", "floor", "ceil", "power", "sqrt", "mod",
            "random", "greatest", "least", "row_number", "rank", "lag", "lead", "json_agg", "unnest"],
        [DatabaseDialect.MySql] = ["count", "sum", "avg", "min", "max", "coalesce", "nullif", "ifnull", "if", "concat", "concat_ws", "length", "char_length", "left", "right", "lpad", "rpad",
            "locate", "now", "curdate", "date_format", "date_add", "date_sub", "datediff", "str_to_date", "extract", "group_concat", "floor", "ceil", "power", "sqrt", "rand",
            "greatest", "least", "row_number", "rank", "lag", "lead", "json_extract", "substring_index"],
        [DatabaseDialect.SqlServer] = ["count", "sum", "avg", "min", "max", "coalesce", "nullif", "isnull", "iif", "len", "left", "right", "substring", "charindex", "concat", "string_agg",
            "getdate", "sysdatetime", "dateadd", "datediff", "datepart", "eomonth", "format", "convert", "try_convert", "try_cast", "floor", "ceiling", "power", "sqrt", "round", "newid",
            "row_number", "rank", "lag", "lead", "stuff", "replicate", "string_split", "object_id"],
        [DatabaseDialect.Sqlite] = ["count", "sum", "avg", "min", "max", "total", "coalesce", "nullif", "ifnull", "iif", "length", "substr", "instr", "replace", "upper", "lower", "trim",
            "ltrim", "rtrim", "abs", "round", "random", "typeof", "date", "time", "datetime", "julianday", "strftime", "group_concat", "json_extract", "json_object", "json_array",
            "printf", "hex", "quote", "last_insert_rowid", "row_number", "rank", "lag", "lead"],
    };

    public static TheoryData<DatabaseDialect> DialectData() => new(Dialects);

    [Theory]
    [MemberData(nameof(DialectData))]
    public void Catalog_ContainsTheFortyMostUsedFunctions(DatabaseDialect dialect)
    {
        Assert.Equal(40, TopFunctions[dialect].Length);
        var names = FunctionCatalog.For(dialect).Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var n in TopFunctions[dialect])
            Assert.True(names.Contains(n), $"{dialect}: missing {n}");
        Assert.All(FunctionCatalog.For(dialect), f => Assert.False(string.IsNullOrWhiteSpace(f.Description), f.Name));
    }

    [Theory]
    [MemberData(nameof(DialectData))]
    public void Catalog_HasNoDuplicateNames(DatabaseDialect dialect) =>
        Assert.Empty(FunctionCatalog.For(dialect).GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key));

    public static TheoryData<DatabaseDialect, string, string, int> Signatures() => new()
    {
        { DatabaseDialect.PostgreSql, "select date_trunc('day', |", "date_trunc", 1 },
        { DatabaseDialect.PostgreSql, "select split_part(a, ',', |", "split_part", 2 },
        { DatabaseDialect.PostgreSql, "select lpad(|", "lpad", 0 },
        { DatabaseDialect.PostgreSql, "select string_agg(a, |", "string_agg", 1 },
        { DatabaseDialect.PostgreSql, "select to_char(now(), |", "to_char", 1 },
        { DatabaseDialect.MySql, "select date_format(|", "date_format", 0 },
        { DatabaseDialect.MySql, "select ifnull(a, |", "ifnull", 1 },
        { DatabaseDialect.MySql, "select substring_index(a, ',', |", "substring_index", 2 },
        { DatabaseDialect.MySql, "select concat_ws('-', a, |", "concat_ws", 2 },
        { DatabaseDialect.MySql, "select locate(|", "locate", 0 },
        { DatabaseDialect.SqlServer, "select dateadd(day, 1, |", "dateadd", 2 },
        { DatabaseDialect.SqlServer, "select isnull(a, |", "isnull", 1 },
        { DatabaseDialect.SqlServer, "select iif(a > 1, |", "iif", 1 },
        { DatabaseDialect.SqlServer, "select stuff(a, 1, 2, |", "stuff", 3 },
        { DatabaseDialect.SqlServer, "select charindex(|", "charindex", 0 },
        { DatabaseDialect.Sqlite, "select strftime('%Y', |", "strftime", 1 },
        { DatabaseDialect.Sqlite, "select substr(a, 1, |", "substr", 2 },
        { DatabaseDialect.Sqlite, "select iif(a, |", "iif", 1 },
        { DatabaseDialect.Sqlite, "select instr(|", "instr", 0 },
        { DatabaseDialect.Sqlite, "select printf('%d', |", "printf", 1 },
    };

    [Theory]
    [MemberData(nameof(Signatures))]
    public void SignatureHelp_FindsTheFunctionOfTheDialect(DatabaseDialect dialect, string marked, string name, int active)
    {
        var offset = marked.IndexOf('|');
        var help = SignatureHelpProvider.GetSignatureHelp(new SqlRequest(marked.Remove(offset, 1), offset, dialect, null));
        Assert.NotNull(help);
        Assert.Equal(name, help.Signature.Name, ignoreCase: true);
        Assert.Equal(active, help.ActiveParameter);
        Assert.NotEmpty(help.Signature.Parameters);
    }

    // ---- hover ----

    private static HoverInfo? Hover(DatabaseDialect dialect, string marked)
    {
        var offset = marked.IndexOf('|');
        return SqlHoverProvider.GetHover(new SqlRequest(marked.Remove(offset, 1), offset, dialect, Snapshot(dialect)));
    }

    [Theory]
    [MemberData(nameof(DialectData))]
    public void Hover_TableShowsItsColumnCount(DatabaseDialect dialect)
    {
        var hover = Hover(dialect, "select * from cust|omers");
        Assert.Equal(SqlMessageCodes.HoverTable, hover?.Code);
        Assert.Contains("(4 columns)", hover!.Markdown);
    }

    [Theory]
    [MemberData(nameof(DialectData))]
    public void Hover_ColumnShowsTypeNullabilityKeyAndForeignKeyTarget(DatabaseDialect dialect)
    {
        var s = SchemaOf(dialect);
        var fk = Hover(dialect, "select o.customer|_id from orders o");
        Assert.Equal(SqlMessageCodes.HoverColumnNotNullFk, fk?.Code);
        Assert.Equal($"`customer_id`: int, not null, FK to {(s is null ? string.Empty : s + ".")}customers(id)", fk!.Markdown);

        var pk = Hover(dialect, "select c.i|d from customers c");
        Assert.Equal(SqlMessageCodes.HoverColumnNotNullPk, pk?.Code);
        Assert.Equal("`id`: int, not null, PK", pk!.Markdown);
    }

    [Theory]
    [MemberData(nameof(DialectData))]
    public void Hover_ViewShowsTheFirstLineOfItsDefinition(DatabaseDialect dialect)
    {
        var hover = Hover(dialect, "select * from order_|totals");
        Assert.Equal(SqlMessageCodes.HoverViewDefinition, hover?.Code);
        Assert.Contains("`select c.id as customer_id, c.name`", hover!.Markdown);
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql)]
    [InlineData(DatabaseDialect.MySql)]
    [InlineData(DatabaseDialect.SqlServer)]
    public void Hover_StoredFunctionAndProcedure(DatabaseDialect dialect)
    {
        var s = SchemaOf(dialect);
        var function = Hover(dialect, "select add_|numbers(1, 2)");
        Assert.Equal(SqlMessageCodes.HoverFunction, function?.Code);
        Assert.Equal($"**function** {s}.add_numbers(a integer, b integer) returns integer", function!.Markdown);

        var procedure = Hover(dialect, "call touch_|customers()");
        Assert.Equal(SqlMessageCodes.HoverProcedure, procedure?.Code);
    }

    // ---- formatter over the valid statements of the P11-27 corpus ----

    private static readonly (string File, DatabaseDialect Dialect)[] CorpusFiles =
        [("postgres", DatabaseDialect.PostgreSql), ("mysql", DatabaseDialect.MySql), ("sqlserver", DatabaseDialect.SqlServer), ("sqlite", DatabaseDialect.Sqlite)];

    public static TheoryData<string, int> CorpusEntries()
    {
        var data = new TheoryData<string, int>();
        foreach (var (file, _) in CorpusFiles)
            foreach (var entry in SqlCorpus.Load(file, "valid"))
                data.Add(file, entry.Index);
        return data;
    }

    private static List<string> Significant(string sql, DatabaseDialect dialect) =>
        [.. SqlTokenizers.For(dialect).Tokenize(sql)
            .Where(t => t.Kind is not (TokenKind.Whitespace or TokenKind.EndOfFile))
            .Select(t => t.Kind is TokenKind.Keyword or TokenKind.Identifier ? t.Text(sql).ToUpperInvariant() : t.Text(sql))];

    [Theory]
    [MemberData(nameof(CorpusEntries))]
    public void Format_KeepsTheTokensOfEveryCorpusStatementAndIsStable(string file, int index)
    {
        var dialect = CorpusFiles.Single(f => f.File == file).Dialect;
        var entry = SqlCorpus.Load(file, "valid").Single(e => e.Index == index);
        var once = SqlFormatter.Format(entry.Sql, dialect, FormatOptions.Default);
        Assert.Equal(Significant(entry.Sql, dialect), Significant(once, dialect));
        Assert.Equal(once, SqlFormatter.Format(once, dialect, FormatOptions.Default));
    }
}
