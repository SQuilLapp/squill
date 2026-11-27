using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Squill.Presentation.Editing;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;
using Windows.ApplicationModel.DataTransfer;

namespace Squill.App.WinUI.Views;

/// <summary>The pane beside the result grid that shows the full value of the selected cell, indented when it is JSON, and applies an edit to the cell.</summary>
public sealed partial class CellPreviewPanel : UserControl
{
    private readonly TextBlock _title = new() { FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _type = new() { FontSize = 12, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _text = new()
    {
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        BorderThickness = new Thickness(0),
        CornerRadius = new CornerRadius(0),
        FontFamily = (FontFamily)Application.Current.Resources["SquillFontMono"],
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
    };

    private readonly TextBlock _error = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 4, 12, 4) };
    private readonly TextBlock _notice = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(12, 4, 12, 4), Visibility = Visibility.Collapsed };
    private readonly Button _format = new() { Height = 26, MinHeight = 26, Content = UiText.Get("preview.format") };
    private readonly Button _apply = new() { Style = (Style)Application.Current.Resources["PrimaryButtonStyle"], Height = 26, MinHeight = 26, Content = UiText.Get("preview.apply") };
    private readonly Button _close = new() { Height = 26, MinHeight = 26, Content = UiText.Get("preview.close") };
    private readonly Border _frame = new() { BorderThickness = new Thickness(1, 0, 0, 0) };
    private readonly ProgressBar _working = new() { IsIndeterminate = true, Height = 2, Visibility = Visibility.Collapsed };
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

        var root = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto }, new RowDefinition(), new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto } } };
        AutomationProperties.SetName(_working, UiText.Get("preview.working"));
        Grid.SetRow(_working, 1);
        Grid.SetRow(_text, 2);
        Grid.SetRow(_notice, 3);
        Grid.SetRow(_error, 4);
        Grid.SetRow(footer, 5);
        root.Children.Add(header);
        root.Children.Add(_working);
        root.Children.Add(_text);
        root.Children.Add(_notice);
        root.Children.Add(_error);
        root.Children.Add(footer);
        _frame.Child = root;
        Content = _frame;
        Paint();
        Loaded += (_, _) => Paint();
        ActualThemeChanged += (_, _) => Paint();

        _text.TextChanged += (_, _) =>
        {
            if (_model is not null && !_syncing && _model.Text != _text.Text)
                _model.Text = _text.Text;
        };
        // While only the start of a long value is shown, copying all the shown text copies the whole value.
        _text.CopyingToClipboard += (_, e) =>
        {
            if (_model is { IsTruncated: true, FullValue: { } full } && _text.SelectionLength == _text.Text.Length)
            {
                e.Handled = true;
                var package = new DataPackage();
                package.SetText(full);
                Clipboard.SetContent(package);
            }
        };
        _text.Paste += (_, e) =>
        {
            e.Handled = true;
            _ = PasteAsync();
        };
        _close.Click += (_, _) => _model?.CloseCommand.Execute(null);
        _format.Click += (_, _) => _model?.FormatCommand.Execute(null);
        _apply.Click += (_, _) => _model?.ApplyCommand.Execute(null);
    }

    /// <summary>Raised when the pane is shown or hidden; the argument tells which.</summary>
    public event EventHandler<bool>? OpenChanged;

    /// <summary>Raised when a long paste into the pane starts (<see langword="true"/>) and ends (<see langword="false"/>).</summary>
    public event EventHandler<bool>? BusyChanged;

    // Replaces the built-in paste so a long text can show the busy popup before the native insert blocks the UI thread.
    private async Task PasteAsync()
    {
        string text;
        try
        {
            var content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.Text))
            {
                return;
            }

            text = await content.GetTextAsync();
        }
        catch (Exception)
        {
            return;
        }

        if (!LongPaste.IsLong(text))
        {
            _text.SelectedText = text;
            return;
        }

        BusyChanged?.Invoke(this, true);
        try
        {
            // Lets the popup lay out and render before the native insert.
            await Task.Delay(1);
            _text.SelectedText = text;
        }
        finally
        {
            BusyChanged?.Invoke(this, false);
        }
    }

    // The element's ActualTheme decides: the pane is built before the settings pick the theme.
    private void Paint()
    {
        _title.Foreground = Tokens.Brush(this, "SquillFgBrush");
        _type.Foreground = Tokens.Brush(this, "SquillFg3Brush");
        _notice.Foreground = Tokens.Brush(this, "SquillFg3Brush");
        _error.Foreground = Tokens.Brush(this, "SquillDangerBrush");
        _text.Background = Tokens.Brush(this, "SquillBgBaseBrush");
        _text.Foreground = Tokens.Brush(this, "SquillFgBrush");
        _frame.Background = Tokens.Brush(this, "SquillBgSurfaceBrush");
        _frame.BorderBrush = Tokens.Brush(this, "SquillBorderSubtleBrush");
    }

    /// <summary>Shows the preview of another result, or nothing when <see langword="null"/>.</summary>
    /// <param name="model">The preview view-model of the result on screen.</param>
    public void Attach(CellPreviewViewModel? model)
    {
        _model?.ViewChanged -= OnModelChanged;
        _model = model;
        model?.ViewChanged += OnModelChanged;
        Sync();
    }

    private void OnModelChanged(object? sender, EventArgs e) => Sync();

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
                if (CellPreviewViewModel.NormalizeLineBreaks(_text.Text) != CellPreviewViewModel.NormalizeLineBreaks(m.Text))
                    _text.Text = m.Text;
                _text.IsReadOnly = !m.CanEdit || m.IsTruncated;
                _notice.Text = m.Notice ?? "";
                ToolTipService.SetToolTip(_notice, m.Notice);
                _notice.Visibility = m.Notice is null ? Visibility.Collapsed : Visibility.Visible;
                _error.Text = m.Error ?? "";
                _error.Visibility = m.HasError ? Visibility.Visible : Visibility.Collapsed;
                _format.Visibility = m.ShowFormat ? Visibility.Visible : Visibility.Collapsed;
                _format.Content = m.FormatLabel;
                _format.IsEnabled = m.CanFormat;
                _apply.Visibility = m.ShowApply ? Visibility.Visible : Visibility.Collapsed;
                _apply.IsEnabled = m.ApplyCommand.CanExecute(null);
                _working.Visibility = m.IsWorking ? Visibility.Visible : Visibility.Collapsed;
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
