using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using Squill.Presentation.Editing;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;

namespace Squill.App.Avalonia.Views;

/// <summary>The pane beside the result grid that shows the full value of the selected cell, indented when it is JSON, and applies an edit to the cell.</summary>
public sealed class CellPreviewPanel : Border
{
    private readonly TextBlock _title = new() { FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _type = new() { FontSize = 12, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _text = new() { AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.NoWrap, BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(0) };
    private readonly TextBlock _error = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 4) };
    private readonly Button _format = new() { Classes = { "ghost" }, Height = 26, MinHeight = 26, Content = UiText.Get("preview.format") };
    private readonly Button _apply = new() { Classes = { "primary" }, Height = 26, MinHeight = 26, Content = UiText.Get("preview.apply") };
    private readonly Button _close = new() { Classes = { "ghost" }, Height = 26, MinHeight = 26, Content = UiText.Get("preview.close") };
    private readonly ProgressBar _working = new() { IsIndeterminate = true, Height = 2, IsVisible = false };
    private CellPreviewViewModel? _model;
    private bool _syncing;

    /// <summary>Creates the hidden pane.</summary>
    public CellPreviewPanel()
    {
        IsVisible = false;
        BorderThickness = new Thickness(1, 0, 0, 0);
        this.Bind(BackgroundProperty, this.GetResourceObservable("SquillBgSurfaceBrush"));
        this.Bind(BorderBrushProperty, this.GetResourceObservable("SquillBorderSubtleBrush"));
        _title.Bind(TextBlock.ForegroundProperty, _title.GetResourceObservable("SquillFgBrush"));
        _type.Bind(TextBlock.ForegroundProperty, _type.GetResourceObservable("SquillFg3Brush"));
        _error.Bind(TextBlock.ForegroundProperty, _error.GetResourceObservable("SquillDangerBrush"));
        _text.Bind(TemplatedControl.BackgroundProperty, _text.GetResourceObservable("SquillBgBaseBrush"));
        _text.Bind(TemplatedControl.ForegroundProperty, _text.GetResourceObservable("SquillFgBrush"));
        _text.Bind(TemplatedControl.FontFamilyProperty, _text.GetResourceObservable("SquillFontMono"));

        var header = new DockPanel { Margin = new Thickness(12, 6, 6, 6), LastChildFill = true };
        DockPanel.SetDock(_close, Dock.Right);
        header.Children.Add(_close);
        var names = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(_title);
        names.Children.Add(_type);
        header.Children.Add(names);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6, Margin = new Thickness(6) };
        footer.Children.Add(_format);
        footer.Children.Add(_apply);

        var bottom = new StackPanel();
        bottom.Children.Add(_error);
        bottom.Children.Add(footer);

        var root = new DockPanel();
        AutomationProperties.SetName(_working, UiText.Get("preview.working"));
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(_working, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(_working);
        root.Children.Add(bottom);
        root.Children.Add(_text);
        Child = root;

        _text.TextChanged += (_, _) =>
        {
            if (_model is not null && !_syncing && _model.Text != (_text.Text ?? ""))
                _model.Text = _text.Text ?? "";
        };
        // Replaces the built-in paste so a long text can show the busy popup before the insert blocks the UI thread.
        _text.PastingFromClipboard += (_, e) =>
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

    private async Task PasteAsync()
    {
        string? text;
        try
        {
            text = TopLevel.GetTopLevel(_text)?.Clipboard is { } clipboard ? await clipboard.TryGetTextAsync() : null;
        }
        catch (Exception)
        {
            return;
        }

        if (text is null)
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
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            _text.SelectedText = text;
        }
        finally
        {
            BusyChanged?.Invoke(this, false);
        }
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
                if ((_text.Text ?? "") != m.Text)
                    _text.Text = m.Text;
                _text.IsReadOnly = !m.CanEdit;
                _error.Text = m.Error;
                _error.IsVisible = m.HasError;
                _format.IsVisible = m.ShowFormat;
                _format.IsEnabled = m.IsJson;
                _apply.IsVisible = m.ShowApply;
                _apply.IsEnabled = m.ApplyCommand.CanExecute(null);
                _working.IsVisible = m.IsWorking;
            }
            finally
            {
                _syncing = false;
            }
        }

        if (IsVisible != open)
        {
            IsVisible = open;
            OpenChanged?.Invoke(this, open);
        }
    }
}
