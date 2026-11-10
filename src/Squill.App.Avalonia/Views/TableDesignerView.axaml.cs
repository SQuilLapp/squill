using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Squill.App.Avalonia.Editors;
using Squill.Presentation.Designer;

namespace Squill.App.Avalonia.Views;

/// <summary>The editing surface of a table designer: header, column, index and foreign key tabs, highlighted script preview and issues.</summary>
public sealed partial class TableDesignerView : UserControl
{
    private readonly AvaloniaSqlEditor _script = new() { IsReadOnly = true, WordWrap = true };
    private TableDesignerViewModel? _bound;

    /// <summary>Creates the view.</summary>
    public TableDesignerView()
    {
        InitializeComponent();
        _script.UseCompactLayout();
        ScriptHost.Child = _script.Control;
        // Focusing a cell of a row selects that row, so the toolbar and the detail card follow the keyboard and the pointer.
        AddHandler(GotFocusEvent, OnRowFocused, RoutingStrategies.Bubble, handledEventsToo: true);
        DataContextChanged += (_, _) => Rebind();
        // The columns table fills the visible width and scrolls sideways only below its minimum width.
        ColumnsScroller.SizeChanged += (_, e) => ColumnsTable.Width = Math.Max(ColumnsTable.MinWidth, e.NewSize.Width);
    }

    private void Rebind()
    {
        if (_bound is not null)
            _bound.PropertyChanged -= OnDesignerChanged;
        _bound = DataContext as TableDesignerViewModel;
        if (_bound is null)
            return;
        _bound.PropertyChanged += OnDesignerChanged;
        _script.SetDialect(_bound.Session.Profile.Dialect);
        ShowScript();
    }

    private void OnDesignerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TableDesignerViewModel.Script))
            ShowScript();
    }

    private void ShowScript()
    {
        _script.Text = _bound?.Script ?? "";
    }

    private void OnRowFocused(object? sender, RoutedEventArgs e)
    {
        if (_bound is not null && e.Source is Control { DataContext: ColumnRowViewModel row })
            _bound.SelectedColumn = row;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e is { Key: Key.F2, Handled: false } && _bound?.SelectedColumn is { } selected
            && ColumnRows.ContainerFromItem(selected) is { } container
            && container.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() is { } box)
        {
            box.Focus();
            box.SelectAll();
            e.Handled = true;
        }
    }

    private async void OnCopyScriptClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(_bound?.Script ?? "");
    }
}
