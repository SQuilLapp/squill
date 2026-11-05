using Squill.Core;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.Data.Tests.Scenarios;

/// <summary>Reads the single row of a query and records, per column, the CLR type the driver returned and the text <see cref="CellFormatter"/> shows for it.</summary>
public static class ColumnTypeScenario
{
    /// <summary>Executes <paramref name="select"/> (one row expected) and returns <c>name -> (CLR type name, invariant text, display text)</c>.</summary>
    public static async Task<Dictionary<string, (string Clr, string? Text, string? Display)>> ReadAsync(IDatabaseConnection connection, string select, List<CapturedColumn>? capture = null)
    {
        await using var reader = await connection.ExecuteAsync(select, [], CancellationToken.None);
        var rows = Assert.IsType<object?[][]>(await reader.ReadBatchAsync(10, CancellationToken.None));
        var row = Assert.Single(rows);
        var result = new Dictionary<string, (string, string?, string?)>();
        for (var i = 0; i < reader.Columns.Count; i++)
        {
            var column = reader.Columns[i];
            if (capture is not null && ExportSnapshotScenario.Capture(column, row[i]) is { } captured)
                capture.Add(captured);
            result[column.Name] = (row[i]?.GetType().Name ?? "null", CellFormatter.Format(row[i], column), CellFormatter.FormatDisplay(row[i], column));
        }
        return result;
    }

    /// <summary>One <c>name|CLR type|invariant text</c> line per column, the form the doc table is written from.</summary>
    public static string Dump(Dictionary<string, (string Clr, string? Text, string? Display)> row) =>
        string.Join('\n', row.Select(p => $"{p.Key}|{p.Value.Clr}|{p.Value.Text}"));
}
