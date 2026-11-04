using Squill.Core;
using Squill.Core.Editing;
using Squill.Presentation.Results;
using Squill.Sql.Ddl;
using Squill.Sql.Editing;
using Xunit;

namespace Squill.Data.Tests.Scenarios;

/// <summary>Types text into every editable column the way the grid does, writes it with <see cref="DmlGenerator"/> and checks the value read back shows the same text.</summary>
public static class ColumnTypeEditScenario
{
    /// <summary>Round-trips each case through the parser, an INSERT and a read back.</summary>
    /// <param name="connection">Open connection holding the sample schema.</param>
    /// <param name="dialect">The engine of the connection.</param>
    /// <param name="schema">Schema of <c>all_types</c>, or <c>null</c>.</param>
    /// <param name="cases">Column name, declared type and the text typed (also the canonical text expected back).</param>
    public static async Task RunAsync(IDatabaseConnection connection, DatabaseDialect dialect, string? schema, params (string Column, string DataType, string Text)[] cases)
    {
        var table = SqlQuoting.QualifiedName(schema, "all_types", dialect);
        foreach (var (column, dataType, text) in cases)
        {
            await RunAsync(connection, $"delete from {table}");
            await using var meta = await connection.ExecuteAsync($"select {SqlQuoting.Quote(column, dialect)} from {table}", [], CancellationToken.None);
            var info = meta.Columns[0];
            Assert.True(CellValueParser.TryParse(text, info, out var value, out var error), $"{column}: {error}");

            var def = new ColumnDefinition(column, dataType, true, false, null);
            var target = new EditTarget(schema, "all_types", [new EditColumn(0, def)], [], new TableInfo(schema ?? "", "all_types", TableKind.Table, [def], [], []));
            await RunAsync(connection, $"delete from {table}");
            var statement = DmlGenerator.Generate(new InsertRow([new ColumnValue(column, value)]), target, dialect);
            await using (var insert = await connection.ExecuteAsync(statement.Sql, [.. statement.Parameters.Select(p => new QueryParameter(p.Name, p.Value))], CancellationToken.None))
                await insert.ReadAllAsync(10, CancellationToken.None);

            await using var read = await connection.ExecuteAsync($"select {SqlQuoting.Quote(column, dialect)} from {table}", [], CancellationToken.None);
            var row = Assert.Single(await read.ReadAllAsync(10, CancellationToken.None));
            Assert.Equal(text, CellFormatter.Format(row[0], read.Columns[0]));
        }
    }

    private static async Task RunAsync(IDatabaseConnection connection, string sql)
    {
        await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
        await reader.ReadAllAsync(10, CancellationToken.None);
    }
}
