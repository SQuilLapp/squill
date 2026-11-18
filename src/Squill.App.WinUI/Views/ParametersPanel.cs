using System.ComponentModel;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Squill.App.WinUI.Controls;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Windows.System;

namespace Squill.App.WinUI.Views;

/// <summary>The query parameters prompt shown under the editor while a statement waits for its values.</summary>
internal sealed partial class ParametersPanel : UserControl
{
    private readonly StackPanel _rows = new() { Spacing = 4 };
    private readonly TextBlock _title = new() { Text = UiText.Get("editor.parameters"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly TextBlock _count = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _ignore = new() { Style = (Style)Application.Current.Resources["GhostButtonStyle"], Content = UiText.Get("editor.ignore") };
    private readonly Button _run = new() { Style = (Style)Application.Current.Resources["PrimaryButtonStyle"], Content = UiText.Get("editor.runWithValues") };
    private readonly List<(TextBlock Error, TextBox Box)> _controls = [];
    private QueryTabViewModel? _tab;

    /// <summary>Creates the hidden panel.</summary>
    public ParametersPanel()
    {
        Padding = new Thickness(12, 8, 12, 8);
        BorderThickness = new Thickness(0, 1, 0, 0);
        Visibility = Visibility.Collapsed;
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        header.Children.Add(_title);
        header.Children.Add(_count);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(_ignore);
        buttons.Children.Add(_run);
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(header);
        stack.Children.Add(_rows);
        stack.Children.Add(buttons);
        Content = stack;
        PreviewKeyDown += OnPreviewKeyDown;
        Paint();
    }

    /// <summary>Follows another tab, or none when <see langword="null"/>.</summary>
    /// <param name="tab">The tab whose parameters prompt this panel shows.</param>
    public void Attach(QueryTabViewModel? tab)
    {
        if (_tab is not null)
            _tab.PropertyChanged -= OnTabPropertyChanged;
        _tab = tab;
        if (tab is not null)
            tab.PropertyChanged += OnTabPropertyChanged;
        _ignore.Command = tab?.IgnoreParametersCommand;
        _run.Command = tab?.RunParametersCommand;
        Refresh();
    }

    /// <summary>Paints the panel with the brushes of the theme it is shown in.</summary>
    public void Paint()
    {
        Background = Tokens.Brush(this, "SquillBgSurfaceBrush");
        BorderBrush = Tokens.Brush(this, "SquillBorderBrush");
        _count.Foreground = Tokens.Brush(this, "SquillFg3Brush");
        foreach (var (error, _) in _controls)
            error.Foreground = Tokens.Brush(this, "SquillDangerBrush");
    }

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueryTabViewModel.IsParametersPanelOpen))
            Refresh();
    }

    private void Refresh()
    {
        var open = _tab?.IsParametersPanelOpen == true;
        Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        if (!open || _tab is null)
            return;
        _count.Text = _tab.ParametersHeader;
        _rows.Children.Clear();
        _controls.Clear();
        foreach (var row in _tab.ParameterRows)
            _rows.Children.Add(BuildRow(row));
        Paint();
        var first = _controls.Select(c => c.Box).FirstOrDefault(b => b.IsEnabled && b.Text.Length == 0) ?? _controls.Select(c => c.Box).FirstOrDefault();
        first?.Focus(FocusState.Programmatic);
    }

    private StackPanel BuildRow(ParameterRowViewModel row)
    {
        var label = new TextBlock { Text = $":{row.Name}", VerticalAlignment = VerticalAlignment.Center, Style = (Style)Application.Current.Resources["MonoTextBlockStyle"] };
        var box = new TextBox { Text = row.Text ?? "", FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SquillFontMono"], IsEnabled = row.IsValueEnabled };
        var isNull = new CheckBox { Content = UiText.Get("editor.passNull"), IsChecked = row.IsNull, MinWidth = 0 };
        var error = new TextBlock { FontSize = 12, Margin = new Thickness(128, 0, 0, 0) };
        void Sync()
        {
            error.Text = row.Error ?? "";
            error.Visibility = row.HasError ? Visibility.Visible : Visibility.Collapsed;
            box.IsEnabled = row.IsValueEnabled;
        }

        box.TextChanged += (_, _) => row.Text = box.Text;
        isNull.Click += (_, _) => row.IsNull = isNull.IsChecked == true;
        row.PropertyChanged += (_, _) => Sync();
        Sync();
        var line = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(120) }, new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        Grid.SetColumn(box, 1);
        Grid.SetColumn(isNull, 2);
        line.Children.Add(label);
        line.Children.Add(box);
        line.Children.Add(isNull);
        _controls.Add((error, box));
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(line);
        stack.Children.Add(error);
        return stack;
    }

    // Escape = Ignore; Ctrl+Enter in a value field = Run with these values.
    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_tab is null)
            return;
        if (e.Key == VirtualKey.Escape)
        {
            _tab.IgnoreParametersCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Enter && InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        {
            if (_tab.RunParametersCommand.CanExecute(null))
                _tab.RunParametersCommand.Execute(null);
            e.Handled = true;
        }
    }
}
