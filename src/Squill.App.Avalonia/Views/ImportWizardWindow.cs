using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ConnectionEnvironment = Squill.Core.ConnectionEnvironment;
using Path = Avalonia.Controls.Shapes.Path;
using Squill.Presentation.Import;
using Squill.Presentation.Localization;

namespace Squill.App.Avalonia.Views;

/// <summary>
/// The import wizard of mockup M10, in its own modal window: step rail, one page per step and the footer. It only binds
/// <see cref="ImportWizardViewModel"/>; the pages are built in code and every combo box holds plain strings mapped back by index.
/// </summary>
internal sealed class ImportWizardWindow : Window
{
    private const int RailWidth = 262;

    private static readonly char[] Delimiters = [',', ';', '\t', '|'];
    private static readonly string[] DelimiterKeys = ["import.ui.delim.comma", "import.ui.delim.semicolon", "import.ui.delim.tab", "import.ui.delim.pipe"];
    private static readonly char[] Quotes = ['"', '\''];
    private static readonly string[] QuoteKeys = ["import.ui.quote.double", "import.ui.quote.single"];
    private static readonly char[] Decimals = ['.', ','];
    private static readonly string[] DecimalKeys = ["import.ui.decimal.dot", "import.ui.decimal.comma"];

    private readonly ImportWizardViewModel _vm;
    private readonly StackPanel _rail = new() { Spacing = 4, Margin = new Thickness(12, 24, 12, 0) };
    private readonly TextBlock _pageTitle = new() { FontSize = 22, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly TextBlock _stepText = new() { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 3) };
    private readonly TextBlock _summary = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Border _pageHost = new();
    private readonly TextBlock _nextText = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _nextChip = Chip("Alt+→");
    private readonly List<ComboBox> _targetBoxes = [];
    private bool _syncing;

    // Live parts of the page on screen; replaced whenever the page is built.
    private TextBlock? _fileName;
    private TextBlock? _fileInfo;
    private Border? _previewHost;
    private ComboBox? _encodingBox;
    private ComboBox? _delimiterBox;
    private ComboBox? _quoteBox;
    private CheckBox? _headerBox;
    private ComboBox? _tableBox;
    private Border? _pill;
    private Border? _warningBand;
    private TextBlock? _warningText;
    private TextBlock? _issuesText;
    private TextBlock? _largeFileHint;
    private TextBox? _rejectsBox;
    private ProgressBar? _progress;
    private TextBlock? _counts;
    private TextBlock? _rate;
    private TextBlock? _outcome;
    private StackPanel? _runActions;
    private Button? _openRejects;

    /// <summary>Creates the window and shows the first page.</summary>
    /// <param name="vm">The wizard to draw.</param>
    public ImportWizardWindow(ImportWizardViewModel vm)
    {
        _vm = vm;
        Title = UiText.Get("import.title");
        Width = 1180;
        Height = 760;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.Paint(BackgroundProperty, "SquillBgBaseBrush");
        CancelButton = new Button { Height = 30, MinHeight = 30 };
        BackButton = new Button { Height = 30, MinHeight = 30 };
        NextButton = new Button { Height = 30, MinHeight = 30, Classes = { "primary" } };
        BuildFrame();
        BuildRail();
        BuildPage();
        UpdateFooter();

        // Escape cancels a running import or closes the wizard; Enter does nothing: the run starts from its button only.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _vm.PropertyChanged += OnChanged;
        _vm.CloseRequested += OnCloseRequested;
        _vm.OpenRejectsRequested += OnOpenRejects;
        Closed += (_, _) =>
        {
            _vm.PropertyChanged -= OnChanged;
            _vm.CloseRequested -= OnCloseRequested;
            _vm.OpenRejectsRequested -= OnOpenRejects;
        };
    }

    /// <summary>Gets the Annuler / Fermer button.</summary>
    public Button CancelButton { get; }

    /// <summary>Gets the Retour button.</summary>
    public Button BackButton { get; }

    /// <summary>Gets the primary button (next step, or the explicit start of the import).</summary>
    public Button NextButton { get; }

    /// <summary>Gets the title of the page on screen.</summary>
    public string PageTitle => _pageTitle.Text ?? "";

    /// <summary>Gets the target-column combo boxes of the mapping page, one per mapping row; empty on the other pages.</summary>
    public IReadOnlyList<ComboBox> TargetBoxes => _targetBoxes;

    /// <summary>Shows the window over another one.</summary>
    /// <param name="owner">The window that owns the dialog.</param>
    /// <param name="vm">The wizard to draw.</param>
    /// <returns>A task that completes when the window is closed.</returns>
    public static async Task ShowAsync(Window owner, ImportWizardViewModel vm) => await new ImportWizardWindow(vm).ShowDialog(owner);

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            _vm.Cancel();
        }
        else if (e.Key == Key.Left && e.KeyModifiers == KeyModifiers.Alt)
        {
            e.Handled = true;
            if (_vm.CanGoBack)
                _vm.Back();
        }
        else if (e.Key == Key.Right && e.KeyModifiers == KeyModifiers.Alt)
        {
            e.Handled = true;
            if (_vm.Step != ImportStep.Options && _vm.CanGoNext)
                _ = _vm.NextCommand.ExecuteAsync(null);
        }
    }

    private void OnCloseRequested(ImportWizardViewModel _) => Close();

    private void OnOpenRejects(string path) =>
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();

    // ---- small builders ---------------------------------------------------------------------------------------------

    private static Border Chip(string text) => new() { Classes = { "kbd" }, VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = text } };

    private static T Mono<T>(T control) where T : Control
    {
        control.Classes.Add("mono");
        return control;
    }

    private static Path Glyph(string key, double size, string brushKey)
    {
        var icon = new Path { Classes = { "icon" }, Width = size, Height = size };
        icon.Bind(Path.DataProperty, icon.GetResourceObservable(key));
        icon.Paint(Shape.StrokeProperty, brushKey);
        return icon;
    }

    private static TextBlock Text(string text, string brushKey, double size = 0, FontWeight weight = FontWeight.Normal)
    {
        var block = new TextBlock { Text = text, FontWeight = weight };
        if (size > 0)
            block.FontSize = size;
        return block.Paint(TextBlock.ForegroundProperty, brushKey);
    }

    private static TextBlock Caption(string key) => Text(UiText.Get(key), "SquillFg2Brush");

    private ComboBox Combo(string name, IEnumerable<string> items, int selected, Action<int> changed)
    {
        var box = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        Mono(box);
        foreach (var item in items)
            box.Items.Add(item);
        box.SelectedIndex = selected;
        box.SelectionChanged += (_, _) =>
        {
            if (!_syncing && box.SelectedIndex >= 0)
                changed(box.SelectedIndex);
        };
        global::Avalonia.Automation.AutomationProperties.SetName(box, name);
        return box;
    }

    private static StackPanel Field(string key, Control control, double width = 0)
    {
        var field = new StackPanel { Spacing = 4 };
        if (width > 0)
            field.Width = width;
        field.Children.Add(Caption(key));
        field.Children.Add(control);
        return field;
    }

    private static Border Card(string glyphKey, out TextBlock name, out TextBlock info)
    {
        name = Mono(Text("", "SquillFgBrush", 0, FontWeight.SemiBold));
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        info = Text("", "SquillFg2Brush", 12);
        info.TextTrimming = TextTrimming.CharacterEllipsis;
        var text = new StackPanel { Children = { name, info }, VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { Glyph(glyphKey, 20, "SquillFg2Brush"), text } };
        var card = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 12), Child = row };
        return card.Paint(Border.BorderBrushProperty, "SquillBorderBrush");
    }

    private static int IndexOf(IReadOnlyList<string> list, string? value)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
                return i;
        }

        return -1;
    }

    // ---- frame: rail, header, footer -------------------------------------------------------------------------------

    private void BuildFrame()
    {
        _pageTitle.Paint(TextBlock.ForegroundProperty, "SquillFgBrush");
        _stepText.Paint(TextBlock.ForegroundProperty, "SquillFg3Brush");
        _summary.Paint(TextBlock.ForegroundProperty, "SquillFg2Brush");
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(32, 28, 32, 16), Children = { _pageTitle, _stepText } };
        _pageHost.Margin = new Thickness(32, 0, 32, 16);
        var content = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        content.Children.Add(header);
        Grid.SetRow(_pageHost, 1);
        content.Children.Add(_pageHost);

        CancelButton.Click += (_, _) => _vm.Cancel();
        BackButton.Click += (_, _) => _vm.Back();
        NextButton.Click += (_, _) => _ = _vm.NextCommand.ExecuteAsync(null);
        NextButton.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _nextText, _nextChip } };
        BackButton.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { Chip("Alt+←"), new TextBlock { Text = UiText.Get("import.ui.back"), VerticalAlignment = VerticalAlignment.Center } },
        };
        global::Avalonia.Automation.AutomationProperties.SetName(BackButton, UiText.Get("import.ui.back"));
        var footer = new Grid { Margin = new Thickness(32, 12, 32, 16), ColumnSpacing = 8, ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto") };
        footer.Children.Add(_summary);
        foreach (var (button, column) in new[] { (CancelButton, 1), (BackButton, 2), (NextButton, 3) })
        {
            Grid.SetColumn(button, column);
            footer.Children.Add(button);
        }

        var footerBorder = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Child = footer }.Paint(Border.BorderBrushProperty, "SquillBorderSubtleBrush");
        Grid.SetRow(footerBorder, 2);
        content.Children.Add(footerBorder);

        var title = Text(UiText.Get("import.ui.title"), "SquillFgBrush", 20, FontWeight.SemiBold);
        title.Margin = new Thickness(28, 28, 28, 0);
        var subtitle = Text(UiText.Format("import.ui.subtitle", _vm.SelectedSession?.Profile.Dialect.ToString() ?? ""), "SquillFg2Brush");
        subtitle.Margin = new Thickness(28, 2, 28, 0);
        Grid.SetRow(subtitle, 1);
        Grid.SetRow(_rail, 2);
        var railGrid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Children = { title, subtitle, _rail } };
        var rail = new Border { Width = RailWidth, BorderThickness = new Thickness(0, 0, 1, 0), Child = railGrid };
        rail.Paint(Border.BackgroundProperty, "SquillBgRaisedBrush").Paint(Border.BorderBrushProperty, "SquillBorderSubtleBrush");

        var frame = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        frame.Children.Add(rail);
        Grid.SetColumn(content, 1);
        frame.Children.Add(content);
        Content = frame;
    }

    private void BuildRail()
    {
        _rail.Children.Clear();
        foreach (var item in _vm.Steps)
        {
            var current = item.State == ImportStepState.Current;
            var disc = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center };
            if (item.State == ImportStepState.Done)
            {
                disc.Paint(Border.BackgroundProperty, "SquillSuccessBrush");
                disc.Child = Glyph("SquillIconCheck", 14, "SquillOnAccentBrush");
            }
            else
            {
                if (current)
                    disc.Paint(Border.BackgroundProperty, "SquillAccentBrush");
                else
                    disc.Paint(Border.BorderBrushProperty, "SquillBorderBrush");
                var number = Text(item.Number.ToString(UiText.Culture), current ? "SquillOnAccentBrush" : "SquillFg2Brush", 12);
                number.HorizontalAlignment = HorizontalAlignment.Center;
                number.VerticalAlignment = VerticalAlignment.Center;
                disc.Child = number;
            }

            var label = Text(item.Title, item.State == ImportStepState.Pending ? "SquillFg2Brush" : "SquillFgBrush", 0, current ? FontWeight.SemiBold : FontWeight.Normal);
            label.VerticalAlignment = VerticalAlignment.Center;
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(12, 8), Children = { disc, label } };
            global::Avalonia.Automation.AutomationProperties.SetName(line, item.Title);
            var row = new Border { CornerRadius = new CornerRadius(6), Child = line };
            if (current)
                row.Paint(Border.BackgroundProperty, "SquillAccentSoftBrush");
            _rail.Children.Add(row);
        }
    }

    private void UpdateFooter()
    {
        var running = _vm.IsRunning;
        var step = _vm.Step;
        _stepText.Text = UiText.Format("import.ui.stepOf", (int)step + 1);
        _pageTitle.Text = UiText.Get(step switch
        {
            ImportStep.Source => "import.ui.page.source",
            ImportStep.Target => "import.ui.page.target",
            ImportStep.Mapping => "import.ui.page.mapping",
            ImportStep.Options => "import.ui.page.options",
            _ => "import.ui.page.run",
        });
        CancelButton.Content = UiText.Get(step == ImportStep.Run && !running && _vm.Result is not null ? "import.ui.close" : "import.ui.cancel");
        BackButton.IsEnabled = _vm.CanGoBack;
        NextButton.IsVisible = step != ImportStep.Run;
        BackButton.IsVisible = !(step == ImportStep.Run && running);
        NextButton.IsEnabled = _vm.CanGoNext;
        _nextChip.IsVisible = step != ImportStep.Options;
        _nextText.Text = step switch
        {
            ImportStep.Source => UiText.Format("import.ui.next", UiText.Get("import.step.target")),
            ImportStep.Target => UiText.Format("import.ui.next", UiText.Get("import.step.mapping")),
            ImportStep.Mapping => UiText.Format("import.ui.next", UiText.Get("import.step.options")),
            _ => UiText.Get("import.ui.start"),
        };
        _summary.Text = step switch
        {
            ImportStep.Source => _vm.FileSummary,
            ImportStep.Mapping => _vm.MappingSummary,
            _ => "",
        };
    }

    // ---- live updates -----------------------------------------------------------------------------------------------

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        var refresh = ImportWizardRefreshMap.For(e.PropertyName);
        if (refresh.HasFlag(ImportWizardRefresh.Page))
            BuildPage();
        if (refresh.HasFlag(ImportWizardRefresh.Rail))
            BuildRail();
        if (refresh.HasFlag(ImportWizardRefresh.Footer))
            UpdateFooter();
        if (refresh.HasFlag(ImportWizardRefresh.Source))
            UpdateSource();
        if (refresh.HasFlag(ImportWizardRefresh.SourceOptions))
            SyncSourceOptions();
        if (refresh.HasFlag(ImportWizardRefresh.Tables))
            FillTables();
        if (refresh.HasFlag(ImportWizardRefresh.Pill))
            UpdatePill();
        if (refresh.HasFlag(ImportWizardRefresh.Warnings))
            UpdateWarnings();
        if (refresh.HasFlag(ImportWizardRefresh.Options))
            UpdateOptions();
        if (refresh.HasFlag(ImportWizardRefresh.Run))
            UpdateRun();
    }

    private void BuildPage()
    {
        _fileName = _fileInfo = _warningText = _issuesText = _largeFileHint = _counts = _rate = _outcome = null;
        _previewHost = null;
        _warningBand = null;
        _encodingBox = _delimiterBox = _quoteBox = _tableBox = null;
        _headerBox = null;
        _pill = null;
        _rejectsBox = null;
        _progress = null;
        _runActions = null;
        _openRejects = null;
        _targetBoxes.Clear();
        _pageHost.Child = _vm.Step switch
        {
            ImportStep.Source => SourcePage(),
            ImportStep.Target => TargetPage(),
            ImportStep.Mapping => MappingPage(),
            ImportStep.Options => OptionsPage(),
            _ => RunPage(),
        };
        UpdateSource();
        UpdateWarnings();
        UpdateOptions();
        UpdateRun();
    }

    // ---- step 1: source ---------------------------------------------------------------------------------------------

    private Grid SourcePage()
    {
        var card = Card("SquillIconFolder", out _fileName, out _fileInfo);
        var choose = new Button { Content = UiText.Get("import.ui.chooseFile"), Height = 30, VerticalAlignment = VerticalAlignment.Center };
        choose.Click += (_, _) => _ = _vm.ChooseFileCommand.ExecuteAsync(null);
        var top = new Grid { ColumnSpacing = 12, ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        top.Children.Add(card);
        Grid.SetColumn(choose, 1);
        top.Children.Add(choose);

        _encodingBox = Combo(UiText.Get("import.ui.encoding"), _vm.EncodingNames, IndexOf(_vm.EncodingNames, _vm.EncodingName), i => _vm.EncodingName = _vm.EncodingNames[i]);
        _delimiterBox = Combo(UiText.Get("import.ui.delimiter"), DelimiterKeys.Select(UiText.Get), Array.IndexOf(Delimiters, _vm.Delimiter), i => _vm.Delimiter = Delimiters[i]);
        _quoteBox = Combo(UiText.Get("import.ui.quote"), QuoteKeys.Select(UiText.Get), Array.IndexOf(Quotes, _vm.Quote), i => _vm.Quote = Quotes[i]);
        _headerBox = new CheckBox { Content = UiText.Get("import.ui.header"), IsChecked = _vm.HasHeader, VerticalAlignment = VerticalAlignment.Bottom };
        _headerBox.IsCheckedChanged += (_, _) =>
        {
            if (!_syncing)
                _vm.HasHeader = _headerBox.IsChecked == true;
        };
        var options = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Children = { Field("import.ui.encoding", _encodingBox, 180), Field("import.ui.delimiter", _delimiterBox, 200), Field("import.ui.quote", _quoteBox, 160), _headerBox },
        };

        _previewHost = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) }.Paint(Border.BorderBrushProperty, "SquillBorderBrush");
        var page = new Grid { RowSpacing = 16, RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        page.Children.Add(top);
        Grid.SetRow(options, 1);
        page.Children.Add(options);
        Grid.SetRow(_previewHost, 2);
        page.Children.Add(_previewHost);
        return page;
    }

    private void SyncSourceOptions()
    {
        _syncing = true;
        try
        {
            if (_encodingBox is not null)
                _encodingBox.SelectedIndex = IndexOf(_vm.EncodingNames, _vm.EncodingName);
            if (_delimiterBox is not null)
                _delimiterBox.SelectedIndex = Array.IndexOf(Delimiters, _vm.Delimiter);
            if (_quoteBox is not null)
                _quoteBox.SelectedIndex = Array.IndexOf(Quotes, _vm.Quote);
            if (_headerBox is not null)
                _headerBox.IsChecked = _vm.HasHeader;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void UpdateSource()
    {
        if (_fileName is not null && _fileInfo is not null)
        {
            _fileName.Text = _vm.FilePath.Length == 0 ? UiText.Get("import.ui.noFile") : global::System.IO.Path.GetFileName(_vm.FilePath);
            _fileInfo.Text = _vm.FilePath.Length == 0 ? "" : _vm.FileSummary + " · " + _vm.ColumnsText;
        }

        if (_previewHost is null)
            return;
        var grid = new Grid { ColumnSpacing = 16, RowSpacing = 6, Margin = new Thickness(12) };
        if (_vm.Preview is { } preview)
        {
            const int shown = 8;
            for (var c = 0; c < preview.ColumnNames.Count; c++)
                grid.ColumnDefinitions.Add(new ColumnDefinition(150, GridUnitType.Pixel));
            for (var r = 0; r <= Math.Min(shown, preview.Sample.Count); r++)
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var c = 0; c < preview.ColumnNames.Count; c++)
            {
                var head = Text(preview.ColumnNames[c], "SquillFgBrush", 0, FontWeight.SemiBold);
                head.TextTrimming = TextTrimming.CharacterEllipsis;
                Grid.SetColumn(head, c);
                grid.Children.Add(head);
                for (var r = 0; r < Math.Min(shown, preview.Sample.Count); r++)
                {
                    var cell = Mono(Text(c < preview.Sample[r].Length ? preview.Sample[r][c] ?? "" : "", "SquillFg2Brush", 12));
                    cell.TextTrimming = TextTrimming.CharacterEllipsis;
                    Grid.SetColumn(cell, c);
                    Grid.SetRow(cell, r + 1);
                    grid.Children.Add(cell);
                }
            }
        }

        _previewHost.Child = new ScrollViewer { HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = grid };
    }

    // ---- step 2: target ---------------------------------------------------------------------------------------------

    private StackPanel TargetPage()
    {
        var names = _vm.Sessions.Select(s => s.Profile.Name).ToList();
        var connection = Combo(UiText.Get("import.ui.connection"), names, _vm.SelectedSession is { } selected ? _vm.Sessions.ToList().IndexOf(selected) : -1, i => _vm.SelectedSession = _vm.Sessions[i]);
        _tableBox = Combo(UiText.Get("import.ui.table"), [], -1, i => _vm.SelectedTable = _vm.TableNames[i]);
        _pill = new Border { Classes = { "badge" }, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 6), Child = new TextBlock() };
        var append = Text(UiText.Get("import.append"), "SquillFg3Brush");
        append.VerticalAlignment = VerticalAlignment.Bottom;
        append.Margin = new Thickness(0, 0, 0, 8);
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { Field("import.ui.connection", connection, 300), _pill, Field("import.ui.table", _tableBox, 300), append },
        };
        FillTables();
        UpdatePill();
        return row;
    }

    private void FillTables()
    {
        if (_tableBox is null)
            return;
        _syncing = true;
        try
        {
            _tableBox.Items.Clear();
            foreach (var name in _vm.TableNames)
                _tableBox.Items.Add(name);
            _tableBox.SelectedIndex = IndexOf(_vm.TableNames, _vm.SelectedTable);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void UpdatePill()
    {
        if (_pill is null)
            return;
        var (label, css) = _vm.Environment switch
        {
            ConnectionEnvironment.Production => (UiText.Get("status.production"), "env-prod"),
            ConnectionEnvironment.Staging => (UiText.Get("status.staging"), "env-staging"),
            ConnectionEnvironment.Development => (UiText.Get("status.dev"), "env-dev"),
            ConnectionEnvironment.Local => (UiText.Get("status.local"), "env-local"),
            _ => ("", ""),
        };
        foreach (var name in new[] { "env-prod", "env-staging", "env-dev", "env-local" })
            _pill.Classes.Set(name, name == css);
        ((TextBlock)_pill.Child!).Text = label;
        _pill.IsVisible = label.Length > 0;
    }

    // ---- step 3: mapping --------------------------------------------------------------------------------------------

    private Grid MappingPage()
    {
        var file = Card("SquillIconFolder", out var fileName, out var fileInfo);
        fileName.Text = global::System.IO.Path.GetFileName(_vm.FilePath);
        fileInfo.Text = _vm.FileSummary + " · " + _vm.ColumnsText;
        var table = Card("SquillIconTable", out var tableName, out var tableInfo);
        tableName.Text = _vm.SelectedTable ?? "";
        tableInfo.Text = (_vm.SelectedSession?.Profile.Name ?? "") + " · " + UiText.Get("import.append");
        var cards = new Grid { ColumnSpacing = 12, ColumnDefinitions = new ColumnDefinitions("*,Auto,*") };
        cards.Children.Add(file);
        var arrow = Glyph("SquillIconArrowLeft", 18, "SquillFg2Brush");
        arrow.RenderTransform = new ScaleTransform(-1, 1);
        Grid.SetColumn(arrow, 1);
        cards.Children.Add(arrow);
        Grid.SetColumn(table, 2);
        cards.Children.Add(table);

        var decimalBox = Combo(UiText.Get("import.ui.decimal"), DecimalKeys.Select(UiText.Get), Array.IndexOf(Decimals, _vm.DecimalSeparator), i => _vm.DecimalSeparator = Decimals[i]);
        var dateBox = Mono(new TextBox { Text = _vm.DateFormat, PlaceholderText = "yyyy-MM-dd" });
        dateBox.TextChanged += (_, _) => _vm.DateFormat = dateBox.Text ?? "";
        global::Avalonia.Automation.AutomationProperties.SetName(dateBox, UiText.Get("import.ui.dateFormat"));
        var zoneBox = Mono(new TextBox { Text = _vm.TimeZoneId, PlaceholderText = "Europe/Paris" });
        zoneBox.TextChanged += (_, _) => _vm.TimeZoneId = zoneBox.Text ?? "";
        global::Avalonia.Automation.AutomationProperties.SetName(zoneBox, UiText.Get("import.ui.timeZone"));
        var format = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Children = { Field("import.ui.decimal", decimalBox, 180), Field("import.ui.dateFormat", dateBox, 220), Field("import.ui.timeZone", zoneBox, 220) },
        };

        _warningText = Text("", "SquillWarningBrush");
        _warningText.TextWrapping = TextWrapping.Wrap;
        _warningText.VerticalAlignment = VerticalAlignment.Center;
        _warningBand = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 10), Child = _warningText, IsVisible = false }.Paint(Border.BackgroundProperty, "SquillWarningSoftBrush");
        _issuesText = Text("", "SquillDangerBrush");
        _issuesText.TextWrapping = TextWrapping.Wrap;
        _issuesText.IsVisible = false;

        var rows = new StackPanel();
        rows.Children.Add(HeaderRow());
        foreach (var row in _vm.Rows)
            rows.Children.Add(MapRow(row));
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = rows,
        };
        var tableBorder = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Child = scroll, ClipToBounds = true }.Paint(Border.BorderBrushProperty, "SquillBorderBrush");

        var top = new StackPanel { Spacing = 12, Children = { cards, format, _warningBand, _issuesText } };
        var page = new Grid { RowSpacing = 12, RowDefinitions = new RowDefinitions("Auto,*") };
        page.Children.Add(top);
        Grid.SetRow(tableBorder, 1);
        page.Children.Add(tableBorder);
        return page;
    }

    private static Grid MapGrid() => new() { ColumnSpacing = 12, Margin = new Thickness(12, 8), ColumnDefinitions = new ColumnDefinitions("32,170,230,280,250,28") };

    private static Grid HeaderRow()
    {
        var grid = MapGrid();
        string[] keys = ["import.ui.colSource", "import.ui.colPreview", "import.ui.colTarget", "import.ui.colConversion"];
        for (var i = 0; i < keys.Length; i++)
        {
            var head = Text(UiText.Get(keys[i]), "SquillFg2Brush", 0, FontWeight.SemiBold);
            Grid.SetColumn(head, i + 1);
            grid.Children.Add(head);
        }

        return grid;
    }

    private Border MapRow(ImportMapRowViewModel row)
    {
        var grid = MapGrid();
        var check = new CheckBox { IsChecked = row.Enabled, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
        check.IsCheckedChanged += (_, _) =>
        {
            if (!_syncing)
                row.Enabled = check.IsChecked == true;
        };
        global::Avalonia.Automation.AutomationProperties.SetName(check, row.SourceName);
        var source = Mono(new TextBlock { Text = row.SourceName, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        var preview = Mono(Text(string.Join(" · ", row.Preview), "SquillFg3Brush", 12));
        preview.VerticalAlignment = VerticalAlignment.Center;
        preview.TextTrimming = TextTrimming.CharacterEllipsis;

        // Plain strings only; the position in the list maps back to the table column.
        var choices = _vm.TargetChoices;
        var target = Combo(row.SourceName, choices.Prepend(UiText.Get("import.ui.ignore")), row.Target is null ? 0 : IndexOf(choices, row.Target) + 1,
            i => row.Target = i <= 0 ? null : choices[i - 1]);
        _targetBoxes.Add(target);
        var conversion = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        var status = new TextBlock { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { conversion, status } };
        var mark = new ContentControl { VerticalAlignment = VerticalAlignment.Center };
        var border = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Child = grid }.Paint(Border.BorderBrushProperty, "SquillBorderSubtleBrush");
        Control[] cells = [check, source, preview, target, text, mark];
        for (var i = 0; i < cells.Length; i++)
        {
            Grid.SetColumn(cells[i], i);
            grid.Children.Add(cells[i]);
        }

        void Sync()
        {
            _syncing = true;
            try
            {
                check.IsChecked = row.Enabled;
                target.SelectedIndex = row.Target is null ? 0 : IndexOf(choices, row.Target) + 1;
            }
            finally
            {
                _syncing = false;
            }

            conversion.Paint(TextBlock.ForegroundProperty, row.Enabled ? "SquillFgBrush" : "SquillFg3Brush");
            conversion.Text = row.Conversion;
            status.Text = row.Status;
            status.Paint(TextBlock.ForegroundProperty, row.IsWarning ? "SquillWarningBrush" : "SquillFg3Brush");
            if (row.IsWarning)
                border.Paint(Border.BackgroundProperty, "SquillWarningSoftBrush");
            else
                border.Background = null;
            mark.Content = row.Enabled && row.Target is not null
                ? Glyph(row.IsWarning ? "SquillIconWarning" : "SquillIconCheck", 16, row.IsWarning ? "SquillWarningBrush" : "SquillSuccessBrush")
                : null;
        }

        row.PropertyChanged += (_, _) => Sync();
        Sync();
        return border;
    }

    private void UpdateWarnings()
    {
        if (_warningBand is not null && _warningText is not null)
        {
            _warningText.Text = _vm.WarningText;
            _warningBand.IsVisible = _vm.WarningText.Length > 0;
        }

        if (_issuesText is not null)
        {
            _issuesText.Text = string.Join(System.Environment.NewLine, _vm.Issues);
            _issuesText.IsVisible = _vm.Issues.Count > 0;
        }
    }

    // ---- step 4: options --------------------------------------------------------------------------------------------

    private StackPanel OptionsPage()
    {
        var batch = new NumericUpDown { Value = _vm.BatchSize, Minimum = 1, Increment = 100, FormatString = "0" };
        batch.ValueChanged += (_, e) =>
        {
            if (e.NewValue is { } value)
                _vm.BatchSize = (int)Math.Min(int.MaxValue, value);
        };
        global::Avalonia.Automation.AutomationProperties.SetName(batch, UiText.Get("import.ui.batchSize"));
        var onError = Combo(UiText.Get("import.ui.onError"), new[] { UiText.Get("import.ui.onError.reject"), UiText.Get("import.ui.onError.stop") }, (int)_vm.OnError, i => _vm.OnError = (ImportErrorPolicy)i);
        var single = new CheckBox { Content = UiText.Get("import.ui.singleTransaction"), IsChecked = _vm.SingleTransaction };
        single.IsCheckedChanged += (_, _) => _vm.SingleTransaction = single.IsChecked == true;
        _largeFileHint = Text(UiText.Get("import.ui.largeFile"), "SquillWarningBrush");
        _largeFileHint.TextWrapping = TextWrapping.Wrap;
        _rejectsBox = Mono(new TextBox { IsReadOnly = true });
        global::Avalonia.Automation.AutomationProperties.SetName(_rejectsBox, UiText.Get("import.ui.rejectsFile"));
        var chooseRejects = new Button { Content = UiText.Get("import.ui.chooseRejects"), Height = 30 };
        chooseRejects.Click += (_, _) => _ = _vm.ChooseRejectsCommand.ExecuteAsync(null);
        var rejects = new Grid { ColumnSpacing = 8, ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        rejects.Children.Add(_rejectsBox);
        Grid.SetColumn(chooseRejects, 1);
        rejects.Children.Add(chooseRejects);
        return new StackPanel
        {
            Spacing = 16,
            MaxWidth = 560,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { Field("import.ui.batchSize", batch), Field("import.ui.onError", onError), single, _largeFileHint, Field("import.ui.rejectsFile", rejects) },
        };
    }

    private void UpdateOptions()
    {
        if (_largeFileHint is not null)
            _largeFileHint.IsVisible = _vm.LargeFileHint;
        if (_rejectsBox is not null)
            _rejectsBox.Text = _vm.RejectsPath.Length == 0 ? _vm.DefaultRejectsPath : _vm.RejectsPath;
    }

    // ---- step 5: run ------------------------------------------------------------------------------------------------

    private StackPanel RunPage()
    {
        _progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 6 };
        _counts = Mono(Text("", "SquillFgBrush"));
        _rate = Mono(Text("", "SquillFg3Brush", 12));
        _outcome = Text("", "SquillFgBrush", 0, FontWeight.SemiBold);
        _outcome.TextWrapping = TextWrapping.Wrap;
        var viewTable = new Button { Content = UiText.Get("import.ui.viewTable") };
        viewTable.Click += (_, _) => _vm.ViewTableCommand.Execute(null);
        _openRejects = new Button { Content = UiText.Get("import.ui.openRejects") };
        _openRejects.Click += (_, _) => _vm.OpenRejectsCommand.Execute(null);
        _runActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { viewTable, _openRejects } };
        return new StackPanel { Spacing = 14, MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left, Children = { _progress, _counts, _rate, _outcome, _runActions } };
    }

    private void UpdateRun()
    {
        if (_progress is null || _counts is null || _rate is null || _outcome is null || _runActions is null || _openRejects is null)
            return;
        _progress.IsIndeterminate = _vm.IsRunning && _vm.Fraction == 0;
        _progress.Value = _vm.Fraction * 100;
        _counts.Text = $"{UiText.Get("import.ui.read")} {_vm.Read:N0}  ·  {UiText.Get("import.ui.inserted")} {_vm.Inserted:N0}  ·  {UiText.Get("import.ui.rejected")} {_vm.Rejected:N0}";
        _rate.Text = _vm.RemainingText.Length == 0 ? _vm.RateText : _vm.RateText + " · " + _vm.RemainingText;
        _outcome.Text = _vm.Result is null ? "" : _vm.Summary;
        _runActions.IsVisible = _vm.Result is not null;
        _openRejects.IsVisible = _vm.HasRejects;
    }
}

/// <summary>Binds a brush property of a control to a theme resource, so the control follows light and dark.</summary>
internal static class PaintExtensions
{
    /// <summary>Binds <paramref name="property"/> of <paramref name="control"/> to the resource <paramref name="key"/>.</summary>
    /// <typeparam name="T">The type of the control.</typeparam>
    /// <param name="control">The control to paint.</param>
    /// <param name="property">The brush property.</param>
    /// <param name="key">The theme resource key.</param>
    /// <returns>The control, for chaining.</returns>
    public static T Paint<T>(this T control, AvaloniaProperty property, string key) where T : AvaloniaObject
    {
        if (control is IResourceHost host)
            control.Bind(property, host.GetResourceObservable(key));
        return control;
    }
}
