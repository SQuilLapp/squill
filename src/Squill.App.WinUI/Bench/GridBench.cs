using System.Diagnostics;
using Microsoft.UI.Xaml;
using Squill.App.WinUI.Controls;
using Squill.Presentation.Results;

namespace Squill.App.WinUI.Bench;

/// <summary>The <c>--grid-bench</c> mode: scrolls a 1,000,000 x 12 synthetic grid and prints frame timings.</summary>
internal static class GridBench
{
    private const int Frames = 300;
    private const int RowsPerFrame = 3;

    /// <summary>Gets whether the command line asks for the benchmark.</summary>
    public static bool IsRequested() => Environment.GetCommandLineArgs().Contains("--grid-bench");

    /// <summary>Creates the bench window; the application exits once the frames are measured.</summary>
    public static Window Start()
    {
        var view = new DataGridView { DataSource = new SyntheticGridSource(1_000_000, 12) };
        var window = new Window { Title = "SQuilL grid bench", Content = view };
        var renderMs = new List<double>(Frames);
        var frameMs = new List<double>(Frames);
        var last = 0L;
        view.Canvas.Drawn += (_, _) =>
        {
            var now = Stopwatch.GetTimestamp();
            if (last != 0)
            {
                frameMs.Add(Stopwatch.GetElapsedTime(last, now).TotalMilliseconds);
                renderMs.Add(view.Canvas.LastRenderMs);
            }

            last = now;
            if (renderMs.Count < Frames)
            {
                view.Canvas.ScrollY += RowsPerFrame * view.Canvas.Layout.RowHeight;
            }
            else
            {
                GridBenchReport.Print(renderMs, frameMs);
                Application.Current.Exit();
            }
        };
        return window;
    }
}
