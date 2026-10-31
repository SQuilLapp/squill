using Squill.Core;

namespace Squill.Presentation.Results;

/// <summary>Generated, read-only <see cref="IGridDataSource"/> (ints, strings and dates with 10% NULL) used by the <c>--grid-bench</c> mode of the heads.</summary>
public sealed class SyntheticGridSource : IGridDataSource
{
    private static readonly DateTime Epoch = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly ColumnInfo[] _columns;

    /// <summary>Initializes a new instance of the <see cref="SyntheticGridSource"/> class.</summary>
    /// <param name="rows">Number of rows.</param>
    /// <param name="columns">Number of columns, cycling through int, string and date.</param>
    public SyntheticGridSource(long rows, int columns)
    {
        RowCount = rows;
        _columns = new ColumnInfo[columns];
        for (var i = 0; i < columns; i++)
        {
            var clr = (i % 3) switch { 0 => typeof(int), 1 => typeof(string), _ => typeof(DateTime) };
            _columns[i] = new ColumnInfo($"column_{i}", clr.Name, clr, true, i);
        }
    }

    /// <inheritdoc />
    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public IReadOnlyList<ColumnInfo> Columns => _columns;

    /// <inheritdoc />
    public long RowCount { get; }

    /// <inheritdoc />
    public bool IsComplete => true;

    /// <inheritdoc />
    public bool IsTruncated => false;

    /// <inheritdoc />
    public object? GetCell(long row, int column)
    {
        if (row < 0 || row >= RowCount || ((row * 31) + column) % 10 == 0)
        {
            return null;
        }

        return (column % 3) switch
        {
            0 => (int)(row * (column + 1) % int.MaxValue),
            1 => $"value {row}-{column}",
            _ => Epoch.AddMinutes(row),
        };
    }
}
