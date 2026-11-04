using Squill.Data;

namespace Squill.Data.RoundTrip;

/// <summary>Reads every result set of a statement; shared by every driver test project.</summary>
internal static class MultiResultScenario
{
    /// <summary>Runs <paramref name="sql"/> and returns the rows of each result set with their column names, the messages and the affected count.</summary>
    public static async Task<(List<(string[] Columns, List<object?[]> Rows)> Sets, string[] Messages, long Affected)> ReadAllSetsAsync(IDatabaseConnection connection, string sql)
    {
        await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
        var sets = new List<(string[], List<object?[]>)>();
        do
        {
            var columns = reader.Columns.Select(c => c.Name).ToArray();
            sets.Add((columns, await reader.ReadAllAsync(10, CancellationToken.None)));
        }
        while (await reader.NextResultAsync(CancellationToken.None));
        return (sets, [.. reader.Messages], reader.RecordsAffected);
    }

    /// <summary>Runs a statement without side effects, ignoring its result.</summary>
    public static async Task RunAsync(IDatabaseConnection connection, string sql)
    {
        await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
        do
        {
            await reader.ReadAllAsync(10, CancellationToken.None);
        }
        while (await reader.NextResultAsync(CancellationToken.None));
    }
}
