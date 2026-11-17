using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;

namespace Squill.App.Avalonia.Views;

/// <summary>The strip between the results header and the grid: filter chips, the <c>+ Filter</c> button and the search box. It only draws <see cref="ResultViewState"/>.</summary>
public sealed partial class FilterBar : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private ResultViewState? _state;
    private FilterEditorViewModel? _editor;
    private bool _syncing;

    /// <summary>Creates the bar, hidden until <see cref="Attach"/> gives it a state.</summary>
    public FilterBar()
    {
        InitializeComponent();
        AutomationProperties.SetName(AddFilter, UiText.Get("results.filter.add"));
        AutomationProperties.SetName(Search, UiText.Get("results.filter.search.placeholder"));
        AutomationProperties.SetName(ClearSearch, UiText.Get("results.filter.clear"));
        Search.TextChanged += OnSearchTextChanged;
        Search.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Search.Text = "";
        };
        ClearSearch.Click += (_, _) => Search.Text = "";
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            if (_state is not null)
                _state.SearchText = Search.Text ?? "";
        };
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

        _timer.Stop();
        _state = state;
        IsVisible = state is not null;
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
        if (IsVisible)
            Search.Focus();
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
        AddFilter.IsEnabled = !blocked;
        Search.IsEnabled = !blocked || !string.IsNullOrEmpty(Search.Text);
        ToolTip.SetShowOnDisabled(AddFilter, true);
        ToolTip.SetShowOnDisabled(Search, true);
        ToolTip.SetTip(AddFilter, blocked ? _state!.BlockedReason : null);
        ToolTip.SetTip(Search, blocked ? _state!.BlockedReason : null);
    }

    // The view-model is the source of truth: it clears the text when the filters are cleared.
    private void ShowSearch()
    {
        var text = _state?.SearchText ?? "";
        if ((Search.Text ?? "") != text)
        {
            _syncing = true;
            Search.Text = text;
            _syncing = false;
        }

        ShowSearchAdornments();
    }

    private void ShowSearchAdornments()
    {
        var empty = string.IsNullOrEmpty(Search.Text);
        Shortcut.IsVisible = empty;
        ClearSearch.IsVisible = !empty;
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ShowSearchAdornments();
        if (_syncing || _state is null)
            return;
        _timer.Stop();
        _timer.Start();
    }

    private void ShowChips()
    {
        Chips.Children.Clear();
        if (_state is null)
            return;
        foreach (var chip in _state.Filters)
            Chips.Children.Add(BuildChip(chip));
    }

    private Border BuildChip(FilterChip chip)
    {
        var text = new TextBlock { Text = chip.Text, MaxWidth = 360, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(text, chip.Text);
        var funnel = new AvaloniaPath { Width = 12, Height = 12, VerticalAlignment = VerticalAlignment.Center };
        funnel.Classes.Add("icon");
        funnel.Bind(AvaloniaPath.DataProperty, this.GetResourceObservable("SquillIconFunnel"));
        var close = new AvaloniaPath { Width = 10, Height = 10 };
        close.Classes.Add("icon");
        close.Bind(AvaloniaPath.DataProperty, this.GetResourceObservable("SquillIconClose"));
        var remove = new Button { Width = 20, Height = 20, MinHeight = 20, MinWidth = 20, Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Center, Content = close };
        remove.Classes.Add("icon");
        ToolTip.SetTip(remove, UiText.Get("results.filter.remove"));
        AutomationProperties.SetName(remove, UiText.Get("results.filter.remove") + " " + chip.Text);
        remove.Click += (_, _) => _state?.RemoveFilter(chip);
        var border = new Border { Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { funnel, text, remove } } };
        border.Classes.Add("filter-chip");
        return border;
    }

    private void OnFlyoutOpening(object? sender, EventArgs e)
    {
        var flyout = (Flyout)sender!;
        _editor?.Dispose();
        _editor = _state?.OpenFilterEditor();
        flyout.Content = _editor is null ? null : BuildEditor(_editor, flyout);
    }

    private void OnFlyoutClosed(object? sender, EventArgs e)
    {
        _editor?.Dispose();
        _editor = null;
        ((Flyout)sender!).Content = null;
    }

    private StackPanel BuildEditor(FilterEditorViewModel editor, Flyout flyout)
    {
        var column = new ComboBox { ItemsSource = editor.Columns, SelectedItem = editor.SelectedColumn, HorizontalAlignment = HorizontalAlignment.Stretch };
        var op = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        var value = new TextBox { Text = editor.OperandText, PlaceholderText = UiText.Get("results.filter.editor.value") };
        var error = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, IsVisible = false };
        error.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("SquillDangerBrush"));
        var apply = new Button { Content = UiText.Get("results.filter.editor.apply"), Height = 28, MinHeight = 28 };
        apply.Classes.Add("primary");
        var cancel = new Button { Content = UiText.Get("dialog.cancel"), Height = 28, MinHeight = 28 };
        column.ItemTemplate = new FuncDataTemplate<ColumnChoice>((c, _) => new TextBlock { Text = c?.Name });
        op.ItemTemplate = new FuncDataTemplate<OperatorChoice>((c, _) => new TextBlock { Text = c?.Text });
        AutomationProperties.SetName(column, UiText.Get("results.filter.editor.column"));
        AutomationProperties.SetName(op, UiText.Get("results.filter.editor.operator"));
        AutomationProperties.SetName(value, UiText.Get("results.filter.editor.value"));

        void ShowOperators()
        {
            op.ItemsSource = editor.Operators;
            op.SelectedItem = editor.SelectedOperator;
        }

        void ShowState()
        {
            value.IsVisible = editor.NeedsOperand;
            error.Text = editor.Error ?? "";
            error.IsVisible = editor.Error is not null;
            apply.IsEnabled = editor.CanChange;
            ToolTip.SetTip(apply, editor.BlockedReason);
            if (!editor.IsOpen)
                flyout.Hide();
        }

        ShowOperators();
        ShowState();
        column.SelectionChanged += (_, _) =>
        {
            if (column.SelectedItem is ColumnChoice choice)
                editor.SelectedColumn = choice;
        };
        op.SelectionChanged += (_, _) =>
        {
            if (op.SelectedItem is OperatorChoice choice)
                editor.SelectedOperator = choice;
        };
        value.TextChanged += (_, _) => editor.OperandText = value.Text ?? "";
        editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FilterEditorViewModel.Operators))
                ShowOperators();
            else if (e.PropertyName == nameof(FilterEditorViewModel.SelectedOperator))
                op.SelectedItem = editor.SelectedOperator;
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
                new TextBlock { Text = UiText.Get("results.filter.editor.title"), FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = UiText.Get("results.filter.editor.column"), FontSize = 12 },
                column,
                new TextBlock { Text = UiText.Get("results.filter.editor.operator"), FontSize = 12 },
                op,
                value,
                error,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, apply } },
            },
        };
        panel.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && editor.CanChange)
            {
                editor.OperandText = value.Text ?? "";
                editor.ApplyCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                editor.CancelCommand.Execute(null);
                e.Handled = true;
            }
        };
        return panel;
    }
}
