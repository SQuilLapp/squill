using System.ComponentModel;
using System.Diagnostics;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Squill.Presentation.Import;
using Squill.Presentation.Localization;
using Windows.System;

namespace Squill.App.WinUI.Views;

/// <summary>
/// The import wizard of mockup M10, in its own modal window: step rail, one page per step and the footer. It only binds
/// <see cref="ImportWizardViewModel"/>; the pages are built in code and every combo box holds plain strings mapped back by index.
/// </summary>
public sealed partial class ImportWizardWindow : Window
{
    private const int LogicalWidth = 1180;
    private const int LogicalHeight = 760;
    private const int RailWidth = 262;

    private static readonly char[] Delimiters = [',', ';', '\t', '|'];
    private static readonly string[] DelimiterKeys = ["import.ui.delim.comma", "import.ui.delim.semicolon", "import.ui.delim.tab", "import.ui.delim.pipe"];
    private static readonly char[] Quotes = ['"', '\''];
    private static readonly string[] QuoteKeys = ["import.ui.quote.double", "import.ui.quote.single"];
    private static readonly char[] Decimals = ['.', ','];
    private static readonly string[] DecimalKeys = ["import.ui.decimal.dot", "import.ui.decimal.comma"];

    private readonly Window _owner;
    private readonly ImportWizardViewModel _vm;
    private readonly TaskCompletionSource _closed = new();
    private readonly Microsoft.UI.Xaml.Media.FontFamily _mono = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SquillFontMono"];
    private readonly StackPanel _rail = new() { Spacing = 4, Margin = new Thickness(12, 24, 12, 0) };
    private readonly TextBlock _pageTitle = new() { FontSize = 22, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly TextBlock _stepText = new() { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 3) };
    private readonly TextBlock _summary = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Border _pageHost = new();
    private readonly Button _cancel = new() { Height = 30, MinHeight = 30 };
    private readonly Button _back = new() { Height = 30, MinHeight = 30 };
    private readonly Button _next = new() { Height = 30, MinHeight = 30 };
    private readonly TextBlock _nextText = new();
    private readonly ContentControl _nextChip = new() { Content = "Alt+→", VerticalAlignment = VerticalAlignment.Center };
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
    private ContentControl? _pillHost;
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

    /// <summary>Creates the window, places it over the main window and shows the first page.</summary>
    /// <param name="owner">The main window; it stays disabled while this window is open.</param>
    /// <param name="vm">The wizard to draw.</param>
    public ImportWizardWindow(Window owner, ImportWizardViewModel vm)
    {
        InitializeComponent();
        _owner = owner;
        _vm = vm;
        Title = UiText.Get("import.title");
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "squill.ico"));
        CaptionBar.Follow(this, Root, (FrameworkElement)owner.Content);
        BuildFrame();
        BuildRail();
        BuildPage();
        UpdateFooter();
        ModalWindow.Place(this, owner, LogicalWidth, LogicalHeight, true);

        // Escape cancels a running import or closes the wizard; Enter does nothing: the run starts from its button only.
        Root.KeyboardAccelerators.Add(Accelerator(VirtualKey.Escape, VirtualKeyModifiers.None, () => _vm.Cancel()));
        Root.KeyboardAccelerators.Add(Accelerator(VirtualKey.Left, VirtualKeyModifiers.Menu, () => { if (_vm.CanGoBack) _vm.Back(); }));
        Root.KeyboardAccelerators.Add(Accelerator(VirtualKey.Right, VirtualKeyModifiers.Menu, () =>
        {
            if (_vm.Step != ImportStep.Options && _vm.CanGoNext)
                _ = _vm.NextCommand.ExecuteAsync(null);
        }));
        _vm.PropertyChanged += OnChanged;
        _vm.CloseRequested += OnCloseRequested;
        _vm.OpenRejectsRequested += OnOpenRejects;
        Closed += (_, _) =>
        {
            _vm.PropertyChanged -= OnChanged;
            _vm.CloseRequested -= OnCloseRequested;
            _vm.OpenRejectsRequested -= OnOpenRejects;
            _owner.Activate();
            _closed.TrySetResult();
        };
    }

    /// <summary>Gets a task that completes when the window is closed.</summary>
    public Task Closure => _closed.Task;

    private static KeyboardAccelerator Accelerator(VirtualKey key, VirtualKeyModifiers modifiers, Action action)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += (_, e) =>
        {
            e.Handled = true;
            action();
        };
        return accelerator;
    }

    private Microsoft.UI.Xaml.Media.Brush Brush(string key) => Tokens.Brush(Root, key);

    private void OnCloseRequested(ImportWizardViewModel _) => Close();

    private void OnOpenRejects(string path) =>
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();

    // ---- frame: rail, header, footer -------------------------------------------------------------------------------

    private void BuildFrame()
    {
        var content = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition(), new RowDefinition { Height = GridLength.Auto } } };
        _pageTitle.Foreground = Brush("SquillFgBrush");
        _stepText.Foreground = Brush("SquillFg3Brush");
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(32, 28, 32, 16), Children = { _pageTitle, _stepText } };
        content.Children.Add(header);
        Grid.SetRow(_pageHost, 1);
        _pageHost.Margin = new Thickness(32, 0, 32, 16);
        content.Children.Add(_pageHost);

        _summary.Foreground = Brush("SquillFg2Brush");
        _cancel.Click += (_, _) => _vm.Cancel();
        _back.Click += (_, _) => _vm.Back();
        _next.Click += (_, _) => _ = _vm.NextCommand.ExecuteAsync(null);
        _next.Style = (Style)Application.Current.Resources["PrimaryButtonStyle"];
        _nextChip.Style = (Style)Application.Current.Resources["KeyboardChipStyle"];
        _next.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _nextText, _nextChip } };
        _back.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new ContentControl { Content = "Alt+←", Style = (Style)Application.Current.Resources["KeyboardChipStyle"], VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = UiText.Get("import.ui.back") },
            },
        };
        AutomationProperties.SetName(_back, UiText.Get("import.ui.back"));
        var footer = new Grid
        {
            Padding = new Thickness(32, 12, 32, 16),
            ColumnSpacing = 8,
            BorderBrush = Brush("SquillBorderSubtleBrush"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Auto } },
        };
        footer.Children.Add(_summary);
        foreach (var (button, column) in new[] { (_cancel, 1), (_back, 2), (_next, 3) })
        {
            Grid.SetColumn(button, column);
            footer.Children.Add(button);
        }

        Grid.SetRow(footer, 2);
        content.Children.Add(footer);

        var title = new TextBlock { Text = UiText.Get("import.ui.title"), FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = Brush("SquillFgBrush"), Margin = new Thickness(28, 28, 28, 0) };
        var subtitle = new TextBlock
        {
            Text = UiText.Format("import.ui.subtitle", _vm.SelectedSession?.Profile.Dialect.ToString() ?? ""),
            Foreground = Brush("SquillFg2Brush"),
            Margin = new Thickness(28, 2, 28, 0),
        };
        var rail = new Grid
        {
            Width = RailWidth,
            Background = Brush("SquillBgRaisedBrush"),
            BorderBrush = Brush("SquillBorderSubtleBrush"),
            BorderThickness = new Thickness(0, 0, 1, 0),
            RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto }, new RowDefinition() },
        };
        rail.Children.Add(title);
        Grid.SetRow(subtitle, 1);
        rail.Children.Add(subtitle);
        Grid.SetRow(_rail, 2);
        rail.Children.Add(_rail);

        var frame = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition() } };
        frame.Children.Add(rail);
        Grid.SetColumn(content, 1);
        frame.Children.Add(content);
        Root.Children.Add(frame);
    }

    private void BuildRail()
    {
        _rail.Children.Clear();
        foreach (var item in _vm.Steps)
        {
            var current = item.State == ImportStepState.Current;
            var disc = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Background = item.State == ImportStepState.Done ? Brush("SquillSuccessBrush") : current ? Brush("SquillAccentBrush") : null,
                BorderBrush = item.State == ImportStepState.Pending ? Brush("SquillBorderBrush") : null,
                Child = item.State == ImportStepState.Done
                    ? Icons.Create("SquillIconCheck", 14, Brush("SquillOnAccentBrush"))
                    : new TextBlock
                    {
                        Text = item.Number.ToString(UiText.Culture),
                        FontSize = 12,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = current ? Brush("SquillOnAccentBrush") : Brush("SquillFg2Brush"),
                    },
            };
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Padding = new Thickness(12, 8, 12, 8) };
            line.Children.Add(disc);
            line.Children.Add(new TextBlock
            {
                Text = item.Title,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = current ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = item.State == ImportStepState.Pending ? Brush("SquillFg2Brush") : Brush("SquillFgBrush"),
            });
            AutomationProperties.SetName(line, item.Title);
            _rail.Children.Add(new Border { CornerRadius = new CornerRadius(6), Background = current ? Brush("SquillAccentSoftBrush") : null, Child = line });
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
        _cancel.Content = UiText.Get(step == ImportStep.Run && !running && _vm.Result is not null ? "import.ui.close" : "import.ui.cancel");
        _back.IsEnabled = _vm.CanGoBack;
        _next.Visibility = step == ImportStep.Run ? Visibility.Collapsed : Visibility.Visible;
        _back.Visibility = step == ImportStep.Run && running ? Visibility.Collapsed : Visibility.Visible;
        _next.IsEnabled = _vm.CanGoNext;
        _nextChip.Visibility = step == ImportStep.Options ? Visibility.Collapsed : Visibility.Visible;
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
        switch (e.PropertyName)
        {
            case nameof(ImportWizardViewModel.Step):
                BuildPage();
                UpdateFooter();
                break;
            case nameof(ImportWizardViewModel.Steps):
                BuildRail();
                break;
            case nameof(ImportWizardViewModel.CanGoNext) or nameof(ImportWizardViewModel.CanGoBack) or nameof(ImportWizardViewModel.IsRunning)
                or nameof(ImportWizardViewModel.MappingSummary) or nameof(ImportWizardViewModel.FileSummary):
                UpdateFooter();
                UpdateSource();
                UpdateRun();
                break;
            case nameof(ImportWizardViewModel.Preview) or nameof(ImportWizardViewModel.FilePath) or nameof(ImportWizardViewModel.ColumnsText):
                UpdateSource();
                break;
            case nameof(ImportWizardViewModel.EncodingName) or nameof(ImportWizardViewModel.Delimiter) or nameof(ImportWizardViewModel.Quote) or nameof(ImportWizardViewModel.HasHeader):
                SyncSourceOptions();
                break;
            case nameof(ImportWizardViewModel.TableNames):
                FillTables();
                break;
            case nameof(ImportWizardViewModel.Environment):
                UpdatePill();
                break;
            case nameof(ImportWizardViewModel.WarningText) or nameof(ImportWizardViewModel.Issues):
                UpdateWarnings();
                break;
            case nameof(ImportWizardViewModel.LargeFileHint) or nameof(ImportWizardViewModel.RejectsPath):
                UpdateOptions();
                break;
            case nameof(ImportWizardViewModel.Read) or nameof(ImportWizardViewModel.Inserted) or nameof(ImportWizardViewModel.Rejected)
                or nameof(ImportWizardViewModel.Fraction) or nameof(ImportWizardViewModel.RateText) or nameof(ImportWizardViewModel.RemainingText)
                or nameof(ImportWizardViewModel.Summary) or nameof(ImportWizardViewModel.Result) or nameof(ImportWizardViewModel.HasRejects):
                UpdateRun();
                UpdateFooter();
                break;
        }
    }

    private void BuildPage()
    {
        _fileName = _fileInfo = _warningText = _issuesText = _largeFileHint = _counts = _rate = _outcome = null;
        _previewHost = null;
        _warningBand = null;
        _encodingBox = _delimiterBox = _quoteBox = _tableBox = null;
        _headerBox = null;
        _pillHost = null;
        _rejectsBox = null;
        _progress = null;
        _runActions = null;
        _openRejects = null;
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

    // ---- small builders ---------------------------------------------------------------------------------------------

    private TextBlock Caption(string key) => new() { Text = UiText.Get(key), Foreground = Brush("SquillFg2Brush") };

    private ComboBox Combo(string name, IEnumerable<string> items, int selected, Action<int> changed)
    {
        var box = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, FontFamily = _mono };
        foreach (var item in items)
            box.Items.Add(item);
        box.SelectedIndex = selected;
        box.SelectionChanged += (_, _) =>
        {
            if (!_syncing && box.SelectedIndex >= 0)
                changed(box.SelectedIndex);
        };
        AutomationProperties.SetName(box, name);
        return box;
    }

    private StackPanel Field(string key, UIElement control, double width = 0)
    {
        var field = new StackPanel { Spacing = 4 };
        if (width > 0)
            field.Width = width;
        field.Children.Add(Caption(key));
        field.Children.Add(control);
        return field;
    }

    private Border Card(string glyphKey, out TextBlock name, out TextBlock info)
    {
        name = new TextBlock { FontFamily = _mono, FontWeight = FontWeights.SemiBold, Foreground = Brush("SquillFgBrush"), TextTrimming = TextTrimming.CharacterEllipsis };
        info = new TextBlock { Foreground = Brush("SquillFg2Brush"), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
        var text = new StackPanel { Children = { name, info }, VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { Icons.Create(glyphKey, 20, Brush("SquillFg2Brush")), text } };
        return new Border { BorderBrush = Brush("SquillBorderBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 12, 16, 12), Child = row };
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

    // ---- step 1: source ---------------------------------------------------------------------------------------------

    private Grid SourcePage()
    {
        var card = Card("SquillIconFolder", out _fileName, out _fileInfo);
        var choose = new Button { Content = UiText.Get("import.ui.chooseFile"), Height = 30, VerticalAlignment = VerticalAlignment.Center };
        choose.Click += (_, _) => _ = _vm.ChooseFileCommand.ExecuteAsync(null);
        var top = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        top.Children.Add(card);
        Grid.SetColumn(choose, 1);
        top.Children.Add(choose);

        _encodingBox = Combo(UiText.Get("import.ui.encoding"), _vm.EncodingNames, IndexOf(_vm.EncodingNames, _vm.EncodingName), i => _vm.EncodingName = _vm.EncodingNames[i]);
        _delimiterBox = Combo(UiText.Get("import.ui.delimiter"), DelimiterKeys.Select(UiText.Get), Array.IndexOf(Delimiters, _vm.Delimiter), i => _vm.Delimiter = Delimiters[i]);
        _quoteBox = Combo(UiText.Get("import.ui.quote"), QuoteKeys.Select(UiText.Get), Array.IndexOf(Quotes, _vm.Quote), i => _vm.Quote = Quotes[i]);
        _headerBox = new CheckBox { Content = UiText.Get("import.ui.header"), IsChecked = _vm.HasHeader, VerticalAlignment = VerticalAlignment.Bottom };
        _headerBox.Click += (_, _) => _vm.HasHeader = _headerBox.IsChecked == true;
        var options = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Children = { Field("import.ui.encoding", _encodingBox, 180), Field("import.ui.delimiter", _delimiterBox, 200), Field("import.ui.quote", _quoteBox, 160), _headerBox },
        };

        _previewHost = new Border { BorderBrush = Brush("SquillBorderBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
        var page = new Grid { RowSpacing = 16, RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto }, new RowDefinition() } };
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
            _fileName.Text = _vm.FilePath.Length == 0 ? UiText.Get("import.ui.noFile") : Path.GetFileName(_vm.FilePath);
            _fileInfo.Text = _vm.FilePath.Length == 0 ? "" : _vm.FileSummary + " · " + _vm.ColumnsText;
        }

        if (_previewHost is null)
            return;
        var grid = new Grid { ColumnSpacing = 16, RowSpacing = 6, Margin = new Thickness(12) };
        if (_vm.Preview is { } preview)
        {
            for (var c = 0; c < preview.ColumnNames.Count; c++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            const int shown = 8;
            for (var r = 0; r <= Math.Min(shown, preview.Sample.Count); r++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var c = 0; c < preview.ColumnNames.Count; c++)
            {
                var head = new TextBlock { Text = preview.ColumnNames[c], FontWeight = FontWeights.SemiBold, Foreground = Brush("SquillFgBrush"), TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(head, c);
                grid.Children.Add(head);
                for (var r = 0; r < Math.Min(shown, preview.Sample.Count); r++)
                {
                    var cell = new TextBlock { Text = c < preview.Sample[r].Length ? preview.Sample[r][c] ?? "" : "", FontFamily = _mono, FontSize = 12, Foreground = Brush("SquillFg2Brush"), TextTrimming = TextTrimming.CharacterEllipsis };
                    Grid.SetColumn(cell, c);
                    Grid.SetRow(cell, r + 1);
                    grid.Children.Add(cell);
                }
            }
        }

        _previewHost.Child = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollMode = ScrollMode.Auto, Content = grid };
    }

    // ---- step 2: target ---------------------------------------------------------------------------------------------

    private StackPanel TargetPage()
    {
        var names = _vm.Sessions.Select(s => s.Profile.Name).ToList();
        var connection = Combo(UiText.Get("import.ui.connection"), names, _vm.SelectedSession is { } selected ? _vm.Sessions.ToList().IndexOf(selected) : -1, i => _vm.SelectedSession = _vm.Sessions[i]);
        _tableBox = Combo(UiText.Get("import.ui.table"), [], -1, i => _vm.SelectedTable = _vm.TableNames[i]);
        _pillHost = new ContentControl { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 6) };
        var append = new TextBlock { Text = UiText.Get("import.append"), Foreground = Brush("SquillFg3Brush"), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 8) };
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { Field("import.ui.connection", connection, 300), _pillHost, Field("import.ui.table", _tableBox, 300), append },
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
        if (_pillHost is null)
            return;
        var profile = _vm.SelectedSession?.Profile;
        var label = ChromeFormat.Environment(_vm.Environment) is { } env ? UiText.Get(env.LabelKey) : "";
        _pillHost.Content = label;
        _pillHost.Style = ExplorerGlyphs.EnvironmentStyle(profile);
        _pillHost.Visibility = label.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---- step 3: mapping --------------------------------------------------------------------------------------------

    private Grid MappingPage()
    {
        var file = Card("SquillIconFolder", out var fileName, out var fileInfo);
        fileName.Text = Path.GetFileName(_vm.FilePath);
        fileInfo.Text = _vm.FileSummary + " · " + _vm.ColumnsText;
        var table = Card("SquillIconTable", out var tableName, out var tableInfo);
        tableName.Text = _vm.SelectedTable ?? "";
        tableInfo.Text = (_vm.SelectedSession?.Profile.Name ?? "") + " · " + UiText.Get("import.append");
        var cards = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition() } };
        cards.Children.Add(file);
        var arrow = Icons.Create("SquillIconArrowLeft", 18, Brush("SquillFg2Brush"));
        arrow.RenderTransform = new Microsoft.UI.Xaml.Media.ScaleTransform { ScaleX = -1 };
        arrow.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        Grid.SetColumn(arrow, 1);
        cards.Children.Add(arrow);
        Grid.SetColumn(table, 2);
        cards.Children.Add(table);

        var decimalBox = Combo(UiText.Get("import.ui.decimal"), DecimalKeys.Select(UiText.Get), Array.IndexOf(Decimals, _vm.DecimalSeparator), i => _vm.DecimalSeparator = Decimals[i]);
        var dateBox = new TextBox { Text = _vm.DateFormat, FontFamily = _mono, PlaceholderText = "yyyy-MM-dd" };
        dateBox.TextChanged += (_, _) => _vm.DateFormat = dateBox.Text;
        AutomationProperties.SetName(dateBox, UiText.Get("import.ui.dateFormat"));
        var zoneBox = new TextBox { Text = _vm.TimeZoneId, FontFamily = _mono, PlaceholderText = "Europe/Paris" };
        zoneBox.TextChanged += (_, _) => _vm.TimeZoneId = zoneBox.Text;
        AutomationProperties.SetName(zoneBox, UiText.Get("import.ui.timeZone"));
        var format = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Children = { Field("import.ui.decimal", decimalBox, 180), Field("import.ui.dateFormat", dateBox, 220), Field("import.ui.timeZone", zoneBox, 220) },
        };

        _warningText = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillWarningBrush"), VerticalAlignment = VerticalAlignment.Center };
        _warningBand = new Border { Background = Brush("SquillWarningSoftBrush"), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 10, 16, 10), Child = _warningText, Visibility = Visibility.Collapsed };
        _issuesText = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillDangerBrush"), Visibility = Visibility.Collapsed };

        var rows = new StackPanel();
        rows.Children.Add(HeaderRow());
        foreach (var row in _vm.Rows)
            rows.Children.Add(MapRow(row));
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Content = rows };
        var tableBorder = new Border { BorderBrush = Brush("SquillBorderBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Child = scroll };

        var top = new StackPanel { Spacing = 12, Children = { cards, format, _warningBand, _issuesText } };
        var page = new Grid { RowSpacing = 12, RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition() } };
        page.Children.Add(top);
        Grid.SetRow(tableBorder, 1);
        page.Children.Add(tableBorder);
        return page;
    }

    private static Grid MapGrid() => new()
    {
        ColumnSpacing = 12,
        Padding = new Thickness(12, 8, 12, 8),
        ColumnDefinitions =
        {
            new ColumnDefinition { Width = new GridLength(32) },
            new ColumnDefinition { Width = new GridLength(170) },
            new ColumnDefinition { Width = new GridLength(230) },
            new ColumnDefinition { Width = new GridLength(280) },
            new ColumnDefinition { Width = new GridLength(250) },
            new ColumnDefinition { Width = new GridLength(28) },
        },
    };

    private Grid HeaderRow()
    {
        var grid = MapGrid();
        string[] keys = ["import.ui.colSource", "import.ui.colPreview", "import.ui.colTarget", "import.ui.colConversion"];
        for (var i = 0; i < keys.Length; i++)
        {
            var head = new TextBlock { Text = UiText.Get(keys[i]), FontWeight = FontWeights.SemiBold, Foreground = Brush("SquillFg2Brush") };
            Grid.SetColumn(head, i + 1);
            grid.Children.Add(head);
        }

        return grid;
    }

    private Border MapRow(ImportMapRowViewModel row)
    {
        var grid = MapGrid();
        var check = new CheckBox { IsChecked = row.Enabled, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
        check.Click += (_, _) => row.Enabled = check.IsChecked == true;
        AutomationProperties.SetName(check, row.SourceName);
        var source = new TextBlock { Text = row.SourceName, FontFamily = _mono, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var preview = new TextBlock { Text = string.Join(" · ", row.Preview), FontFamily = _mono, FontSize = 12, Foreground = Brush("SquillFg3Brush"), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };

        // Plain strings only; the position in the list maps back to the table column.
        var choices = _vm.TargetChoices;
        var target = Combo(row.SourceName, choices.Prepend(UiText.Get("import.ui.ignore")), row.Target is null ? 0 : IndexOf(choices, row.Target) + 1,
            i => row.Target = i <= 0 ? null : choices[i - 1]);
        var conversion = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        var status = new TextBlock { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { conversion, status } };
        var mark = new ContentControl { VerticalAlignment = VerticalAlignment.Center };
        var border = new Border { BorderBrush = Brush("SquillBorderSubtleBrush"), BorderThickness = new Thickness(0, 1, 0, 0), Child = grid };
        UIElement[] cells = [check, source, preview, target, text, mark];
        for (var i = 0; i < cells.Length; i++)
        {
            Grid.SetColumn((FrameworkElement)cells[i], i);
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

            conversion.Text = row.Conversion;
            conversion.Foreground = Brush(row.Enabled ? "SquillFgBrush" : "SquillFg3Brush");
            status.Text = row.Status;
            status.Foreground = Brush(row.IsWarning ? "SquillWarningBrush" : "SquillFg3Brush");
            border.Background = row.IsWarning ? Brush("SquillWarningSoftBrush") : null;
            mark.Content = row.Enabled && row.Target is not null
                ? Icons.Create(row.IsWarning ? "SquillIconWarning" : "SquillIconCheck", 16, Brush(row.IsWarning ? "SquillWarningBrush" : "SquillSuccessBrush"))
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
            _warningBand.Visibility = _vm.WarningText.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        if (_issuesText is not null)
        {
            _issuesText.Text = string.Join(Environment.NewLine, _vm.Issues);
            _issuesText.Visibility = _vm.Issues.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    // ---- step 4: options --------------------------------------------------------------------------------------------

    private StackPanel OptionsPage()
    {
        var batch = new NumberBox { Value = _vm.BatchSize, Minimum = 1, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, SmallChange = 100, LargeChange = 1000 };
        batch.ValueChanged += (_, e) =>
        {
            if (!double.IsNaN(e.NewValue))
                _vm.BatchSize = (int)Math.Min(int.MaxValue, e.NewValue);
        };
        AutomationProperties.SetName(batch, UiText.Get("import.ui.batchSize"));
        var onError = Combo(UiText.Get("import.ui.onError"), new[] { UiText.Get("import.ui.onError.reject"), UiText.Get("import.ui.onError.stop") }, (int)_vm.OnError, i => _vm.OnError = (ImportErrorPolicy)i);
        var single = new CheckBox { Content = UiText.Get("import.ui.singleTransaction"), IsChecked = _vm.SingleTransaction };
        single.Click += (_, _) => _vm.SingleTransaction = single.IsChecked == true;
        _largeFileHint = new TextBlock { Text = UiText.Get("import.ui.largeFile"), Foreground = Brush("SquillWarningBrush"), TextWrapping = TextWrapping.Wrap };
        _rejectsBox = new TextBox { IsReadOnly = true, FontFamily = _mono };
        AutomationProperties.SetName(_rejectsBox, UiText.Get("import.ui.rejectsFile"));
        var chooseRejects = new Button { Content = UiText.Get("import.ui.chooseRejects"), Height = 30 };
        chooseRejects.Click += (_, _) => _ = _vm.ChooseRejectsCommand.ExecuteAsync(null);
        var rejects = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
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
            _largeFileHint.Visibility = _vm.LargeFileHint ? Visibility.Visible : Visibility.Collapsed;
        if (_rejectsBox is not null)
            _rejectsBox.Text = _vm.RejectsPath.Length == 0 ? _vm.DefaultRejectsPath : _vm.RejectsPath;
    }

    // ---- step 5: run ------------------------------------------------------------------------------------------------

    private StackPanel RunPage()
    {
        _progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 6 };
        _counts = new TextBlock { FontFamily = _mono, Foreground = Brush("SquillFgBrush") };
        _rate = new TextBlock { FontFamily = _mono, FontSize = 12, Foreground = Brush("SquillFg3Brush") };
        _outcome = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFgBrush"), FontWeight = FontWeights.SemiBold };
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
        _runActions.Visibility = _vm.Result is null ? Visibility.Collapsed : Visibility.Visible;
        _openRejects.Visibility = _vm.HasRejects ? Visibility.Visible : Visibility.Collapsed;
    }
}
