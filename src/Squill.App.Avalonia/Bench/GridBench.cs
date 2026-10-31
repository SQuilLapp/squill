using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Squill.App.Avalonia.Controls;
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
        var view = new DataGridView { DataSource = new SyntheticGridSource(1_000_000, 12) };
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
                GridBenchReport.Print(renderMs, frameMs);
                desktop.Shutdown();
            }
        }
    }
}
