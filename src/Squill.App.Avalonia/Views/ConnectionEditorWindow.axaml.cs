using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Squill.Core;
using Squill.Presentation.Connections;

namespace Squill.App.Avalonia.Views;

/// <summary>Modal dialog that edits one connection profile.</summary>
public sealed partial class ConnectionEditorWindow : Window
{
    private ConnectionEditorViewModel? _editor;

    /// <summary>Creates the window.</summary>
    public ConnectionEditorWindow()
    {
        InitializeComponent();
        DialectBox.ItemsSource = new[] { DatabaseDialect.PostgreSql, DatabaseDialect.MySql, DatabaseDialect.SqlServer, DatabaseDialect.Sqlite };
        SshAuthBox.ItemsSource = new[] { SshAuthMethod.Password, SshAuthMethod.PrivateKey };
        DataContextChanged += (_, _) => Attach();
    }

    private void Attach()
    {
        if (_editor is { } old)
        {
            old.PropertyChanged -= OnEditorChanged;
            old.Saved -= OnSaved;
        }

        _editor = DataContext as ConnectionEditorViewModel;
        if (_editor is null)
            return;
        _editor.PropertyChanged += OnEditorChanged;
        _editor.Saved += OnSaved;
        ShowErrors();
    }

    private void OnEditorChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => ShowErrors();

    private void ShowErrors() => ErrorsText.Text = _editor is null ? "" : string.Join('\n', _editor.Validate());

    private void OnSaved(object? sender, EventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

    private async void OnBrowseDatabase(object? sender, RoutedEventArgs e)
    {
        if (_editor is not null && await PickAsync("Select the database file") is { } path)
            _editor.FilePath = path;
    }

    private async void OnBrowseKey(object? sender, RoutedEventArgs e)
    {
        if (_editor is not null && await PickAsync("Select the private key file") is { } path)
            _editor.SshPrivateKeyPath = path;
    }

    private async Task<string?> PickAsync(string title)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = false });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }
}
