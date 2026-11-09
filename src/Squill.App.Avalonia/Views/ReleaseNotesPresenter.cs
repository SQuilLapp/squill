using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Squill.Presentation.Updates;

namespace Squill.App.Avalonia.Views;

/// <summary>Turns parsed release notes into selectable text elements, with theme brushes read from the host control.</summary>
internal static class ReleaseNotesPresenter
{
    private const string FgKey = "SquillFgBrush";
    private const string Fg2Key = "SquillFg2Brush";
    private const string AccentKey = "SquillAccentBrush";

    /// <summary>Builds the elements of the notes.</summary>
    /// <param name="blocks">The parsed notes.</param>
    /// <param name="host">The control the result is shown in; its theme decides the colors and its launcher opens links.</param>
    /// <returns>A vertical stack with one child per block.</returns>
    public static StackPanel Build(IReadOnlyList<NotesBlock> blocks, Control host)
    {
        var stack = new StackPanel { Spacing = 6 };
        foreach (var block in blocks)
            stack.Children.Add(BuildBlock(block, host));
        return stack;
    }

    private static Control BuildBlock(NotesBlock block, Control host)
    {
        switch (block)
        {
            case NotesHeading heading:
                return new SelectableTextBlock
                {
                    Text = heading.Text,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brush(host, FgKey),
                    Margin = new Thickness(0, 6, 0, 0),
                };
            case NotesBullet bullet:
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
                row.Children.Add(new TextBlock { Text = ReleaseNotesFormat.BulletGlyph, Foreground = Brush(host, Fg2Key) });
                var text = Runs(bullet.Runs, host);
                Grid.SetColumn(text, 1);
                row.Children.Add(text);
                return row;
            case NotesParagraph paragraph:
                return Runs(paragraph.Runs, host);
            default:
                return new TextBlock();
        }
    }

    private static SelectableTextBlock Runs(IReadOnlyList<NotesRun> runs, Control host)
    {
        var block = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brush(host, Fg2Key) };
        foreach (var run in runs)
            block.Inlines!.Add(BuildRun(run, host));
        return block;
    }

    private static Inline BuildRun(NotesRun run, Control host)
    {
        switch (run.Style)
        {
            case NotesRunStyle.Bold:
                return new Run { Text = run.Text, FontWeight = FontWeight.SemiBold };
            case NotesRunStyle.Code:
                return new Run { Text = run.Text, FontFamily = Mono(host), Foreground = Brush(host, FgKey) };
            case NotesRunStyle.Link when ReleaseNotesFormat.LinkTarget(run) is { } target:
                var link = new Button
                {
                    Content = run.Text,
                    Padding = new Thickness(0),
                    MinHeight = 0,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = Brush(host, AccentKey),
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                link.Click += async (_, _) =>
                {
                    if (TopLevel.GetTopLevel(host)?.Launcher is { } launcher)
                        await launcher.LaunchUriAsync(target);
                };
                return new InlineUIContainer(link);
            default:
                return new Run { Text = run.Text };
        }
    }

    private static FontFamily Mono(Control host) =>
        host.TryFindResource("SquillFontMono", host.ActualThemeVariant, out var value) && value is FontFamily family ? family : FontFamily.Default;

    private static IBrush? Brush(Control host, string key) =>
        host.TryFindResource(key, host.ActualThemeVariant, out var value) ? value as IBrush : null;
}
