using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;
using Windows.System;

namespace Squill.App.WinUI.Views;

/// <summary>The strip between the results header and the grid: filter chips, the <c>+ Filter</c> button and the search box. It only draws <see cref="ResultViewState"/>.</summary>
internal sealed partial class FilterBar : UserControl
{
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(150);

    private readonly Border _frame = new() { Padding = new Thickness(8, 0, 8, 0), BorderThickness = new Thickness(0, 0, 0, 1) };
    private readonly StackPanel _chips = new() { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _add = new() { Height = 26, MinHeight = 26, Padding = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, Content = UiText.Get("results.filter.add") };
    private readonly TextBox _search = new() { Height = 27, MinHeight = 27, Width = 220, Padding = new Thickness(28, 3, 64, 3), PlaceholderText = UiText.Get("results.filter.search.placeholder"), VerticalAlignment = VerticalAlignment.Center };
    private readonly ContentControl _shortcut = new() { Content = "Ctrl+F", Style = Res("KeyboardChipStyle"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), IsHitTestVisible = false };
    private readonly Button _clear = new() { Style = Res("IconButtonStyle"), Width = 22, Height = 22, MinHeight = 22, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 3, 0), Visibility = Visibility.Collapsed };
    private readonly Flyout _flyout = new() { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
    private readonly List<(FrameworkElement Icon, string Brush)> _icons = [];
    private ResultViewState? _state;
    private FilterEditorViewModel? _editor;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _timer;
    private bool _syncing;

    public FilterBar()
    {
        Height = 36;
        Visibility = Visibility.Collapsed;
        var searchIcon = Track(Icons.Create("SquillIconSearch", 13, Brush("SquillFg3Brush")), "SquillFg3Brush");
        searchIcon.HorizontalAlignment = HorizontalAlignment.Left;
        searchIcon.VerticalAlignment = VerticalAlignment.Center;
        searchIcon.Margin = new Thickness(9, 0, 0, 0);
        searchIcon.IsHitTestVisible = false;
        _clear.Content = Track(Icons.Create("SquillIconClose", 10, Brush("SquillFg2Brush")), "SquillFg2Brush");
        var box = new Grid { Children = { _search, searchIcon, _shortcut, _clear } };
        var scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalAlignment = VerticalAlignment.Center, Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _chips, _add } } };
        Grid.SetColumn(box, 1);
        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } }, ColumnSpacing = 8, Children = { scroll, box } };
        Content = _frame;
        _frame.Child = row;

        ToolTipService.SetToolTip(_clear, UiText.Get("results.filter.clear"));
        AutomationProperties.SetName(_add, UiText.Get("results.filter.add"));
        AutomationProperties.SetName(_search, UiText.Get("results.filter.search.placeholder"));
        AutomationProperties.SetName(_clear, UiText.Get("results.filter.clear"));
        _add.Flyout = _flyout;
        _flyout.Opening += OnFlyoutOpening;
        _flyout.Closed += OnFlyoutClosed;
        _search.TextChanged += OnSearchTextChanged;
        _clear.Click += (_, _) => _search.Text = "";
        _search.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Escape)
                _search.Text = "";
        };
        Loaded += (_, _) => Paint();
        ActualThemeChanged += (_, _) => Paint();
    }

    /// <summary>Binds the bar to the view state of a grid result, or hides it for <see langword="null"/>.</summary>
    /// <param name="state">The state to draw.</param>
    public void Attach(ResultViewState? state)
    {
        if (_state is not null)
        {
            _state.PropertyChanged -= OnStateChanged;
            _state.Filters.CollectionChanged -= OnFiltersChanged;
        }

        _timer?.Stop();
        _state = state;
        Visibility = state is null ? Visibility.Collapsed : Visibility.Visible;
        if (state is not null)
        {
            state.PropertyChanged += OnStateChanged;
            state.Filters.CollectionChanged += OnFiltersChanged;
        }

        ShowSearch();
        ShowChips();
        ShowBlocked();
    }

    /// <summary>Moves the focus to the search box.</summary>
    public void FocusSearch()
    {
        if (Visibility == Visibility.Visible)
            _search.Focus(FocusState.Keyboard);
    }

    private static Style Res(string key) => (Style)Application.Current.Resources[key];

    private static Microsoft.UI.Xaml.Media.Brush Brush(string key) => Tokens.Brush(key);

    private FrameworkElement Track(FrameworkElement icon, string brush)
    {
        _icons.Add((icon, brush));
        return icon;
    }

    private void Paint()
    {
        _frame.BorderBrush = Tokens.Brush(this, "SquillBorderSubtleBrush");
        _frame.Background = Tokens.Brush(this, "SquillBgBaseBrush");
        foreach (var (icon, brush) in _icons)
            Icons.SetStroke(icon, Tokens.Brush(this, brush));
        ShowChips();
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ResultViewState.CanChange))
            ShowBlocked();
        else if (e.PropertyName is nameof(ResultViewState.SearchText))
            ShowSearch();
    }

    private void OnFiltersChanged(object? sender, NotifyCollectionChangedEventArgs e) => ShowChips();

    private void ShowBlocked()
    {
        var blocked = _state?.CanChange == false;
        _add.IsEnabled = !blocked;
        _search.IsEnabled = !blocked || _search.Text.Length > 0;
        ToolTipService.SetToolTip(_add, blocked ? _state!.BlockedReason : null);
        ToolTipService.SetToolTip(_search, blocked ? _state!.BlockedReason : null);
    }

    // The view-model is the source of truth: it clears the text when the filters are cleared.
    private void ShowSearch()
    {
        var text = _state?.SearchText ?? "";
        if (_search.Text != text)
        {
            _syncing = true;
            _search.Text = text;
            _syncing = false;
        }

        ShowSearchAdornments();
    }

    private void ShowSearchAdornments()
    {
        var empty = _search.Text.Length == 0;
        _shortcut.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        _clear.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ShowSearchAdornments();
        if (_syncing || _state is null)
            return;
        _timer ??= CreateTimer();
        _timer.Stop();
        _timer.Start();
    }

    private Microsoft.UI.Dispatching.DispatcherQueueTimer CreateTimer()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = SearchDelay;
        timer.IsRepeating = false;
        timer.Tick += (_, _) =>
        {
            if (_state is not null)
                _state.SearchText = _search.Text;
        };
        return timer;
    }

    private void ShowChips()
    {
        _chips.Children.Clear();
        if (_state is null)
            return;
        foreach (var chip in _state.Filters)
            _chips.Children.Add(BuildChip(chip));
    }

    private Border BuildChip(FilterChip chip)
    {
        var accent = Tokens.Brush(this, "SquillAccentTextBrush");
        var funnel = Icons.Create("SquillIconFunnel", 12, accent);
        funnel.VerticalAlignment = VerticalAlignment.Center;
        var text = new TextBlock { Text = chip.Text, Style = Res("MonoTextBlockStyle"), FontSize = 12, Foreground = accent, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 360, TextTrimming = TextTrimming.CharacterEllipsis };
        ToolTipService.SetToolTip(text, chip.Text);
        var remove = new Button { Style = Res("IconButtonStyle"), Width = 20, Height = 20, MinHeight = 20, MinWidth = 20, VerticalAlignment = VerticalAlignment.Center, Content = Icons.Create("SquillIconClose", 10, accent) };
        ToolTipService.SetToolTip(remove, UiText.Get("results.filter.remove"));
        AutomationProperties.SetName(remove, UiText.Get("results.filter.remove") + " " + chip.Text);
        remove.Click += (_, _) => _state?.RemoveFilter(chip);
        return new Border
        {
            Height = 24,
            Padding = new Thickness(8, 0, 3, 0),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            BorderBrush = Tokens.Brush(this, "SquillAccentLineBrush"),
            Background = Tokens.Brush(this, "SquillAccentSoftBrush"),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { funnel, text, remove } },
        };
    }

    private void OnFlyoutOpening(object? sender, object e)
    {
        _editor?.Dispose();
        _editor = _state?.OpenFilterEditor();
        _flyout.Content = _editor is null ? null : BuildEditor(_editor);
    }

    private void OnFlyoutClosed(object? sender, object e)
    {
        _editor?.Dispose();
        _editor = null;
        _flyout.Content = null;
    }

    private static int IndexOf<T>(IReadOnlyList<T> items, T? selected) where T : class
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], selected))
                return i;
        }

        return -1;
    }

    private StackPanel BuildEditor(FilterEditorViewModel editor)
    {
        // The combo boxes list plain strings and map the index back: an object ItemsSource with DisplayMemberPath does not survive Native AOT.
        var column = new ComboBox { ItemsSource = editor.Columns.Select(c => c.Name).ToArray(), SelectedIndex = IndexOf(editor.Columns, editor.SelectedColumn), HorizontalAlignment = HorizontalAlignment.Stretch, Header = UiText.Get("results.filter.editor.column") };
        var op = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, Header = UiText.Get("results.filter.editor.operator") };
        var value = new TextBox { Header = UiText.Get("results.filter.editor.value"), Text = editor.OperandText };
        var error = new TextBlock { Foreground = Tokens.Brush(this, "SquillDangerBrush"), TextWrapping = TextWrapping.Wrap, FontSize = 12, Visibility = Visibility.Collapsed };
        var apply = new Button { Style = Res("PrimaryButtonStyle"), Content = UiText.Get("results.filter.editor.apply"), Height = 28, MinHeight = 28 };
        var cancel = new Button { Content = UiText.Get("dialog.cancel"), Height = 28, MinHeight = 28 };
        AutomationProperties.SetName(column, UiText.Get("results.filter.editor.column"));
        AutomationProperties.SetName(op, UiText.Get("results.filter.editor.operator"));
        AutomationProperties.SetName(value, UiText.Get("results.filter.editor.value"));

        void ShowOperators()
        {
            op.ItemsSource = editor.Operators.Select(o => o.Text).ToArray();
            op.SelectedIndex = IndexOf(editor.Operators, editor.SelectedOperator);
        }

        void ShowState()
        {
            value.Visibility = editor.NeedsOperand ? Visibility.Visible : Visibility.Collapsed;
            error.Text = editor.Error ?? "";
            error.Visibility = editor.Error is null ? Visibility.Collapsed : Visibility.Visible;
            apply.IsEnabled = editor.CanChange;
            ToolTipService.SetToolTip(apply, editor.BlockedReason);
            if (!editor.IsOpen)
                _flyout.Hide();
        }

        ShowOperators();
        ShowState();
        column.SelectionChanged += (_, _) =>
        {
            if (column.SelectedIndex >= 0 && column.SelectedIndex < editor.Columns.Count)
                editor.SelectedColumn = editor.Columns[column.SelectedIndex];
        };
        op.SelectionChanged += (_, _) =>
        {
            if (op.SelectedIndex >= 0 && op.SelectedIndex < editor.Operators.Count)
                editor.SelectedOperator = editor.Operators[op.SelectedIndex];
        };
        value.TextChanged += (_, _) => editor.OperandText = value.Text;
        editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FilterEditorViewModel.Operators))
                ShowOperators();
            else if (e.PropertyName == nameof(FilterEditorViewModel.SelectedOperator))
                op.SelectedIndex = IndexOf(editor.Operators, editor.SelectedOperator);
            ShowState();
        };
        apply.Click += (_, _) => editor.ApplyCommand.Execute(null);
        cancel.Click += (_, _) => editor.CancelCommand.Execute(null);
        var panel = new StackPanel
        {
            Width = 280,
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = UiText.Get("results.filter.editor.title"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                column,
                op,
                value,
                error,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, apply } },
            },
        };
        panel.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Enter && editor.CanChange)
            {
                editor.OperandText = value.Text;
                editor.ApplyCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Escape)
            {
                editor.CancelCommand.Execute(null);
                e.Handled = true;
            }
        };
        return panel;
    }
}
