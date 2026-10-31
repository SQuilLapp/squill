#if SQUILL_LOOMEDITOR
using System.Diagnostics;
using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Squill.App.WinUI.Editors;
using Squill.Core;
using Squill.Presentation.Editor;
using Squill.Sql.Completion;

namespace Squill.App.WinUI.Bench;

/// <summary>The <c>--editor-demo</c> mode: opens LoomEditor on a 100,000-line script and prints paint, highlighting, completion and UI stall times.</summary>
internal static class EditorDemo
{
    private const int Lines = 100_000;
    private const int HeartbeatMs = 16;
    private const int ScrollJumps = 50;

    /// <summary>Gets whether the command line asks for the demo.</summary>
    public static bool IsRequested() => Environment.GetCommandLineArgs().Contains("--editor-demo");

    /// <summary>Creates the demo window; the application exits once everything is measured.</summary>
    public static Window Start()
    {
        var editor = new LoomSqlEditor();
        editor.SetDialect(DatabaseDialect.PostgreSql);
        editor.SetCompletionProvider(new CompositeCompletionProvider(new KeywordCompletionProvider(), new SchemaCompletionProvider()));
        var window = new Window { Title = "SQuilL editor demo", Content = editor.Control };
        var queue = DispatcherQueue.GetForCurrentThread();

        var script = SqlDemoScript.Generate(Lines);
        var start = Stopwatch.GetTimestamp();
        var firstPaintMs = -1.0;
        var maxGapMs = 0.0;
        var last = start;
        var stalls = new List<string>();
        var heartbeat = queue.CreateTimer();
        heartbeat.Interval = TimeSpan.FromMilliseconds(HeartbeatMs);
        heartbeat.Tick += (_, _) =>
        {
            var now = Stopwatch.GetTimestamp();
            var gap = Stopwatch.GetElapsedTime(last, now).TotalMilliseconds;
            maxGapMs = Math.Max(maxGapMs, gap);
            if (gap > 50)
            {
                stalls.Add($"{gap:F0} ms at {Stopwatch.GetElapsedTime(start, now).TotalMilliseconds:F0}");
            }
            last = now;
        };

        void OnRendering(object? sender, object e)
        {
            CompositionTarget.Rendering -= OnRendering;
            firstPaintMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }

        editor.HighlightApplied += (_, _) => _ = FinishAsync(editor, start, () => firstPaintMs, () => maxGapMs, heartbeat, stalls);
        CompositionTarget.Rendering += OnRendering;
        editor.Text = script;
        Print($"text load ms {Stopwatch.GetElapsedTime(start).TotalMilliseconds:F1}");
        heartbeat.Start();
        return window;
    }

    private static async Task FinishAsync(LoomSqlEditor editor, long start, Func<double> firstPaint, Func<double> maxGap, DispatcherQueueTimer heartbeat, List<string> stalls)
    {
        var highlightMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        for (var jump = 1; jump <= ScrollJumps; jump++)
        {
            editor.ScrollToLine(jump * (Lines / ScrollJumps));
            await Task.Delay(40);
        }

        var text = editor.Text;
        editor.CaretOffset = text.IndexOf("FROM t", text.Length / 2, StringComparison.Ordinal) + "FROM t".Length;
        var requested = Stopwatch.GetTimestamp();
        var items = await editor.RequestCompletionAsync();
        var completionMs = Stopwatch.GetElapsedTime(requested).TotalMilliseconds;
        heartbeat.Stop();
        Print($"lines {Lines}, scroll jumps {ScrollJumps}");
        Print($"time to first paint ms {firstPaint():F1}");
        Print($"time to highlighting applied ms {highlightMs:F1}");
        Print($"completion items {items}");
        Print($"completion latency ms {completionMs:F1} (adapter {editor.LastCompletionMs:F1})");
        Print($"max UI heartbeat gap ms {maxGap():F1} (interval {HeartbeatMs})");
        Print($"stalls over 50 ms: {string.Join("; ", stalls)}");
        Application.Current.Exit();
    }

    private static void Print(FormattableString line) => Console.WriteLine(line.ToString(CultureInfo.InvariantCulture));
}
#endif
