using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Squill.Core;
using Squill.Presentation.Connections;

namespace Squill.App.WinUI.Views;

/// <summary>Modal dialog that edits one connection profile.</summary>
public sealed partial class ConnectionEditorDialog : ContentDialog
{
    private readonly ConnectionEditorViewModel _editor;
    private readonly StackPanel _sqliteFields = new() { Spacing = 6 };
    private readonly StackPanel _serverFields = new() { Spacing = 6 };
    private readonly StackPanel _sshFields = new() { Spacing = 6 };
    private readonly StackPanel _sshKeyFields = new() { Spacing = 6 };
    private readonly TextBlock _errors = new() { TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Application.Current.Resources["SquillDangerBrush"] };
    private readonly TextBlock _testStatus = new() { TextWrapping = TextWrapping.Wrap };

    /// <summary>Creates the dialog for an editor view-model.</summary>
    /// <param name="editor">The profile being edited.</param>
    /// <param name="root">The root the dialog is shown in.</param>
    public ConnectionEditorDialog(ConnectionEditorViewModel editor, XamlRoot root)
    {
        _editor = editor;
        XamlRoot = root;
        Title = "Connection";
        PrimaryButtonText = "Save";
        SecondaryButtonText = "Test";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Primary;
        Content = new ScrollViewer { Content = BuildForm() };
        PrimaryButtonClick += (_, e) =>
        {
            // Keep the dialog open until Saved fires, so validation errors stay visible.
            e.Cancel = true;
            _editor.SaveCommand.Execute(null);
        };
        SecondaryButtonClick += (_, e) =>
        {
            e.Cancel = true;
            _editor.TestCommand.Execute(null);
        };
        Opened += (_, _) => Subscribe();
        Closed += (_, _) => Unsubscribe();
        Refresh();
    }

    private void Subscribe()
    {
        _editor.PropertyChanged += OnEditorChanged;
        _editor.Saved += OnSaved;
    }

    private void Unsubscribe()
    {
        _editor.PropertyChanged -= OnEditorChanged;
        _editor.Saved -= OnSaved;
    }

    private void OnSaved(object? sender, EventArgs e) => Hide();

    private void OnEditorChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        var sqlite = _editor.Dialect == DatabaseDialect.Sqlite;
        _sqliteFields.Visibility = sqlite ? Visibility.Visible : Visibility.Collapsed;
        _serverFields.Visibility = sqlite ? Visibility.Collapsed : Visibility.Visible;
        _sshFields.Visibility = _editor.UseSsh ? Visibility.Visible : Visibility.Collapsed;
        _sshKeyFields.Visibility = _editor.SshAuthMethod == SshAuthMethod.PrivateKey ? Visibility.Visible : Visibility.Collapsed;
        _errors.Text = string.Join('\n', _editor.Validate());
        _testStatus.Text = _editor.TestStatus ?? "";
    }

    private StackPanel BuildForm()
    {
        var form = new StackPanel { Spacing = 6, MinWidth = 400 };
        AddText(form, "Name", _editor.Name, v => _editor.Name = v);
        form.Children.Add(new TextBlock { Text = "Engine" });
        form.Children.Add(Combo(
            [DatabaseDialect.PostgreSql, DatabaseDialect.MySql, DatabaseDialect.SqlServer, DatabaseDialect.Sqlite],
            _editor.Dialect,
            v => _editor.Dialect = v));

        AddText(_sqliteFields, "Database file", _editor.FilePath, v => _editor.FilePath = v);
        AddText(_serverFields, "Host", _editor.Host, v => _editor.Host = v);
        AddText(_serverFields, "Port", _editor.Port?.ToString(CultureInfo.InvariantCulture) ?? "", v => _editor.Port = ParsePort(v));
        AddText(_serverFields, "Database", _editor.Database, v => _editor.Database = v);
        AddText(_serverFields, "User name", _editor.UserName, v => _editor.UserName = v);
        AddPassword(_serverFields, "Password", _editor.Password, v => _editor.Password = v);
        form.Children.Add(_sqliteFields);
        form.Children.Add(_serverFields);

        var ssh = new CheckBox { Content = "Connect through an SSH tunnel", IsChecked = _editor.UseSsh, Margin = new Thickness(0, 6, 0, 0) };
        ssh.Click += (_, _) => _editor.UseSsh = ssh.IsChecked == true;
        form.Children.Add(ssh);
        AddText(_sshFields, "SSH host", _editor.SshHost, v => _editor.SshHost = v);
        AddText(_sshFields, "SSH port", _editor.SshPort?.ToString(CultureInfo.InvariantCulture) ?? "", v => _editor.SshPort = ParsePort(v));
        AddText(_sshFields, "SSH user name", _editor.SshUserName, v => _editor.SshUserName = v);
        _sshFields.Children.Add(new TextBlock { Text = "Authentication" });
        _sshFields.Children.Add(Combo([SshAuthMethod.Password, SshAuthMethod.PrivateKey], _editor.SshAuthMethod, v => _editor.SshAuthMethod = v));
        AddText(_sshKeyFields, "Private key file", _editor.SshPrivateKeyPath, v => _editor.SshPrivateKeyPath = v);
        _sshFields.Children.Add(_sshKeyFields);
        AddPassword(_sshFields, "Password or key passphrase", _editor.SshSecret, v => _editor.SshSecret = v);
        form.Children.Add(_sshFields);

        form.Children.Add(_errors);
        form.Children.Add(_testStatus);
        return form;
    }

    private static int? ParsePort(string text) => int.TryParse(text, CultureInfo.InvariantCulture, out var port) ? port : null;

    private static void AddText(Panel parent, string label, string initial, Action<string> set)
    {
        var box = new TextBox { Text = initial };
        box.TextChanged += (_, _) => set(box.Text);
        parent.Children.Add(new TextBlock { Text = label });
        parent.Children.Add(box);
    }

    private static void AddPassword(Panel parent, string label, string initial, Action<string> set)
    {
        var box = new PasswordBox { Password = initial };
        box.PasswordChanged += (_, _) => set(box.Password);
        parent.Children.Add(new TextBlock { Text = label });
        parent.Children.Add(box);
    }

    private static ComboBox Combo<T>(T[] items, T selected, Action<T> set)
        where T : struct, Enum
    {
        var box = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var item in items)
            box.Items.Add(item.ToString());
        box.SelectedIndex = Array.IndexOf(items, selected);
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedIndex >= 0)
                set(items[box.SelectedIndex]);
        };
        return box;
    }
}
