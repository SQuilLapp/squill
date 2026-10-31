using System.Globalization;

namespace Squill.Presentation.Results;

/// <summary>Prints the frame statistics of the <c>--grid-bench</c> mode in the format recorded in <c>docs/poc/grid.md</c>.</summary>
public static class GridBenchReport
{
    /// <summary>Writes the five measurement lines to the console.</summary>
    /// <param name="renderMs">Time spent drawing each frame, in milliseconds.</param>
    /// <param name="frameMs">Wall-clock interval between frames, in milliseconds.</param>
    public static void Print(IReadOnlyList<double> renderMs, IReadOnlyList<double> frameMs)
    {
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"frames {renderMs.Count}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"avg frame ms {renderMs.Average():F3}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"p95 frame ms {Percentile(renderMs, 0.95):F3}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"avg interval ms {frameMs.Average():F3}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"p95 interval ms {Percentile(frameMs, 0.95):F3}"));
    }

    /// <summary>Gets a percentile with the nearest-rank method.</summary>
    /// <param name="values">The samples (not empty).</param>
    /// <param name="p">The fraction, for example 0.95.</param>
    /// <returns>The sample at that rank.</returns>
    public static double Percentile(IReadOnlyList<double> values, double p)
    {
        var sorted = values.Order().ToArray();
        return sorted[(int)Math.Min(sorted.Length - 1, Math.Ceiling(sorted.Length * p) - 1)];
    }
}
