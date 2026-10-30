using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Squill.App.Avalonia.Controls;
using Squill.Core;
using Squill.Presentation.Results;

namespace Squill.App.Avalonia.Bench;

/// <summary>The <c>--grid-bench</c> mode: scrolls a 1,000,000 x 12 synthetic grid and prints frame timings.</summary>
internal static class GridBench
{
    private const int Frames = 300;
    private const int RowsPerFrame = 3;

    /// <summary>Gets whether the command line asks for the benchmark.</summary>
    public static bool IsRequested(string[]? args) => args is not null && args.Contains("--grid-bench");

    /// <summary>Opens the bench window and exits the application once the frames are measured.</summary>
    public static Window Start(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var view = new DataGridView { DataSource = new SyntheticSource(1_000_000, 12) };
        var window = new Window { Width = 1280, Height = 720, Title = "SQuilL grid bench", Content = view };
        var renderMs = new List<double>(Frames);
        var frameMs = new List<double>(Frames);
        var last = 0L;
        window.Opened += (_, _) => Dispatcher.UIThread.Post(Step, DispatcherPriority.Background);
        return window;

        void Step()
        {
            var begin = Stopwatch.GetTimestamp();
            view.Canvas.ScrollY += RowsPerFrame * view.Canvas.Layout.RowHeight;
            if (renderMs.Count < Frames)
            {
                if (last != 0)
                {
                    frameMs.Add(Stopwatch.GetElapsedTime(last, begin).TotalMilliseconds);
                }

                last = begin;
                if (TopLevel.GetTopLevel(view) is { } top)
                {
                    top.RequestAnimationFrame(_ =>
                    {
                        renderMs.Add(view.Canvas.LastRenderMs);
                        Step();
                    });
                }
            }
            else
            {
                Report(renderMs, frameMs);
                desktop.Shutdown();
            }
        }
    }

    private static void Report(List<double> renderMs, List<double> frameMs)
    {
        Console.WriteLine($"frames {renderMs.Count}");
        Console.WriteLine($"avg frame ms {renderMs.Average():F3}");
        Console.WriteLine($"p95 frame ms {Percentile(renderMs, 0.95):F3}");
        Console.WriteLine($"avg interval ms {frameMs.Average():F3}");
        Console.WriteLine($"p95 interval ms {Percentile(frameMs, 0.95):F3}");
    }

    private static double Percentile(List<double> values, double p)
    {
        var sorted = values.Order().ToArray();
        return sorted[(int)Math.Min(sorted.Length - 1, Math.Ceiling(sorted.Length * p) - 1)];
    }

    private sealed class SyntheticSource : IGridDataSource
    {
        private static readonly DateTime Epoch = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly ColumnInfo[] _columns;

        public SyntheticSource(long rows, int columns)
        {
            RowCount = rows;
            _columns = new ColumnInfo[columns];
            for (var i = 0; i < columns; i++)
            {
                var clr = (i % 3) switch { 0 => typeof(int), 1 => typeof(string), _ => typeof(DateTime) };
                _columns[i] = new ColumnInfo($"column_{i}", clr.Name, clr, true, i);
            }
        }

        public IReadOnlyList<ColumnInfo> Columns => _columns;

        public long RowCount { get; }

        public bool IsComplete => true;

        public bool IsTruncated => false;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

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
}
