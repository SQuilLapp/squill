using System.ComponentModel;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;

namespace Squill.App.WinUI.Views;

/// <summary>The pane beside the result grid that shows the full value of the selected cell, indented when it is JSON, and applies an edit to the cell.</summary>
public sealed partial class CellPreviewPanel : UserControl
{
    private readonly TextBlock _title = new() { FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("SquillFgBrush") };
    private readonly TextBlock _type = new() { FontSize = 12, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("SquillFg3Brush") };
    private readonly TextBox _text = new()
    {
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        BorderThickness = new Thickness(0),
        CornerRadius = new CornerRadius(0),
        FontFamily = (FontFamily)Application.Current.Resources["SquillFontMono"],
        Background = Brush("SquillBgBaseBrush"),
        Foreground = Brush("SquillFgBrush"),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
    };

    private readonly TextBlock _error = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 4, 12, 4), Foreground = Brush("SquillDangerBrush") };
    private readonly Button _format = new() { Height = 26, MinHeight = 26, Content = UiText.Get("preview.format") };
    private readonly Button _apply = new() { Style = (Style)Application.Current.Resources["PrimaryButtonStyle"], Height = 26, MinHeight = 26, Content = UiText.Get("preview.apply") };
    private readonly Button _close = new() { Height = 26, MinHeight = 26, Content = UiText.Get("preview.close") };
    private CellPreviewViewModel? _model;
    private bool _syncing;

    /// <summary>Creates the hidden pane.</summary>
    public CellPreviewPanel()
    {
        Visibility = Visibility.Collapsed;
        ScrollViewer.SetHorizontalScrollBarVisibility(_text, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(_text, ScrollBarVisibility.Auto);

        var header = new Grid { Margin = new Thickness(12, 6, 6, 6), ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        var names = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(_title);
        names.Children.Add(_type);
        Grid.SetColumn(_close, 1);
        header.Children.Add(names);
        header.Children.Add(_close);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6, Margin = new Thickness(6) };
        footer.Children.Add(_format);
        footer.Children.Add(_apply);

        var root = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition(), new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto } } };
        Grid.SetRow(_text, 1);
        Grid.SetRow(_error, 2);
        Grid.SetRow(footer, 3);
        root.Children.Add(header);
        root.Children.Add(_text);
        root.Children.Add(_error);
        root.Children.Add(footer);
        Content = new Border
        {
            BorderThickness = new Thickness(1, 0, 0, 0),
            Background = Brush("SquillBgSurfaceBrush"),
            BorderBrush = Brush("SquillBorderSubtleBrush"),
            Child = root,
        };

        _text.TextChanged += (_, _) =>
        {
            if (_model is not null && !_syncing && _model.Text != _text.Text)
                _model.Text = _text.Text;
        };
        _close.Click += (_, _) => _model?.CloseCommand.Execute(null);
        _format.Click += (_, _) => _model?.FormatCommand.Execute(null);
        _apply.Click += (_, _) => _model?.ApplyCommand.Execute(null);
    }

    /// <summary>Raised when the pane is shown or hidden; the argument tells which.</summary>
    public event EventHandler<bool>? OpenChanged;

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    /// <summary>Shows the preview of another result, or nothing when <see langword="null"/>.</summary>
    /// <param name="model">The preview view-model of the result on screen.</param>
    public void Attach(CellPreviewViewModel? model)
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
            _model.ApplyCommand.CanExecuteChanged -= OnApplyChanged;
        }

        _model = model;
        if (model is not null)
        {
            model.PropertyChanged += OnModelChanged;
            model.ApplyCommand.CanExecuteChanged += OnApplyChanged;
        }

        Sync();
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e) => Sync();

    private void OnApplyChanged(object? sender, EventArgs e) => Sync();

    private void Sync()
    {
        var open = _model?.IsOpen == true;
        if (_model is { } m)
        {
            _syncing = true;
            try
            {
                _title.Text = m.Title;
                _type.Text = m.TypeText;
                if (_text.Text != m.Text)
                    _text.Text = m.Text;
                _text.IsReadOnly = !m.CanEdit;
                _error.Text = m.Error ?? "";
                _error.Visibility = string.IsNullOrEmpty(m.Error) ? Visibility.Collapsed : Visibility.Visible;
                _format.Visibility = m.IsJson || m.CanEdit ? Visibility.Visible : Visibility.Collapsed;
                _format.IsEnabled = m.IsJson;
                _apply.Visibility = m.CanEdit ? Visibility.Visible : Visibility.Collapsed;
                _apply.IsEnabled = m.ApplyCommand.CanExecute(null);
            }
            finally
            {
                _syncing = false;
            }
        }

        var visibility = open ? Visibility.Visible : Visibility.Collapsed;
        if (Visibility != visibility)
        {
            Visibility = visibility;
            OpenChanged?.Invoke(this, open);
        }
    }
}
