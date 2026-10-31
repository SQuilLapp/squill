using System.Globalization;
using System.Text;

namespace Squill.Presentation.Editor;

/// <summary>Generates large, valid SQL scripts used to measure editors.</summary>
public static class SqlDemoScript
{
    /// <summary>Builds a script with one statement per line, mixing keywords, strings, numbers, functions and comments.</summary>
    /// <param name="lineCount">The number of lines, at least 1.</param>
    /// <returns>The script, lines separated by <c>\n</c>, without a trailing line break.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lineCount"/> is less than 1.</exception>
    public static string Generate(int lineCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lineCount, 1);
        var sb = new StringBuilder(lineCount * 72);
        for (var i = 0; i < lineCount; i++)
        {
            if (i > 0)
            {
                sb.Append('\n');
            }

            sb.Append(CultureInfo.InvariantCulture, $"SELECT id, UPPER(name) AS n{i}, {i} * 2 FROM t{i % 50} WHERE kind = 'k{i % 7}' AND score > {i % 100}; -- row {i}");
        }

        return sb.ToString();
    }
}
