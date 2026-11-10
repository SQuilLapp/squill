using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Squill.Core.Plans;
using Squill.Presentation.Localization;
using Squill.Presentation.Plans;
using Windows.ApplicationModel.DataTransfer;

namespace Squill.App.WinUI.Views;

/// <summary>The node table of a plan (indented operations, relative-cost bars, figures) next to the details panel of the selected node with its warnings and index suggestion.</summary>
internal sealed partial class PlanTable : UserControl
{
    private const double RowHeight = 28;
    private const double Indent = 14;
    private const double BarWidth = 96;
    private const double DetailsWidth = 264;

    private static readonly string[] IndexKeywords = ["CREATE", "UNIQUE", "NONCLUSTERED", "CLUSTERED", "INDEX", "ON", "INCLUDE", "WITH"];

    private readonly Border _header = new() { Height = RowHeight };
    private readonly StackPanel _rows = new();
    private readonly ScrollViewer _details = new() { Width = DetailsWidth, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly List<Border> _rowElements = [];
    private PlanViewModel? _plan;
    private PlanTabViewModel? _tab;
    private ElementTheme _theme;

    /// <summary>Creates the table; <see cref="Bind"/> fills it.</summary>
    public PlanTable()
    {
        var body = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        var table = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition() } };
        table.Children.Add(_header);
        var scroll = new ScrollViewer { Content = _rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 1);
        table.Children.Add(scroll);
        body.Children.Add(table);
        var panel = new Border { Child = _details, BorderThickness = new Thickness(1, 0, 0, 0) };
        panel.Background = (Brush)Application.Current.Resources["SquillBgSurfaceBrush"];
        panel.BorderBrush = (Brush)Application.Current.Resources["SquillBorderBrush"];
        Grid.SetColumn(panel, 1);
        body.Children.Add(panel);
        Content = body;
    }

    /// <summary>Shows a plan; calling it again with the same plan and theme only moves the selection.</summary>
    /// <param name="tab">The plan tab, which receives the open-in-editor request.</param>
    /// <param name="plan">The plan to show.</param>
    public void Bind(PlanTabViewModel tab, PlanViewModel plan)
    {
        _tab = tab;
        if (!ReferenceEquals(plan, _plan) || ActualTheme != _theme)
        {
            _plan = plan;
            _theme = ActualTheme;
            BuildTable(plan);
        }

        UpdateSelection();
    }

    private static TextBlock Text(string text, Brush brush, bool mono = false, bool right = false, bool semibold = false)
    {
        var block = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap };
        block.Foreground = brush;
        if (mono)
            block.FontFamily = (FontFamily)Application.Current.Resources["SquillFontMono"];
        if (right)
            block.HorizontalAlignment = HorizontalAlignment.Right;
        if (semibold)
            block.FontWeight = FontWeights.SemiBold;
        return block;
    }

    private static Brush SeverityBrush(FrameworkElement owner, PlanSeverity severity) =>
        Tokens.Brush(owner, severity switch { PlanSeverity.Critical => "SquillDangerBrush", PlanSeverity.Alert => "SquillWarningBrush", _ => "SquillFg3Brush" });

    private void BuildTable(PlanViewModel plan)
    {
        var rows = plan.Rows;
        bool Any(Func<PlanNode, bool> has) => rows.Any(r => has(r.Node));
        var shown = new[]
        {
            true,
            rows.Any(r => r.RelativeShare is not null),
            Any(n => n.EstimatedCost is not null),
            Any(n => n.EstimatedRows is not null),
            Any(n => n.ActualRows is not null),
            Any(n => n.ActualTimeMs is not null),
            Any(n => n.Loops is not null),
        };
        var widths = new double[] { 0, 150, 90, 100, 110, 80, 70 };
        Grid NewGrid()
        {
            var grid = new Grid { Padding = new Thickness(12, 0, 12, 0) };
            for (var c = 0; c < shown.Length; c++)
            {
                if (shown[c])
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = c == 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(widths[c]), MinWidth = c == 0 ? 220 : 0 });
            }

            return grid;
        }

        void Place(Grid grid, int column, FrameworkElement cell)
        {
            if (!shown[column])
                return;
            Grid.SetColumn(cell, shown[..column].Count(s => s));
            cell.Margin = new Thickness(column == 0 ? 0 : 8, 0, 0, 0);
            grid.Children.Add(cell);
        }

        var muted = Tokens.Brush(this, "SquillFg3Brush");
        var normal = Tokens.Brush(this, "SquillFgBrush");
        var warning = Tokens.Brush(this, "SquillWarningBrush");
        var track = Tokens.Brush(this, "SquillBgActiveBrush");
        var capsStyle = (Style)Application.Current.Resources["CapsTextBlockStyle"];

        var headerGrid = NewGrid();
        string[] keys = ["plan.col.node", "plan.col.relative", "plan.col.cost", "plan.col.estRows", "plan.col.actRows", "plan.col.time", "plan.col.loops"];
        for (var c = 0; c < keys.Length; c++)
        {
            var head = Text(UiText.Get(keys[c]), muted, right: c > 1);
            head.Style = capsStyle;
            head.Foreground = muted;
            Place(headerGrid, c, head);
        }

        _header.Child = headerGrid;
        _header.BorderThickness = new Thickness(0, 0, 0, 1);
        _header.BorderBrush = Tokens.Brush(this, "SquillBorderSubtleBrush");

        _rows.Children.Clear();
        _rowElements.Clear();
        foreach (var row in rows)
        {
            var ink = row.IsHelper ? muted : normal;
            var grid = NewGrid();
            var node = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(row.Depth * Indent, 0, 0, 0) };
            node.Children.Add(Text(row.Label, ink, semibold: !row.IsHelper));
            if (row.Target is { } target)
                node.Children.Add(Text(target, muted, mono: true));
            Place(grid, 0, node);

            if (row.RelativeShare is { } share)
            {
                var severity = SeverityBrush(this, row.Severity);
                var bar = new Grid { Width = BarWidth, Height = 4, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, CornerRadius = new CornerRadius(2) };
                bar.Background = track;
                var fill = new Border { Width = BarWidth * Math.Clamp(share, 0, 1), HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(2) };
                fill.Background = severity;
                bar.Children.Add(fill);
                var relative = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                relative.Children.Add(bar);
                relative.Children.Add(Text(row.RelativeText, ink, mono: true, right: true));
                Place(grid, 1, relative);
            }

            Place(grid, 2, Text(row.TotalCost, ink, mono: true, right: true));
            Place(grid, 3, Text(row.EstimatedRows, ink, mono: true, right: true));
            Place(grid, 4, Text(row.ActualRows, row.MisestimateText.Length > 0 ? warning : ink, mono: true, right: true));
            Place(grid, 5, Text(row.Time, ink, mono: true, right: true));
            Place(grid, 6, Text(row.Loops, ink, mono: true, right: true));

            var element = new Border { Height = RowHeight, Child = grid };
            var captured = row;
            element.Tapped += (_, _) => plan.SelectedRow = captured;
            _rows.Children.Add(element);
            _rowElements.Add(element);
        }
    }

    private void UpdateSelection()
    {
        if (_plan is not { } plan)
            return;
        var selection = Tokens.Brush(this, "SquillSelectionBrush");
        for (var i = 0; i < _rowElements.Count; i++)
            _rowElements[i].Background = ReferenceEquals(plan.Rows[i], plan.SelectedRow) ? selection : null;
        var index = IndexOfSelected(plan);
        if (index >= 0)
            _rowElements[index].StartBringIntoView();
        BuildDetails(plan);
    }

    private static int IndexOfSelected(PlanViewModel plan)
    {
        for (var i = 0; i < plan.Rows.Count; i++)
        {
            if (ReferenceEquals(plan.Rows[i], plan.SelectedRow))
                return i;
        }

        return -1;
    }

    private void BuildDetails(PlanViewModel plan)
    {
        var details = plan.Details;
        var row = plan.SelectedRow;
        var muted = Tokens.Brush(this, "SquillFg2Brush");
        var normal = Tokens.Brush(this, "SquillFgBrush");
        var warning = Tokens.Brush(this, "SquillWarningBrush");
        var severity = SeverityBrush(this, row.Severity);

        var stack = new StackPanel { Spacing = 14, Padding = new Thickness(14) };

        var title = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } }, ColumnSpacing = 8 };
        if (row.Severity != PlanSeverity.Normal)
        {
            var square = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center };
            square.Background = severity;
            title.Children.Add(square);
        }

        var name = Text(details.Title, normal, semibold: true);
        name.TextWrapping = TextWrapping.Wrap;
        Grid.SetColumn(name, 1);
        title.Children.Add(name);
        var percent = Text(row.RelativeText, severity, mono: true, right: true);
        Grid.SetColumn(percent, 2);
        if (row.RelativeShare is not null)
            title.Children.Add(percent);
        stack.Children.Add(title);

        if (details.ShareText.Length > 0)
        {
            var share = Text(details.ShareText, muted);
            share.TextWrapping = TextWrapping.Wrap;
            stack.Children.Add(share);
        }

        if (details.Properties.Count > 0)
        {
            var list = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(96) }, new ColumnDefinition() }, ColumnSpacing = 10, RowSpacing = 6 };
            for (var i = 0; i < details.Properties.Count; i++)
            {
                list.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var label = Text(details.Properties[i].Name, muted);
                label.TextWrapping = TextWrapping.Wrap;
                label.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetRow(label, i);
                var value = Text(details.Properties[i].Value, normal, mono: true, right: true);
                value.TextWrapping = TextWrapping.Wrap;
                value.TextAlignment = TextAlignment.Right;
                value.VerticalAlignment = VerticalAlignment.Top;
                value.IsTextSelectionEnabled = true;
                Grid.SetRow(value, i);
                Grid.SetColumn(value, 1);
                list.Children.Add(label);
                list.Children.Add(value);
            }

            stack.Children.Add(list);
        }

        foreach (var text in details.Warnings)
        {
            var line = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition() }, ColumnSpacing = 8, Padding = new Thickness(10, 8, 10, 8), CornerRadius = new CornerRadius(6) };
            line.Background = Tokens.Brush(this, "SquillWarningSoftBrush");
            var icon = Icons.Create("SquillIconWarning", 14, warning);
            icon.VerticalAlignment = VerticalAlignment.Top;
            line.Children.Add(icon);
            var message = Text(text, normal);
            message.TextWrapping = TextWrapping.Wrap;
            Grid.SetColumn(message, 1);
            line.Children.Add(message);
            stack.Children.Add(line);
        }

        if (details.HasSuggestions)
            stack.Children.Add(BuildSuggestion(details, muted, normal));

        _details.Content = stack;
    }

    private Border BuildSuggestion(PlanDetailsViewModel details, Brush muted, Brush normal)
    {
        var card = new StackPanel { Spacing = 10 };
        card.Children.Add(Text(UiText.Get("plan.suggestion.title"), normal, semibold: true));

        var keyword = Tokens.Brush(this, "SquillSynKeywordBrush");
        foreach (var suggestion in details.Suggestions)
        {
            var code = new TextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, FontFamily = (FontFamily)Application.Current.Resources["SquillFontMono"] };
            code.Foreground = normal;
            var words = suggestion.Text.Split(' ');
            for (var i = 0; i < words.Length; i++)
            {
                var run = new Run { Text = i == 0 ? words[i] : " " + words[i] };
                if (IndexKeywords.Contains(words[i], StringComparer.OrdinalIgnoreCase))
                    run.Foreground = keyword;
                code.Inlines.Add(run);
            }

            var block = new Border { Padding = new Thickness(10), CornerRadius = new CornerRadius(6), Child = code };
            block.Background = Tokens.Brush(this, "SquillBgBaseBrush");
            card.Children.Add(block);
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var copy = new Button { Content = UiText.Get("plan.copy"), Style = (Style)Application.Current.Resources["GhostButtonStyle"] };
        copy.Click += (_, _) =>
        {
            var data = new DataPackage();
            data.SetText(details.CopyText);
            Clipboard.SetContent(data);
        };
        var open = new Button { Content = UiText.Get("plan.openineditor"), Style = (Style)Application.Current.Resources["GhostButtonStyle"] };
        open.Click += (_, _) => _tab?.OpenInEditorCommand.Execute(details.CopyText);
        buttons.Children.Add(copy);
        buttons.Children.Add(open);
        card.Children.Add(buttons);

        var border = new Border { Padding = new Thickness(12), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Child = card };
        border.Background = Tokens.Brush(this, "SquillAccentSoftBrush");
        border.BorderBrush = Tokens.Brush(this, "SquillAccentLineBrush");
        return border;
    }
}
