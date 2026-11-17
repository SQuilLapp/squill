using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Squill.Core;
using Squill.Presentation.Localization;
using Squill.Presentation.Palette;

namespace Squill.App.Avalonia.Controls;

/// <summary>The command palette overlay (mockup M13): a dimmed scrim over the whole window and a centered card bound to a <see cref="PaletteViewModel"/>; it owns every key while open.</summary>
public sealed class CommandPalette : Grid
{
    private const int PageRows = 8;
    private const string Muted = "SquillFg3Brush";

    private static readonly (PaletteMode Mode, string Prefix, string Key)[] Modes =
    [
        (PaletteMode.All, "", "palette.mode.all"), (PaletteMode.Actions, "> ", "palette.mode.actions"), (PaletteMode.Objects, "# ", "palette.mode.objects"),
        (PaletteMode.Line, ": ", "palette.mode.line"), (PaletteMode.History, "! ", "palette.mode.history"),
    ];

    private readonly PaletteViewModel _vm;
    private readonly Border _scrim = new() { Opacity = 0.45 };
    private readonly Border _card;
    private readonly TextBox _text;
    private readonly StackPanel _rows = new();
    private readonly ScrollViewer _scroller;
    private readonly TextBlock _empty = new() { FontSize = 13, Margin = new Thickness(14, 12), TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly TextBlock _footer = new() { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
    private readonly List<Border> _rowElements = [];
    private readonly List<Button> _pillButtons = [];
    private readonly List<Action> _paint = [];
    private readonly HashSet<Run> _matchRuns = [];
    private IInputElement? _previousFocus;
    private bool _syncingText;

    /// <summary>Creates the overlay, hidden until the view-model opens.</summary>
    /// <param name="vm">The palette state.</param>
    public CommandPalette(PaletteViewModel vm)
    {
        _vm = vm;
        IsVisible = false;
        ZIndex = 100;
        SetRowSpan(this, 4);
        SetColumnSpan(this, 6);
        _scrim.PointerPressed += (_, e) =>
        {
            _vm.Close();
            e.Handled = true;
        };
        _text = BuildTextBox();
        _scroller = new ScrollViewer { Content = _rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false };
        AutomationProperties.SetName(_scroller, UiText.Get("palette.mode.all"));
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
        var search = BuildSearchRow();
        var pillRow = new Border { Padding = new Thickness(12, 0, 12, 8), Child = BuildPills() };
        SetRow(pillRow, 1);
        var list = new StackPanel { Children = { _empty, _scroller } };
        SetRow(list, 2);
        var footer = BuildFooter();
        SetRow(footer, 3);
        layout.Children.Add(search);
        layout.Children.Add(pillRow);
        layout.Children.Add(list);
        layout.Children.Add(footer);
        _card = new Border
        {
            MaxWidth = 880,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(24, 0),
            Child = layout,
        };
        Children.Add(_scrim);
        Children.Add(_card);
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        _vm.PropertyChanged += OnViewModelChanged;
        ActualThemeVariantChanged += (_, _) =>
        {
            Repaint();
            Rebuild();
        };
        Repaint();
    }

    private IBrush? Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        _card.Margin = new Thickness(24, arrangeSize.Height * 0.15, 24, 0);
        _scroller.MaxHeight = Math.Max(120, arrangeSize.Height * 0.55);
        return base.ArrangeOverride(arrangeSize);
    }

    private TextBox BuildTextBox()
    {
        var box = new TextBox { BorderThickness = new Thickness(0), Padding = new Thickness(0), MinHeight = 0, FontSize = 15, VerticalAlignment = VerticalAlignment.Center, PlaceholderText = UiText.Get("search.placeholder") };
        foreach (var key in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused", "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused" })
            box.Resources[key] = Brushes.Transparent;
        box.Background = Brushes.Transparent;
        AutomationProperties.SetName(box, UiText.Get("search.placeholder"));
        _paint.Add(() => box.Foreground = Brush("SquillFgBrush"));
        box.TextChanged += (_, _) =>
        {
            if (!_syncingText)
                _vm.Text = box.Text ?? "";
        };
        return box;
    }

    private Grid BuildSearchRow()
    {
        var icon = Icon("SquillIconSearch", 16);
        icon.Margin = new Thickness(0, 0, 10, 0);
        var chip = Kbd(UiText.Get("palette.key.escape"));
        chip.Margin = new Thickness(8, 0, 0, 0);
        var row = new Grid { Margin = new Thickness(16, 0, 14, 0), Height = 48, ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        row.Children.Add(icon);
        SetColumn(_text, 1);
        row.Children.Add(_text);
        SetColumn(chip, 2);
        row.Children.Add(chip);
        return row;
    }

    private static Border Kbd(string text) => new() { Classes = { "kbd" }, Child = new TextBlock { Text = text } };

    private StackPanel BuildPills()
    {
        var pills = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var (mode, prefix, key) in Modes)
        {
            var label = prefix + UiText.Get(key);
            var button = new Button { Content = label, Tag = mode, FontSize = 12, Padding = new Thickness(10, 2), MinHeight = 0, MinWidth = 0, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1) };
            AutomationProperties.SetName(button, label);
            var captured = mode;
            button.Click += (_, _) =>
            {
                _vm.SetMode(captured);
                _text.Focus();
            };
            _pillButtons.Add(button);
            pills.Children.Add(button);
        }

        return pills;
    }

    private Grid BuildFooter()
    {
        var keys = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        keys.Text = $"↑ ↓ {UiText.Get("palette.footer.navigate")}    {UiText.Get("palette.footer.open")}    {UiText.Get("palette.footer.newTab")}";
        _paint.Add(() => keys.Foreground = Brush(Muted));
        var grid = new Grid { Margin = new Thickness(16, 8), ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(keys);
        SetColumn(_footer, 1);
        grid.Children.Add(_footer);
        return grid;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PaletteViewModel.IsOpen):
                if (_vm.IsOpen)
                    Show();
                else
                    Hide();
                break;
            case nameof(PaletteViewModel.Text):
                if (_text.Text != _vm.Text)
                {
                    _syncingText = true;
                    _text.Text = _vm.Text;
                    _text.CaretIndex = _vm.Text.Length;
                    _syncingText = false;
                }

                PaintPills();
                break;
            case nameof(PaletteViewModel.Sections):
            case nameof(PaletteViewModel.Hint):
                Rebuild();
                break;
            case nameof(PaletteViewModel.SelectedIndex):
                PaintSelection();
                break;
            case nameof(PaletteViewModel.FooterText):
                _footer.Text = _vm.FooterText;
                break;
        }
    }

    private void Show()
    {
        // The focused element is remembered before the card takes focus, so Esc hands it back (editor, grid or explorer).
        _previousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        IsVisible = true;
        Repaint();
        Rebuild();
        _text.Focus();
        _text.SelectAll();
    }

    private void Hide()
    {
        IsVisible = false;
        var back = _previousFocus;
        _previousFocus = null;
        back?.Focus();
    }

    private void Rebuild()
    {
        _rows.Children.Clear();
        _rowElements.Clear();
        _matchRuns.Clear();
        var index = 0;
        foreach (var section in _vm.Sections)
        {
            _rows.Children.Add(new TextBlock { Text = section.Caption, FontSize = 11, FontWeight = FontWeight.SemiBold, Margin = new Thickness(16, 8, 16, 4), Foreground = Brush(Muted) });
            foreach (var item in section.Items)
            {
                var row = BuildRow(item, index++);
                _rowElements.Add(row);
                _rows.Children.Add(row);
            }
        }

        _empty.Text = _vm.Hint ?? "";
        _empty.IsVisible = _vm.Hint is not null;
        _scroller.IsVisible = _vm.Hint is null;
        PaintSelection();
    }

    private Border BuildRow(PaletteItem item, int index)
    {
        var mono = item.Kind is PaletteItemKind.SchemaObject or PaletteItemKind.History;
        var title = new TextBlock { FontSize = 13, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap, Tag = item };
        if (mono && this.TryFindResource("SquillFontMono", ActualThemeVariant, out var font) && font is FontFamily family)
            title.FontFamily = family;
        FillTitle(title, item);
        var detail = new TextBlock { Text = item.Detail, FontSize = 12, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = Brush(Muted) };
        var icon = Icon(IconResource(item.IconKey), 14);
        icon.Margin = new Thickness(0, 0, 10, 0);
        var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { icon, title, detail } };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (item.Right is { Length: > 0 } connection)
        {
            if (EnvironmentBrush(item.Environment) is { } env)
                right.Children.Add(new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center, Background = env });
            right.Children.Add(new TextBlock { Text = connection, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush(Muted) });
        }
        else if (item.Shortcut is { Length: > 0 } shortcut)
        {
            right.Children.Add(Kbd(shortcut));
        }

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(left);
        SetColumn(right, 1);
        grid.Children.Add(right);
        var row = new Border { Height = 28, Padding = new Thickness(16, 0), Margin = new Thickness(6, 0), CornerRadius = new CornerRadius(6), Child = grid, Tag = item };
        AutomationProperties.SetName(row, item.Detail.Length == 0 ? item.Title : $"{item.Title}, {item.Detail}");
        row.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
                return;
            _vm.MoveSelection(index - _vm.SelectedIndex);
            e.Handled = true;
            _ = RunAsync(PaletteRunMode.Default);
        };
        return row;
    }

    private IBrush? EnvironmentBrush(ConnectionEnvironment? environment) => environment switch
    {
        ConnectionEnvironment.Production => Brush("SquillEnvProdBrush"),
        ConnectionEnvironment.Staging => Brush("SquillEnvStagingBrush"),
        ConnectionEnvironment.Development => Brush("SquillEnvDevBrush"),
        ConnectionEnvironment.Local => Brush("SquillEnvLocalBrush"),
        _ => null,
    };

    private static string IconResource(string key) => key switch
    {
        "table" => "SquillIconTable",
        "view" => "SquillIconView",
        "function" or "procedure" or "routine" => "SquillIconRoutine",
        "trigger" => "SquillIconTrigger",
        "history" => "SquillIconHistory",
        "line" => "SquillIconLine",
        "check" => "SquillIconCheck",
        _ => "SquillIconAction",
    };

    private global::Avalonia.Controls.Shapes.Path Icon(string resource, double size)
    {
        var path = new global::Avalonia.Controls.Shapes.Path { Classes = { "icon" }, Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };
        if (this.TryFindResource(resource, ActualThemeVariant, out var geometry) && geometry is Geometry data)
            path.Data = data;
        path.Stroke = Brush(Muted);
        return path;
    }

    private void FillTitle(TextBlock block, PaletteItem item)
    {
        var inlines = block.Inlines ??= new InlineCollection();
        var at = 0;
        foreach (var (start, length) in item.Ranges.OrderBy(r => r.Start))
        {
            if (start < at || start + length > item.Title.Length)
                continue;
            if (start > at)
                inlines.Add(new Run(item.Title[at..start]));
            var match = new Run(item.Title.Substring(start, length));
            _matchRuns.Add(match);
            inlines.Add(match);
            at = start + length;
        }

        if (at < item.Title.Length)
            inlines.Add(new Run(item.Title[at..]));
    }

    private void Repaint()
    {
        _scrim.Background = Brush("SquillFgBrush");
        _card.Background = Brush("SquillBgOverlayBrush");
        _card.BorderBrush = Brush("SquillBorderBrush");
        _card.BoxShadow = this.TryFindResource("SquillShadow3", ActualThemeVariant, out var shadow) && shadow is BoxShadows shadows ? shadows : default;
        _empty.Foreground = Brush(Muted);
        _footer.Foreground = Brush(Muted);
        foreach (var paint in _paint)
            paint();
        PaintPills();
    }

    private void PaintPills()
    {
        var current = _vm.Mode;
        foreach (var button in _pillButtons)
        {
            var selected = (PaletteMode)button.Tag! == current;
            button.Background = Brush(selected ? "SquillAccentSoftBrush" : "SquillBgRaisedBrush");
            button.BorderBrush = Brush(selected ? "SquillAccentBrush" : "SquillBorderBrush");
            button.Foreground = Brush(selected ? "SquillAccentTextBrush" : "SquillFg2Brush");
        }
    }

    private void PaintSelection()
    {
        for (var i = 0; i < _rowElements.Count; i++)
        {
            var row = _rowElements[i];
            var item = (PaletteItem)row.Tag!;
            var selected = i == _vm.SelectedIndex;
            row.Background = selected ? Brush("SquillAccentSoftBrush") : Brushes.Transparent;
            var fg = Brush(item.IsEnabled ? "SquillFgBrush" : "SquillFg3Brush");
            var accent = Brush(item.IsEnabled ? "SquillAccentTextBrush" : "SquillFg3Brush");
            var title = (TextBlock)((StackPanel)((Grid)row.Child!).Children[0]).Children[1];
            title.Foreground = fg;
            foreach (var run in title.Inlines!.OfType<Run>())
                run.Foreground = _matchRuns.Contains(run) ? accent : fg;
            if (selected)
                row.BringIntoView();
        }
    }

    private async Task RunAsync(PaletteRunMode mode) => await _vm.RunSelectedAsync(mode);

    // Everything is handled here, on the tunnelling event: nothing below the palette sees a key while it is open.
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_vm.IsOpen)
            return;
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        var onButton = e.Source is Button;
        switch (e.Key)
        {
            case Key.Escape:
                _vm.Close();
                break;
            case Key.Down:
                _vm.MoveSelection(1);
                break;
            case Key.Up:
                _vm.MoveSelection(-1);
                break;
            case Key.PageDown:
                _vm.MoveSelection(PageRows);
                break;
            case Key.PageUp:
                _vm.MoveSelection(-PageRows);
                break;
            case Key.Enter when !onButton || ctrl:
                _ = RunAsync(ctrl ? PaletteRunMode.NewTab : PaletteRunMode.Default);
                break;
            case Key.Tab:
                CycleFocus(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
                break;
            case Key.F5:
                break;
            case Key.Enter or Key.Space when onButton:
                return;
            default:
                // Text editing chords stay with the text box; any other Ctrl chord is swallowed so a window shortcut (Ctrl+T, Ctrl+W...) cannot fire behind the scrim.
                if (!ctrl || e.Key is Key.A or Key.C or Key.V or Key.X or Key.Z or Key.Y or Key.Left or Key.Right or Key.Back or Key.Delete or Key.Home or Key.End
                    or Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
                    return;
                break;
        }

        e.Handled = true;
    }

    // Focus cycle: text box, then the pills, then the text box again.
    private void CycleFocus(int direction)
    {
        var stops = new List<Control> { _text };
        stops.AddRange(_pillButtons);
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        var current = stops.FindIndex(s => ReferenceEquals(focused, s));
        stops[(((current < 0 ? 0 : current) + direction) % stops.Count + stops.Count) % stops.Count].Focus();
    }
}
