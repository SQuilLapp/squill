namespace Squill.Data;

/// <summary>Helpers over <see cref="IResultReader"/>.</summary>
public static class ResultReaderExtensions
{
    /// <summary>Reads every remaining row, <paramref name="batchSize"/> at a time.</summary>
    public static async ValueTask<List<object?[]>> ReadAllAsync(this IResultReader stream, int batchSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var rows = new List<object?[]>();
        while (await stream.ReadBatchAsync(batchSize, cancellationToken) is { } batch)
            rows.AddRange(batch);
        return rows;
    }
}
