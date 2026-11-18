namespace Squill.Presentation.Compare;

/// <summary>How an aligned row of the side-by-side DDL differs between the two sides.</summary>
public enum LineMark
{
    /// <summary>Both sides have the same line.</summary>
    Same,

    /// <summary>Only the source side has a line; the target side shows a filler.</summary>
    SourceOnly,

    /// <summary>Only the target side has a line; the source side shows a filler.</summary>
    TargetOnly,

    /// <summary>Both sides have a different line at this position.</summary>
    Changed,
}

/// <summary>One row of the aligned side-by-side view.</summary>
/// <param name="SourceNumber">1-based line number on the source side, or <see langword="null"/> for a filler.</param>
/// <param name="SourceText">Source line, or <see langword="null"/> for a filler.</param>
/// <param name="TargetNumber">1-based line number on the target side, or <see langword="null"/> for a filler.</param>
/// <param name="TargetText">Target line, or <see langword="null"/> for a filler.</param>
/// <param name="Mark">How the two sides relate.</param>
public sealed record AlignedLine(int? SourceNumber, string? SourceText, int? TargetNumber, string? TargetText, LineMark Mark);

/// <summary>Line-level diff producing the aligned rows of the two DDL panes.</summary>
public static class LineDiff
{
    private const int MaxLines = 20_000;
    private const int MaxEdits = 4_000;

    /// <summary>Aligns two texts line by line.</summary>
    /// <param name="source">The source side text.</param>
    /// <param name="target">The target side text.</param>
    /// <returns>One row per aligned line; fillers have a <see langword="null"/> number and text. Trailing whitespace and CRLF versus LF are ignored when comparing, the original text is kept.</returns>
    public static IReadOnlyList<AlignedLine> Align(string source, string target)
    {
        var a = Split(source);
        var b = Split(target);
        var ops = a.Length > MaxLines || b.Length > MaxLines ? null : Myers(a, b);
        // shortcut: position-by-position compare past 20,000 lines or 4,000 edits, upgrade when a real case shows up.
        ops ??= Positional(a, b);

        var rows = new List<AlignedLine>(ops.Count);
        int i = 0, j = 0, k = 0;
        while (k < ops.Count)
        {
            if (ops[k] == Op.Equal)
            {
                rows.Add(new AlignedLine(i + 1, a[i], j + 1, b[j], LineMark.Same));
                i++;
                j++;
                k++;
                continue;
            }

            var del = 0;
            var ins = 0;
            for (; k < ops.Count && ops[k] != Op.Equal; k++)
            {
                if (ops[k] == Op.Delete)
                {
                    del++;
                }
                else
                {
                    ins++;
                }
            }

            var pairs = Math.Min(del, ins);
            for (var p = 0; p < pairs; p++)
            {
                rows.Add(new AlignedLine(i + 1, a[i], j + 1, b[j], LineMark.Changed));
                i++;
                j++;
            }

            for (var p = pairs; p < del; p++)
            {
                rows.Add(new AlignedLine(i + 1, a[i], null, null, LineMark.SourceOnly));
                i++;
            }

            for (var p = pairs; p < ins; p++)
            {
                rows.Add(new AlignedLine(null, null, j + 1, b[j], LineMark.TargetOnly));
                j++;
            }
        }

        return rows;
    }

    private enum Op : byte
    {
        Equal,
        Delete,
        Insert,
    }

    private static string[] Split(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        // A final newline does not add an empty line.
        return lines[^1].Length == 0 ? lines[..^1] : lines;
    }

    private static bool Eq(string x, string y) => x.AsSpan().TrimEnd().SequenceEqual(y.AsSpan().TrimEnd());

    private static List<Op> Positional(string[] a, string[] b)
    {
        var ops = new List<Op>();
        var n = Math.Max(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            if (i < a.Length && i < b.Length)
            {
                if (Eq(a[i], b[i]))
                {
                    ops.Add(Op.Equal);
                }
                else
                {
                    ops.Add(Op.Delete);
                    ops.Add(Op.Insert);
                }
            }
            else
            {
                ops.Add(i < a.Length ? Op.Delete : Op.Insert);
            }
        }

        return ops;
    }

    /// <summary>Myers O(ND) shortest edit script; <see langword="null"/> when more than <see cref="MaxEdits"/> edits are needed.</summary>
    private static List<Op>? Myers(string[] a, string[] b)
    {
        int n = a.Length, m = b.Length, max = n + m;
        var offset = max;
        var v = new int[(2 * max) + 2];
        var trace = new List<int[]>();
        var found = false;
        for (var d = 0; d <= max && !found; d++)
        {
            if (d > MaxEdits)
            {
                return null;
            }

            trace.Add((int[])v.Clone());
            for (var diag = -d; diag <= d; diag += 2)
            {
                var x = diag == -d || (diag != d && v[offset + diag - 1] < v[offset + diag + 1])
                    ? v[offset + diag + 1]
                    : v[offset + diag - 1] + 1;
                var y = x - diag;
                while (x < n && y < m && Eq(a[x], b[y]))
                {
                    x++;
                    y++;
                }

                v[offset + diag] = x;
                if (x >= n && y >= m)
                {
                    found = true;
                    break;
                }
            }
        }

        var ops = new List<Op>();
        int cx = n, cy = m;
        for (var d = trace.Count - 1; d >= 0; d--)
        {
            var tv = trace[d];
            var diag = cx - cy;
            var prev = diag == -d || (diag != d && tv[offset + diag - 1] < tv[offset + diag + 1]) ? diag + 1 : diag - 1;
            var px = tv[offset + prev];
            var py = px - prev;
            while (cx > px && cy > py)
            {
                ops.Add(Op.Equal);
                cx--;
                cy--;
            }

            if (d > 0)
            {
                ops.Add(cx == px ? Op.Insert : Op.Delete);
            }

            cx = px;
            cy = py;
        }

        ops.Reverse();
        return ops;
    }
}
