using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input.Platform;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Squill.Core.Plans;
using Squill.Presentation.Localization;
using Squill.Presentation.Plans;

namespace Squill.App.Avalonia.Views;

/// <summary>The node table of a plan (indented operations, relative-cost bars, figures) next to the details panel of the selected node with its warnings and index suggestion.</summary>
internal sealed class PlanTable : UserControl
{
    private const double RowHeight = 28;
    private const double Indent = 14;
    private const double BarWidth = 96;
    private const double DetailsWidth = 264;

    private static readonly string[] IndexKeywords = ["CREATE", "UNIQUE", "NONCLUSTERED", "CLUSTERED", "INDEX", "ON", "INCLUDE", "WITH"];

    private readonly Border _header = new() { Height = RowHeight };
    private readonly StackPanel _rows = new();
    private readonly ScrollViewer _details = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly ScrollViewer _rowScroll;
    private readonly List<Border> _rowElements = [];
    private readonly Border _panel;
    private PlanViewModel? _plan;
    private PlanTabViewModel? _tab;
    private ThemeVariant? _theme;

    /// <summary>Creates the table; <see cref="Bind"/> fills it.</summary>
    public PlanTable()
    {
        var body = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition(GridLength.Auto) } };
        var table = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition() } };
        table.Children.Add(_header);
        _rowScroll = new ScrollViewer
        {
            Content = _rows,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        Grid.SetRow(_rowScroll, 1);
        table.Children.Add(_rowScroll);
        body.Children.Add(table);
        var panel = new Border { Child = _details, BorderThickness = new Thickness(1, 0, 0, 0), Width = DetailsWidth };
        _panel = panel;
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
        if (!ReferenceEquals(plan, _plan) || ActualThemeVariant != _theme)
        {
            _plan = plan;
            _theme = ActualThemeVariant;
            BuildTable(plan);
        }

        UpdateSelection();
    }

    private IBrush Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;

    private FontFamily Mono =>
        this.TryFindResource("SquillFontMono", ActualThemeVariant, out var value) && value is FontFamily family ? family : FontFamily.Default;

    private IBrush SeverityBrush(PlanSeverity severity) =>
        Brush(severity switch { PlanSeverity.Critical => "SquillDangerBrush", PlanSeverity.Alert => "SquillWarningBrush", _ => "SquillFg3Brush" });

    private TextBlock Text(string text, IBrush brush, bool mono = false, bool right = false, bool semibold = false)
    {
        var block = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap, Foreground = brush };
        if (mono)
            block.FontFamily = Mono;
        if (right)
            block.HorizontalAlignment = HorizontalAlignment.Right;
        if (semibold)
            block.FontWeight = FontWeight.SemiBold;
        return block;
    }

    private void BuildTable(PlanViewModel plan)
    {
        var rows = plan.Rows;
        var shown = plan.ColumnsShown;
        var widths = new double[] { 0, 150, 90, 100, 110, 80, 70 };
        Grid NewGrid()
        {
            var grid = new Grid { Margin = new Thickness(12, 0, 12, 0) };
            for (var c = 0; c < shown.Count; c++)
            {
                if (shown[c])
                    grid.ColumnDefinitions.Add(c == 0 ? new ColumnDefinition(1, GridUnitType.Star) { MinWidth = 220 } : new ColumnDefinition(widths[c], GridUnitType.Pixel));
            }

            return grid;
        }

        void Place(Grid grid, int column, Control cell)
        {
            if (!shown[column])
                return;
            Grid.SetColumn(cell, shown.Take(column).Count(s => s));
            cell.Margin = new Thickness(column == 0 ? 0 : 8, 0, 0, 0);
            grid.Children.Add(cell);
        }

        _panel.Background = Brush("SquillBgSurfaceBrush");
        _panel.BorderBrush = Brush("SquillBorderBrush");
        var muted = Brush("SquillFg3Brush");
        var normal = Brush("SquillFgBrush");
        var warning = Brush("SquillWarningBrush");
        var track = Brush("SquillBgActiveBrush");

        var headerGrid = NewGrid();
        string[] keys = ["plan.col.node", "plan.col.relative", "plan.col.cost", "plan.col.estRows", "plan.col.actRows", "plan.col.time", "plan.col.loops"];
        for (var c = 0; c < keys.Length; c++)
        {
            var head = Text(UiText.Get(keys[c]), muted, right: c > 1);
            head.Classes.Add("caps");
            Place(headerGrid, c, head);
        }

        _header.Child = headerGrid;
        _header.BorderThickness = new Thickness(0, 0, 0, 1);
        _header.BorderBrush = Brush("SquillBorderSubtleBrush");

        _rows.Children.Clear();
        _rowElements.Clear();
        foreach (var row in rows)
        {
            var ink = row.IsHelper ? muted : normal;
            var grid = NewGrid();
            var node = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            node.Children.Add(Text(row.Label, ink, semibold: !row.IsHelper));
            if (row.Target is { } target)
                node.Children.Add(Text(target, muted, mono: true));
            Place(grid, 0, node);
            node.Margin = new Thickness(row.Depth * Indent, 0, 0, 0);

            if (row.RelativeShare is { } share)
            {
                var bar = new Grid { Width = BarWidth, Height = 4, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Background = track };
                bar.Children.Add(new Border { Width = BarWidth * Math.Clamp(share, 0, 1), HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(2), Background = SeverityBrush(row.Severity) });
                var relative = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                relative.Children.Add(bar);
                relative.Children.Add(Text(row.RelativeText, ink, mono: true, right: true));
                Place(grid, 1, relative);
            }

            for (var c = 0; c < row.NumericCells.Count; c++)
                Place(grid, c + 2, Text(row.NumericCells[c], c == 2 && row.MisestimateText.Length > 0 ? warning : ink, mono: true, right: true));

            var element = new Border { Height = RowHeight, Child = grid, Background = Brushes.Transparent };
            var captured = row;
            element.PointerPressed += (_, _) => plan.SelectedRow = captured;
            _rows.Children.Add(element);
            _rowElements.Add(element);
        }
    }

    private void UpdateSelection()
    {
        if (_plan is not { } plan)
            return;
        var selection = Brush("SquillSelectionBrush");
        for (var i = 0; i < _rowElements.Count; i++)
            _rowElements[i].Background = ReferenceEquals(plan.Rows[i], plan.SelectedRow) ? selection : Brushes.Transparent;
        var index = IndexOfSelected(plan);
        if (index >= 0)
            _rowElements[index].BringIntoView();
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
        var muted = Brush("SquillFg2Brush");
        var normal = Brush("SquillFgBrush");
        var warning = Brush("SquillWarningBrush");
        var severity = SeverityBrush(row.Severity);

        var stack = new StackPanel { Spacing = 14, Margin = new Thickness(14) };

        var title = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(), new ColumnDefinition(GridLength.Auto) } };
        if (row.Severity != PlanSeverity.Normal)
            title.Children.Add(new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), Background = severity });

        var name = Text(details.Title, normal, semibold: true);
        name.TextWrapping = TextWrapping.Wrap;
        Grid.SetColumn(name, 1);
        title.Children.Add(name);
        if (row.RelativeShare is not null)
        {
            var percent = Text(row.RelativeText, severity, mono: true, right: true);
            percent.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(percent, 2);
            title.Children.Add(percent);
        }

        stack.Children.Add(title);

        if (details.ShareText.Length > 0)
        {
            var share = Text(details.ShareText, muted);
            share.TextWrapping = TextWrapping.Wrap;
            stack.Children.Add(share);
        }

        if (details.Properties.Count > 0)
        {
            var list = new Grid { ColumnDefinitions = { new ColumnDefinition(96, GridUnitType.Pixel), new ColumnDefinition() }, RowSpacing = 6, ColumnSpacing = 10 };
            for (var i = 0; i < details.Properties.Count; i++)
            {
                list.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var label = Text(details.Properties[i].Name, muted);
                label.TextWrapping = TextWrapping.Wrap;
                label.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetRow(label, i);
                var value = Text(details.Properties[i].Value, normal, mono: true, right: true);
                value.TextWrapping = TextWrapping.Wrap;
                value.TextAlignment = TextAlignment.Right;
                value.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetRow(value, i);
                Grid.SetColumn(value, 1);
                list.Children.Add(label);
                list.Children.Add(value);
            }

            stack.Children.Add(list);
        }

        foreach (var text in details.Warnings)
        {
            var line = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition() }, ColumnSpacing = 8 };
            var icon = new Path { Width = 14, Height = 14, Stroke = warning, StrokeThickness = 1.5, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Top };
            if (this.TryFindResource("SquillIconWarning", ActualThemeVariant, out var geometry) && geometry is Geometry g)
                icon.Data = g;
            line.Children.Add(icon);
            var message = Text(text, normal);
            message.TextWrapping = TextWrapping.Wrap;
            Grid.SetColumn(message, 1);
            line.Children.Add(message);
            stack.Children.Add(new Border { Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(6), Background = Brush("SquillWarningSoftBrush"), Child = line });
        }

        if (details.HasSuggestions)
            stack.Children.Add(BuildSuggestion(details, normal));

        _details.Content = stack;
    }

    private Border BuildSuggestion(PlanDetailsViewModel details, IBrush normal)
    {
        var card = new StackPanel { Spacing = 10 };
        card.Children.Add(Text(UiText.Get("plan.suggestion.title"), normal, semibold: true));

        var keyword = Brush("SquillSynKeywordBrush");
        foreach (var suggestion in details.Suggestions)
        {
            var code = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap, FontFamily = Mono, Foreground = normal };
            var words = suggestion.Text.Split(' ');
            for (var i = 0; i < words.Length; i++)
            {
                var run = new Run { Text = i == 0 ? words[i] : " " + words[i] };
                if (IndexKeywords.Contains(words[i], StringComparer.OrdinalIgnoreCase))
                    run.Foreground = keyword;
                code.Inlines!.Add(run);
            }

            card.Children.Add(new Border { Padding = new Thickness(10), CornerRadius = new CornerRadius(6), Background = Brush("SquillBgBaseBrush"), Child = code });
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var copy = new Button { Content = UiText.Get("plan.copy") };
        copy.Classes.Add("ghost");
        copy.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(details.CopyText);
        };
        var open = new Button { Content = UiText.Get("plan.openineditor") };
        open.Classes.Add("ghost");
        open.Click += (_, _) => _tab?.OpenInEditorCommand.Execute(details.CopyText);
        buttons.Children.Add(copy);
        buttons.Children.Add(open);
        card.Children.Add(buttons);

        return new Border { Padding = new Thickness(12), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Child = card, Background = Brush("SquillAccentSoftBrush"), BorderBrush = Brush("SquillAccentLineBrush") };
    }
}
