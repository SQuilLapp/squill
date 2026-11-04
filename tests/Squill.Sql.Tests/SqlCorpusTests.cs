using Squill.Core;
using Squill.Corpus;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;
using Squill.Sql.Lexing;
using Squill.Sql.Statements;
using Xunit;

namespace Squill.Sql.Tests;

/// <summary>Statements validated against real servers (see the Docker-gated tests of each driver) must be split, parsed and diagnosed without error.</summary>
public sealed class SqlCorpusTests
{
    private static readonly (string File, DatabaseDialect Dialect, string? Schema)[] Dialects =
    [
        ("postgres", DatabaseDialect.PostgreSql, "sample"),
        ("mysql", DatabaseDialect.MySql, null),
        ("sqlserver", DatabaseDialect.SqlServer, "sample"),
        ("sqlite", DatabaseDialect.Sqlite, null),
    ];

    private static ColumnDefinition Col(string name, string type = "int") => new(name, type, true, false, null);

    // The sample schema of tests/Shared/SampleSchema, as the schema reader would return it.
    private static SchemaSnapshot Snapshot(string? schema) => new(schema,
    [
        new TableInfo(schema, "customers", TableKind.Table, [Col("id"), Col("name", "text"), Col("email", "text"), Col("created_at", "timestamp")], [], []),
        new TableInfo(schema, "orders", TableKind.Table, [Col("id"), Col("customer_id"), Col("placed_on", "date"), Col("total", "numeric")], [], []),
        new TableInfo(schema, "order_items", TableKind.Table, [Col("order_id"), Col("line_no"), Col("sku", "text"), Col("quantity")], [], []),
        new TableInfo(schema, "no_pk", TableKind.Table, [Col("label", "text"), Col("amount")], [], []),
        new TableInfo(schema, "order_totals", TableKind.View, [Col("customer_id"), Col("name", "text"), Col("orders"), Col("total", "numeric")], [], []),
    ]);

    public static TheoryData<string, int> ValidEntries() => Entries("valid");

    public static TheoryData<string, int> InvalidEntries() => Entries("invalid");

    private static TheoryData<string, int> Entries(string kind)
    {
        var data = new TheoryData<string, int>();
        foreach (var (file, _, _) in Dialects)
            foreach (var entry in SqlCorpus.Load(file, kind))
                data.Add(file, entry.Index);
        return data;
    }

    private static (CorpusEntry Entry, DatabaseDialect Dialect, string? Schema) Find(string file, int index, string kind)
    {
        var d = Dialects.Single(x => x.File == file);
        return (SqlCorpus.Load(file, kind).Single(e => e.Index == index), d.Dialect, d.Schema);
    }

    [Theory]
    [MemberData(nameof(ValidEntries))]
    public void ValidStatement_IsSplitAndHasNoError(string file, int index)
    {
        var (entry, dialect, schema) = Find(file, index, "valid");
        if (entry.Unsupported is { } reason)
        {
            Console.WriteLine($"unsupported {file}#{index}: {reason}");
            return;
        }

        var spans = SqlStatementSplitter.Split(entry.Sql, dialect);
        Assert.Equal(entry.Statements, spans.Count);
        if (entry.Statements == 1)
        {
            // the statement starts at its first token and ends at its last one (a trailing ; may or may not be part of it); comments around it are not
            var tokens = SqlTokenizers.For(dialect).Tokenize(entry.Sql).Where(t => t.Kind is not (TokenKind.Whitespace or TokenKind.LineComment or TokenKind.BlockComment)).ToList();
            var last = tokens[^1].Length == 1 && entry.Sql[tokens[^1].Start] == ';' && tokens.Count > 1 ? tokens[^2] : tokens[^1];
            // a DELIMITER script is delimited by its own directives: only the statement count is checked
            if (!entry.Sql.StartsWith("delimiter ", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Equal(tokens[0].Start, spans[0].Start);
                Assert.InRange(spans[0].Start + spans[0].Length, last.End, tokens[^1].End);
            }
        }

        var errors = SqlDiagnosticsProvider.GetDiagnostics(new SqlRequest(entry.Sql, 0, dialect, Snapshot(schema)))
            .Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => $"{d.Code} {d.Message} @{d.Range.Start}").ToList();
        Assert.True(errors.Count == 0, $"{file}#{index}: {entry.Sql}\n{string.Join("\n", errors)}");
    }

    [Theory]
    [MemberData(nameof(InvalidEntries))]
    public void InvalidStatement_IsReportedWithItsCode(string file, int index)
    {
        var (entry, dialect, schema) = Find(file, index, "invalid");
        Assert.NotNull(entry.Expect);
        var codes = SqlDiagnosticsProvider.GetDiagnostics(new SqlRequest(entry.Sql, 0, dialect, Snapshot(schema)))
            .Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.MessageCode).ToList();
        Assert.True(codes.Contains(entry.Expect), $"{file}#{index}: {entry.Sql}\nexpected {entry.Expect}, got [{string.Join(", ", codes)}]");
    }
}
