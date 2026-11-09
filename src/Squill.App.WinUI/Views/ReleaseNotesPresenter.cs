using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Squill.Presentation.Updates;
using Windows.System;

namespace Squill.App.WinUI.Views;

/// <summary>Turns parsed release notes into text elements, with theme brushes read from the host element.</summary>
internal static class ReleaseNotesPresenter
{
    /// <summary>Builds the elements of the notes.</summary>
    /// <param name="blocks">The parsed notes.</param>
    /// <param name="host">The element the result is shown in; its theme decides the colors.</param>
    /// <returns>A vertical stack with one child per block.</returns>
    public static StackPanel Build(IReadOnlyList<NotesBlock> blocks, FrameworkElement host)
    {
        var stack = new StackPanel { Spacing = 6 };
        foreach (var block in blocks)
            stack.Children.Add(BuildBlock(block, host));
        return stack;
    }

    private static UIElement BuildBlock(NotesBlock block, FrameworkElement host)
    {
        switch (block)
        {
            case NotesHeading heading:
                return new TextBlock
                {
                    Text = heading.Text,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Tokens.Brush(host, "SquillFgBrush"),
                    Margin = new Thickness(0, 6, 0, 0),
                };
            case NotesBullet bullet:
                var row = new Grid { ColumnSpacing = 8 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                var glyph = new TextBlock { Text = ReleaseNotesFormat.BulletGlyph };
                glyph.Foreground = Tokens.Brush(host, "SquillFg2Brush");
                row.Children.Add(glyph);
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

    private static RichTextBlock Runs(IReadOnlyList<NotesRun> runs, FrameworkElement host)
    {
        var paragraph = new Paragraph();
        foreach (var run in runs)
            paragraph.Inlines.Add(BuildRun(run, host));
        var block = new RichTextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Tokens.Brush(host, "SquillFg2Brush") };
        block.Blocks.Add(paragraph);
        return block;
    }

    private static Inline BuildRun(NotesRun run, FrameworkElement host)
    {
        switch (run.Style)
        {
            case NotesRunStyle.Bold:
                return new Run { Text = run.Text, FontWeight = FontWeights.SemiBold };
            case NotesRunStyle.Code:
                var code = new Run { Text = run.Text };
                code.FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SquillFontMono"];
                code.Foreground = Tokens.Brush(host, "SquillFgBrush");
                return code;
            case NotesRunStyle.Link when ReleaseNotesFormat.LinkTarget(run) is { } target:
                var link = new Hyperlink();
                link.Inlines.Add(new Run { Text = run.Text });
                link.Click += async (_, _) => await Launcher.LaunchUriAsync(target);
                return link;
            default:
                return new Run { Text = run.Text };
        }
    }
}
