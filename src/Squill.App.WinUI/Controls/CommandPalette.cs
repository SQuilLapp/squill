using System.ComponentModel;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Squill.App.WinUI.Views;
using Squill.Presentation.Localization;
using Squill.Presentation.Palette;
using Windows.System;
using Windows.UI.Core;

namespace Squill.App.WinUI.Controls;

/// <summary>The command palette overlay (mockup M13): a dimmed scrim over the whole window and a centered card bound to a <see cref="PaletteViewModel"/>; it owns every key while open.</summary>
public sealed partial class CommandPalette : Grid
{
    private const int PageRows = 8;
    private enum Role
    {
        Caption,
        Muted,
        Footer,
        Icon,
    }

    private static readonly (PaletteMode Mode, string Prefix, string Key)[] Modes =
    [
        (PaletteMode.All, "", "palette.mode.all"), (PaletteMode.Actions, "> ", "palette.mode.actions"), (PaletteMode.Objects, "# ", "palette.mode.objects"),
        (PaletteMode.Line, ": ", "palette.mode.line"), (PaletteMode.History, "! ", "palette.mode.history"),
    ];

    private readonly PaletteViewModel _vm;
    private readonly Border _card;
    private readonly TextBox _text;
    private readonly StackPanel _pills = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private readonly StackPanel _rows = new();
    private readonly ScrollViewer _scroller;
    private readonly TextBlock _empty;
    private readonly TextBlock _footer;
    private readonly List<Border> _rowElements = [];
    private readonly Dictionary<Border, List<Run>> _matchRuns = [];
    private readonly List<Button> _pillButtons = [];
    private Control? _previousFocus;
    private bool _syncingText;

    /// <summary>Creates the overlay, collapsed until the view-model opens.</summary>
    /// <param name="vm">The palette state.</param>
    public CommandPalette(PaletteViewModel vm)
    {
        _vm = vm;
        Visibility = Visibility.Collapsed;
        Canvas.SetZIndex(this, 100);
        Grid.SetRowSpan(this, 4);
        Grid.SetColumnSpan(this, 6);
        Tapped += (_, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, this))
                _vm.Close();
        };
        _text = BuildTextBox();
        _empty = new TextBlock { FontSize = 13, Margin = new Thickness(14, 12, 14, 12), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        _footer = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        _scroller = new ScrollViewer { Content = _rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, IsTabStop = false, TabFocusNavigation = KeyboardNavigationMode.Cycle };
        AutomationProperties.SetName(_scroller, UiText.Get("palette.mode.all"));
        var layout = new Grid { RowDefinitions = { new() { Height = GridLength.Auto }, new() { Height = GridLength.Auto }, new() { Height = new GridLength(1, GridUnitType.Star) }, new() { Height = GridLength.Auto } } };
        var search = BuildSearchRow();
        BuildPills();
        var pillRow = new Border { Padding = new Thickness(12, 0, 12, 8), Child = _pills };
        Grid.SetRow(pillRow, 1);
        var list = new StackPanel { Children = { _empty, _scroller } };
        Grid.SetRow(list, 2);
        var footer = BuildFooter();
        Grid.SetRow(footer, 3);
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
            Margin = new Thickness(24, 0, 24, 0),
            Child = layout,
            Shadow = new ThemeShadow(),
            Translation = new System.Numerics.Vector3(0, 0, 32),
        };
        Children.Add(_card);
        SizeChanged += (_, _) => Fit();
        ActualThemeChanged += (_, _) => Repaint();
        PreviewKeyDown += OnPreviewKeyDown;
        _vm.PropertyChanged += OnViewModelChanged;
        Repaint();
    }

    /// <summary>Gets whether the palette is shown.</summary>
    public bool IsOpen => _vm.IsOpen;

    /// <summary>Closes the palette when it is open, opens it otherwise; used by <c>Ctrl+K</c> and the title bar box.</summary>
    /// <param name="open">Opens the palette; called only while it is closed.</param>
    public void Toggle(Action open)
    {
        if (_vm.IsOpen)
            _vm.Close();
        else
            open();
    }

    private TextBox BuildTextBox()
    {
        var box = new TextBox { BorderThickness = new Thickness(0), Padding = new Thickness(0), MinHeight = 0, FontSize = 15, VerticalAlignment = VerticalAlignment.Center, PlaceholderText = UiText.Get("search.placeholder") };
        foreach (var key in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused", "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused" })
            box.Resources[key] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        box.Resources["TextControlBorderThemeThicknessFocused"] = new Thickness(0);
        AutomationProperties.SetName(box, UiText.Get("search.placeholder"));
        box.TextChanged += (_, _) =>
        {
            if (!_syncingText)
                _vm.Text = box.Text;
        };
        return box;
    }

    private Grid BuildSearchRow()
    {
        var icon = Icons.Create("SquillIconSearch", 16, Tokens.Brush(this, "SquillFg3Brush"));
        icon.Margin = new Thickness(0, 0, 10, 0);
        icon.Tag = Role.Icon;
        var chip = new ContentControl { Content = UiText.Get("palette.key.escape"), Style = (Style)Application.Current.Resources["KeyboardChipStyle"], VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        var row = new Grid { Padding = new Thickness(16, 0, 14, 0), Height = 48, ColumnDefinitions = { new() { Width = GridLength.Auto }, new() { Width = new GridLength(1, GridUnitType.Star) }, new() { Width = GridLength.Auto } } };
        row.Children.Add(icon);
        Grid.SetColumn(_text, 1);
        row.Children.Add(_text);
        Grid.SetColumn(chip, 2);
        row.Children.Add(chip);
        return row;
    }

    private void BuildPills()
    {
        foreach (var (mode, prefix, key) in Modes)
        {
            var label = prefix + UiText.Get(key);
            var button = new Button { Content = label, Tag = mode, FontSize = 12, Padding = new Thickness(10, 2, 10, 2), MinHeight = 0, MinWidth = 0, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1) };
            AutomationProperties.SetName(button, label);
            var captured = mode;
            button.Click += (_, _) =>
            {
                _vm.SetMode(captured);
                FocusText();
            };
            _pillButtons.Add(button);
            _pills.Children.Add(button);
        }
    }

    private StackPanel BuildFooter()
    {
        var keys = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        keys.Text = $"↑ ↓ {UiText.Get("palette.footer.navigate")}    {UiText.Get("palette.footer.open")}    {UiText.Get("palette.footer.newTab")}";
        keys.Tag = Role.Footer;
        var grid = new Grid { Padding = new Thickness(16, 8, 16, 8), ColumnDefinitions = { new() { Width = new GridLength(1, GridUnitType.Star) }, new() { Width = GridLength.Auto } } };
        grid.Children.Add(keys);
        Grid.SetColumn(_footer, 1);
        grid.Children.Add(_footer);
        return new StackPanel { Children = { grid } };
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
                    _text.SelectionStart = _text.Text.Length;
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
        _previousFocus = FocusManager.GetFocusedElement(XamlRoot) as Control;
        Visibility = Visibility.Visible;
        Fit();
        Repaint();
        Rebuild();
        FocusText();
        _text.SelectAll();
    }

    private void Hide()
    {
        Visibility = Visibility.Collapsed;
        var back = _previousFocus;
        _previousFocus = null;
        back?.Focus(FocusState.Programmatic);
    }

    private void FocusText() => _text.Focus(FocusState.Programmatic);

    private void Fit()
    {
        _card.Margin = new Thickness(24, ActualHeight * 0.15, 24, 0);
        _scroller.MaxHeight = Math.Max(120, ActualHeight * 0.55);
    }

    private void Rebuild()
    {
        _rows.Children.Clear();
        _rowElements.Clear();
        _matchRuns.Clear();
        var index = 0;
        foreach (var section in _vm.Sections)
        {
            _rows.Children.Add(new TextBlock { Text = section.Caption, FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(16, 8, 16, 4), Tag = Role.Caption });
            foreach (var item in section.Items)
            {
                var row = BuildRow(item, index++);
                _rowElements.Add(row);
                _rows.Children.Add(row);
            }
        }

        _empty.Text = _vm.Hint ?? "";
        _empty.Visibility = _vm.Hint is null ? Visibility.Collapsed : Visibility.Visible;
        _scroller.Visibility = _vm.Hint is null ? Visibility.Visible : Visibility.Collapsed;
        Repaint();
    }

    private Border BuildRow(PaletteItem item, int index)
    {
        var mono = item.Kind is PaletteItemKind.SchemaObject or PaletteItemKind.History;
        var title = new TextBlock { FontSize = 13, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap, Tag = item };
        if (mono)
            title.FontFamily = (FontFamily)Application.Current.Resources["SquillFontMono"];
        var matches = FillTitle(title, item);
        var detail = new TextBlock { Text = item.Detail, FontSize = 12, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Tag = Role.Muted };
        var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { IconFor(item.IconKey), title, detail } };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (item.Right is { Length: > 0 } connection)
        {
            if (item.Environment is { } environment && ChromeFormat.Environment(environment) is { } env)
            {
                right.Children.Add(new Border
                {
                    Width = 8,
                    Height = 8,
                    CornerRadius = new CornerRadius(2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = (Brush)Application.Current.Resources[$"SquillEnv{env.Suffix}Brush"],
                });
            }

            right.Children.Add(new TextBlock { Text = connection, FontSize = 12, Tag = Role.Muted, VerticalAlignment = VerticalAlignment.Center });
        }
        else if (item.Shortcut is { Length: > 0 } shortcut)
        {
            right.Children.Add(new ContentControl { Content = shortcut, Style = (Style)Application.Current.Resources["KeyboardChipStyle"], VerticalAlignment = VerticalAlignment.Center });
        }

        var grid = new Grid { ColumnDefinitions = { new() { Width = new GridLength(1, GridUnitType.Star) }, new() { Width = GridLength.Auto } } };
        grid.Children.Add(left);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        var row = new Border { Height = 28, Padding = new Thickness(16, 0, 16, 0), Margin = new Thickness(6, 0, 6, 0), CornerRadius = new CornerRadius(6), Child = grid, Tag = item };
        _matchRuns[row] = matches;
        AutomationProperties.SetName(row, item.Detail.Length == 0 ? item.Title : $"{item.Title}, {item.Detail}");
        row.Tapped += (_, e) =>
        {
            _vm.MoveSelection(index - _vm.SelectedIndex);
            e.Handled = true;
            _ = RunAsync(PaletteRunMode.Default);
        };
        return row;
    }

    private static FrameworkElement IconFor(string key)
    {
        var resource = key switch
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
        var icon = Icons.Create(resource, 14, (Brush)Application.Current.Resources["SquillFg3Brush"]);
        icon.Margin = new Thickness(0, 0, 10, 0);
        icon.VerticalAlignment = VerticalAlignment.Center;
        icon.Tag = Role.Icon;
        return icon;
    }

    private static List<Run> FillTitle(TextBlock block, PaletteItem item)
    {
        var matches = new List<Run>();
        var at = 0;
        foreach (var (start, length) in item.Ranges.OrderBy(r => r.Start))
        {
            if (start < at || start + length > item.Title.Length)
                continue;
            if (start > at)
                block.Inlines.Add(new Run { Text = item.Title[at..start] });
            var match = new Run { Text = item.Title.Substring(start, length) };
            matches.Add(match);
            block.Inlines.Add(match);
            at = start + length;
        }

        if (at < item.Title.Length)
            block.Inlines.Add(new Run { Text = item.Title[at..] });
        return matches;
    }

    private void Repaint()
    {
        var scrim = Tokens.Color(this, "SquillFgBrush");
        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x73, scrim.R, scrim.G, scrim.B));
        _card.Background = Tokens.Brush(this, "SquillBgOverlayBrush");
        _card.BorderBrush = Tokens.Brush(this, "SquillBorderBrush");
        _text.Foreground = Tokens.Brush(this, "SquillFgBrush");
        _empty.Foreground = Tokens.Brush(this, "SquillFg3Brush");
        _footer.Foreground = Tokens.Brush(this, "SquillFg3Brush");
        PaintPills();
        PaintSelection();
        PaintStatic(_card);
    }

    private void PaintPills()
    {
        var current = PaletteQuery.Parse(_vm.Text).Mode;
        foreach (var button in _pillButtons)
        {
            var selected = (PaletteMode)button.Tag == current;
            button.Background = Tokens.Brush(this, selected ? "SquillAccentSoftBrush" : "SquillBgRaisedBrush");
            button.BorderBrush = Tokens.Brush(this, selected ? "SquillAccentBrush" : "SquillBorderBrush");
            button.Foreground = Tokens.Brush(this, selected ? "SquillAccentTextBrush" : "SquillFg2Brush");
        }
    }

    // Static texts and icons carry a Tag so one pass can recolor them after a theme change.
    private void PaintStatic(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            switch (child)
            {
                case TextBlock { Tag: Role.Caption or Role.Muted or Role.Footer } text:
                    text.Foreground = Tokens.Brush(this, "SquillFg3Brush");
                    break;
                case Viewbox { Tag: Role.Icon } icon:
                    Icons.SetStroke(icon, Tokens.Brush(this, "SquillFg3Brush"));
                    break;
            }

            PaintStatic(child);
        }
    }

    private void PaintSelection()
    {
        for (var i = 0; i < _rowElements.Count; i++)
        {
            var row = _rowElements[i];
            var item = (PaletteItem)row.Tag;
            var selected = i == _vm.SelectedIndex;
            row.Background = selected ? Tokens.Brush(this, "SquillAccentSoftBrush") : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            var fg = Tokens.Brush(this, item.IsEnabled ? "SquillFgBrush" : "SquillFg3Brush");
            var accent = Tokens.Brush(this, item.IsEnabled ? "SquillAccentTextBrush" : "SquillFg3Brush");
            var title = FindTitle((Grid)row.Child);
            title.Foreground = fg;
            foreach (var run in title.Inlines.OfType<Run>())
                run.Foreground = _matchRuns[row].Contains(run) ? accent : fg;
            if (selected)
                row.StartBringIntoView();
        }
    }

    private static TextBlock FindTitle(Grid grid) => ((StackPanel)grid.Children[0]).Children.OfType<TextBlock>().First(t => t.Tag is PaletteItem);

    private async Task RunAsync(PaletteRunMode mode) => await _vm.RunSelectedAsync(mode);

    private static bool Down(VirtualKey key) => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

    // Everything is handled here, on the tunnelling event: nothing below the palette sees a key while it is open.
    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_vm.IsOpen)
            return;
        var ctrl = Down(VirtualKey.Control);
        var onButton = e.OriginalSource is Button;
        switch (e.Key)
        {
            case VirtualKey.Escape:
                _vm.Close();
                break;
            case VirtualKey.Down:
                _vm.MoveSelection(1);
                break;
            case VirtualKey.Up:
                _vm.MoveSelection(-1);
                break;
            case VirtualKey.PageDown:
                _vm.MoveSelection(PageRows);
                break;
            case VirtualKey.PageUp:
                _vm.MoveSelection(-PageRows);
                break;
            case VirtualKey.Enter when !onButton || ctrl:
                _ = RunAsync(ctrl ? PaletteRunMode.NewTab : PaletteRunMode.Default);
                break;
            case VirtualKey.Tab:
                CycleFocus(Down(VirtualKey.Shift) ? -1 : 1);
                break;
            case VirtualKey.F5:
                break;
            case VirtualKey.Enter or VirtualKey.Space when onButton:
                return;
            default:
                // Text editing chords stay with the text box; any other Ctrl chord is swallowed so a window accelerator (Ctrl+T, Ctrl+W...) cannot fire behind the scrim.
                if (!ctrl || e.Key is VirtualKey.A or VirtualKey.C or VirtualKey.V or VirtualKey.X or VirtualKey.Z or VirtualKey.Y
                    or VirtualKey.Left or VirtualKey.Right or VirtualKey.Back or VirtualKey.Delete or VirtualKey.Home or VirtualKey.End or VirtualKey.Control or VirtualKey.Shift)
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
        var current = stops.FindIndex(s => ReferenceEquals(FocusManager.GetFocusedElement(XamlRoot), s));
        stops[(((current < 0 ? 0 : current) + direction) % stops.Count + stops.Count) % stops.Count].Focus(FocusState.Keyboard);
    }
}
