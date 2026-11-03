using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Squill.Core;
using Squill.Core.Design;
using Squill.App.WinUI.Editors;
using Squill.Presentation.Designer;
using Squill.Presentation.Editor;
using Squill.Presentation.Localization;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Squill.App.WinUI.Views;

/// <summary>The editing UI of a <see cref="TableDesignerViewModel"/>: header, columns, indexes, foreign keys, highlighted script preview and issues.</summary>
public sealed partial class TableDesignerView : UserControl
{
    private readonly ISqlEditor _script = EditorFactory.Create();
    private TableDesignerViewModel? _viewModel;

    /// <summary>Creates the view.</summary>
    public TableDesignerView()
    {
        InitializeComponent();
        var editor = (IEditorControl)_script;
        editor.IsReadOnly = true;
        ScriptHost.Child = editor.Control;
        ToolTipService.SetToolTip(CopyScriptButton, UiText.Get("designer.copyScript"));
        ToolTipService.SetToolTip(OpenScriptButton, UiText.Get("designer.openScript"));
        ToolTipService.SetToolTip(MoveUpButton, UiText.Get("designer.moveUp"));
        ToolTipService.SetToolTip(MoveDownButton, UiText.Get("designer.moveDown"));
    }

    /// <summary>The referential actions offered by the foreign key combo boxes.</summary>
    public static IReadOnlyList<ReferentialAction> Actions { get; } = Enum.GetValues<ReferentialAction>();

    /// <summary>Gets the visibility of the error badge of an issue.</summary>
    /// <param name="severity">The severity of the issue.</param>
    /// <returns>Visible for an error.</returns>
    public static Visibility ErrorVisibility(DesignIssueSeverity severity) =>
        TableDesignerFormat.IsError(severity) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets the visibility of the warning badge of an issue.</summary>
    /// <param name="severity">The severity of the issue.</param>
    /// <returns>Visible for anything that is not an error.</returns>
    public static Visibility WarningVisibility(DesignIssueSeverity severity) =>
        TableDesignerFormat.IsWarning(severity) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets or sets the designer shown; the view rebinds and highlights the rows named by error issues.</summary>
    public TableDesignerViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            if (_viewModel is not null)
                _viewModel.PropertyChanged -= OnViewModelChanged;
            _viewModel = value;
            if (value is not null)
                value.PropertyChanged += OnViewModelChanged;
            Bindings.Update();
            ShowHeader();
            ShowScript();
            ShowIssues();
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TableDesignerViewModel.Issues):
                ShowIssues();
                break;
            case nameof(TableDesignerViewModel.SelectedColumn):
                RefreshRowErrors();
                SelectedCard.Visibility = _viewModel?.SelectedColumn is null ? Visibility.Collapsed : Visibility.Visible;
                break;
            case nameof(TableDesignerViewModel.DraftText):
                DraftPanel.Visibility = TableDesignerFormat.HasDraft(_viewModel?.DraftText) ? Visibility.Visible : Visibility.Collapsed;
                break;
            case nameof(TableDesignerViewModel.Script):
                ShowScript();
                break;
        }
    }

    // The empty issue list of the view-model is an empty array, which the XAML items source marshalling rejects (E_INVALIDARG); a List is accepted.
    private void ShowIssues()
    {
        IssueList.ItemsSource = _viewModel?.Issues.ToList();
        RefreshRowErrors();
    }

    private void ShowHeader()
    {
        if (_viewModel is not { } vm)
            return;
        DraftPanel.Visibility = TableDesignerFormat.HasDraft(vm.DraftText) ? Visibility.Visible : Visibility.Collapsed;
        SelectedCard.Visibility = vm.SelectedColumn is null ? Visibility.Collapsed : Visibility.Visible;
        EnvPill.Content = vm.EnvironmentLabel;
        EnvPill.Visibility = vm.EnvironmentLabel is null ? Visibility.Collapsed : Visibility.Visible;
        var style = vm.Environment switch
        {
            Core.ConnectionEnvironment.Production => "BadgeEnvProdStyle",
            Core.ConnectionEnvironment.Staging => "BadgeEnvStagingStyle",
            Core.ConnectionEnvironment.Development => "BadgeEnvDevStyle",
            _ => "BadgeEnvLocalStyle",
        };
        EnvPill.Style = (Style)Application.Current.Resources[style];
    }

    private void ShowScript()
    {
        if (_viewModel is null)
            return;
        _script.SetDialect(_viewModel.Session.Profile.Dialect);
        _script.Text = _viewModel.Script;
    }

    private void RefreshRowErrors()
    {
        foreach (var rows in new[] { ColumnRows, IndexRows, ForeignKeyRows })
        {
            foreach (var child in rows.ItemsPanelRoot?.Children.OfType<ContentPresenter>() ?? [])
            {
                if (VisualTreeHelper.GetChildrenCount(child) > 0 && VisualTreeHelper.GetChild(child, 0) is Grid row)
                    ShowRowError(row);
            }
        }
    }

    private void ShowRowError(Grid row)
    {
        var name = row.DataContext switch
        {
            ColumnRowViewModel c => c.Name,
            IndexRowViewModel i => i.Name,
            ForeignKeyRowViewModel f => f.Name,
            _ => null,
        };
        var failed = name is not null && _viewModel is not null
            && _viewModel.Issues.Any(i => i.Severity == DesignIssueSeverity.Error && string.Equals(i.Subject, name, StringComparison.OrdinalIgnoreCase));
        ((Border)row.Children[0]).Visibility = failed ? Visibility.Visible : Visibility.Collapsed;
        if (row.DataContext is ColumnRowViewModel column)
            ((Border)row.Children[1]).Visibility = ReferenceEquals(column, _viewModel?.SelectedColumn) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnRowGotFocus(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && ((FrameworkElement)sender).DataContext is ColumnRowViewModel column)
            _viewModel.SelectedColumn = column;
    }

    private void OnViewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_viewModel is not { } vm)
            return;
        var alt = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        switch (e.Key)
        {
            case VirtualKey.Insert when ctrl:
                vm.AddColumnCommand.Execute(null);
                break;
            case VirtualKey.Up when alt && vm.SelectedColumn is { } up:
                vm.MoveColumnUpCommand.Execute(up);
                break;
            case VirtualKey.Down when alt && vm.SelectedColumn is { } down:
                vm.MoveColumnDownCommand.Execute(down);
                break;
            case VirtualKey.F2 when vm.SelectedColumn is { } selected && ColumnRows.ContainerFromItem(selected) is DependencyObject container:
                if (FirstTextBox(container) is { } box)
                {
                    box.Focus(FocusState.Keyboard);
                    box.SelectAll();
                }

                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private static TextBox? FirstTextBox(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TextBox box)
                return box;
            if (FirstTextBox(child) is { } found)
                return found;
        }

        return null;
    }

    private void OnCopyScriptClicked(object sender, RoutedEventArgs e)
    {
        var data = new DataPackage();
        data.SetText(_viewModel?.Script ?? "");
        Clipboard.SetContent(data);
    }

    private void OnRowLoaded(object sender, RoutedEventArgs e) => ShowRowError((Grid)sender);

    private void OnTypeGotFocus(object sender, RoutedEventArgs e) => FillSuggestions((AutoSuggestBox)sender);

    private void OnTypeTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            FillSuggestions(sender);
    }

    private void FillSuggestions(AutoSuggestBox box)
    {
        if (_viewModel is not null)
            box.ItemsSource = _viewModel.TypeSuggestions.Where(t => t.Contains(box.Text, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void OnMoveColumnUp(object sender, RoutedEventArgs e) => _viewModel?.MoveColumnUpCommand.Execute(Row(sender));

    private void OnMoveColumnDown(object sender, RoutedEventArgs e) => _viewModel?.MoveColumnDownCommand.Execute(Row(sender));

    private void OnRemoveColumn(object sender, RoutedEventArgs e) => _viewModel?.RemoveColumnCommand.Execute(Row(sender));

    private void OnRemoveIndex(object sender, RoutedEventArgs e) => _viewModel?.RemoveIndexCommand.Execute(Row(sender));

    private void OnRemoveForeignKey(object sender, RoutedEventArgs e) => _viewModel?.RemoveForeignKeyCommand.Execute(Row(sender));

    private static object? Row(object sender) => ((FrameworkElement)sender).DataContext;
}
