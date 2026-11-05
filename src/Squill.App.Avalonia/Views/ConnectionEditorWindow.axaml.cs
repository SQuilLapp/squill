using Squill.App.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Squill.Core;
using Squill.Platform.Profiles;
using Squill.Presentation.Connections;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;

namespace Squill.App.Avalonia.Views;

/// <summary>Modal dialog that edits one connection profile, next to the grouped list of all profiles.</summary>
public sealed partial class ConnectionEditorWindow : Window
{
    private readonly HashSet<string> _collapsed = [];
    private ConnectionEditorViewModel? _editor;
    private ConnectionListViewModel? _list;

    /// <summary>Creates the window.</summary>
    public ConnectionEditorWindow()
    {
        InitializeComponent();
        SshAuthBox.ItemsSource = new[] { SshAuthMethod.Password, SshAuthMethod.PrivateKey };
        TlsBox.ItemsSource = new[]
        {
            UiText.Get("connections.tlsDefault"), UiText.Get("connections.tlsDisable"),
            UiText.Get("connections.tlsRequire"), UiText.Get("connections.tlsVerifyFull"),
        };
        DataContextChanged += (_, _) => Attach();
        Opened += (_, _) => RebuildList();
    }

    /// <summary>Gets or sets the connection list shown on the left; <see langword="null"/> shows an empty list.</summary>
    public ConnectionListViewModel? List
    {
        get => _list;
        set
        {
            if (_list is not null)
                _list.PropertyChanged -= OnListChanged;
            _list = value;
            if (_list is not null)
                _list.PropertyChanged += OnListChanged;
            RebuildList();
        }
    }

    private T? Resource<T>(string key) where T : class =>
        this.TryFindResource(key, ActualThemeVariant, out var value) ? value as T : null;

    private void OnListChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => RebuildList();

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
        Refresh();
    }

    private void OnEditorChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Refresh();

    private void OnSaved(object? sender, EventArgs e) => Close(true);

    private void OnTlsChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_editor is not null && TlsBox.SelectedIndex is >= 0 and <= 3)
            _editor.Tls = (TlsMode)TlsBox.SelectedIndex;
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

    private void OnEngineTile(object? sender, RoutedEventArgs e)
    {
        if (_editor is not null && sender is Button { Tag: DatabaseDialect dialect })
        {
            _editor.Dialect = dialect;
            _editor.MariaDb = ReferenceEquals(sender, TileMariaDb);
        }
    }

    private void OnEnvironmentTile(object? sender, RoutedEventArgs e)
    {
        if (_editor is not null && sender is Button { Tag: ConnectionEnvironment environment })
            _editor.Environment = environment;
    }

    private void OnEditSsh(object? sender, RoutedEventArgs e) => Tabs.SelectedItem = SshTab;

    private void OnNewConnection(object? sender, RoutedEventArgs e)
    {
        if (_list is null)
            return;
        _list.SelectedProfile = null;
        _list.NewCommand.Execute(null);
    }

    private void Refresh()
    {
        if (_editor is not { } editor)
            return;

        ErrorsText.Text = string.Join('\n', editor.Validate());

        HeaderEngine.Dialect = editor.Dialect;
        HeaderEngine.MariaDb = editor.MariaDb;
        HeaderName.Text = string.IsNullOrWhiteSpace(editor.Name) ? UiText.Get("connections.newConnection") : editor.Name;
        HeaderSubtitle.Text = UiText.Get("connections.newUnsaved");
        HeaderSubtitle.IsVisible = _list?.SelectedProfile is null;

        var (envText, envClass) = editor.Environment switch
        {
            ConnectionEnvironment.Production => (UiText.Get("status.production"), "env-prod"),
            ConnectionEnvironment.Staging => (UiText.Get("status.staging"), "env-staging"),
            ConnectionEnvironment.Development => (UiText.Get("status.dev"), "env-dev"),
            ConnectionEnvironment.Local => (UiText.Get("status.local"), "env-local"),
            _ => ("", ""),
        };
        HeaderEnv.IsVisible = envClass.Length > 0;
        HeaderEnvText.Text = envText;
        SetClasses(HeaderEnv, envClass, "env-prod", "env-staging", "env-dev", "env-local");
        ProductionHint.IsVisible = editor.Environment == ConnectionEnvironment.Production;

        foreach (var tile in new[] { TilePostgres, TileMySql, TileMariaDb, TileSqlServer, TileSqlite, TileOracle })
            tile.Classes.Set("selected", tile.Tag is DatabaseDialect d && d == editor.Dialect && (d != DatabaseDialect.MySql || ReferenceEquals(tile, TileMariaDb) == editor.MariaDb));
        foreach (var tile in new[] { EnvProduction, EnvStaging, EnvDevelopment, EnvLocal })
            tile.Classes.Set("selected", tile.Tag is ConnectionEnvironment env && env == editor.Environment);

        TlsBox.SelectedIndex = (int)editor.Tls;
        TlsFields.IsVisible = editor.SupportsTls;
        TrustBox.IsVisible = editor.SupportsTrustCertificate;
        ConnectTimeoutFields.IsVisible = editor.SupportsTimeouts;

        SshSummary.Text = $"{editor.SshHost}:{editor.SshPort} · {editor.SshUserName}";

        ResultBar.IsVisible = editor.TestSucceeded is not null;
        var ok = editor.TestSucceeded == true;
        SetClasses(ResultBar, ok ? "success" : "danger", "success", "danger");
        ResultText.Text = ok ? UiText.Get("connections.testSucceeded") : editor.TestStatus;
        ResultDetail.Text = ok ? editor.TestServerVersion : "";

        FolderBox.ItemsSource = _list?.Groups.Where(g => g.Folder is not null).Select(g => g.Folder!).ToArray() ?? [];
        RebuildList();
    }

    private static void SetClasses(StyledElement element, string active, params string[] all)
    {
        foreach (var name in all)
            element.Classes.Set(name, name == active);
    }

    private void RebuildList()
    {
        ListHost.Children.Clear();
        if (_list is null)
            return;
        foreach (var group in _list.Groups)
        {
            var key = group.Folder ?? "";
            var open = !_collapsed.Contains(key);
            if (group.Folder is not null)
                ListHost.Children.Add(GroupHeader(group, open));
            if (!open)
                continue;
            foreach (var profile in group.Items)
                ListHost.Children.Add(ProfileRow(profile, indented: group.Folder is not null));
        }
    }

    private Button GroupHeader(ConnectionGroup group, bool open)
    {
        var chevron = new global::Avalonia.Controls.Shapes.Path { Classes = { "icon" }, Width = 12, Height = 12, Data = Resource<Geometry>("SquillIconChevron"), RenderTransform = open ? null : new RotateTransform(-90) };
        var folder = new global::Avalonia.Controls.Shapes.Path { Classes = { "icon" }, Width = 14, Height = 14, Data = Resource<Geometry>("SquillIconFolder") };
        var name = new TextBlock { Text = group.Folder, Classes = { "group" }, VerticalAlignment = VerticalAlignment.Center };
        var count = new TextBlock { Text = DisplayFormat.Count(group.Items.Count), Foreground = (IBrush?)this.FindResource("SquillFg3Brush"), VerticalAlignment = VerticalAlignment.Center };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), Height = 31 };
        name.Margin = new Thickness(8, 0, 0, 0);
        folder.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(folder, 1);
        Grid.SetColumn(name, 2);
        Grid.SetColumn(count, 3);
        row.Children.Add(chevron);
        row.Children.Add(folder);
        row.Children.Add(name);
        row.Children.Add(count);
        var button = new Button { Content = row, Classes = { "ghost" }, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(4, 0), MinHeight = 0 };
        button.Click += (_, _) =>
        {
            if (!_collapsed.Remove(group.Folder!))
                _collapsed.Add(group.Folder!);
            RebuildList();
        };
        return button;
    }

    private Button ProfileRow(ConnectionProfile profile, bool indented)
    {
        var badge = new EngineIcon { Dialect = profile.Dialect, MariaDb = profile.MariaDb, VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock { Text = profile.Name, Foreground = Resource<IBrush>("SquillFgBrush"), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(8, 0) };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Height = 31 };
        Grid.SetColumn(name, 1);
        row.Children.Add(badge);
        row.Children.Add(name);
        var envClass = profile.Environment switch
        {
            ConnectionEnvironment.Production => "env-prod",
            ConnectionEnvironment.Staging => "env-staging",
            ConnectionEnvironment.Development => "env-dev",
            ConnectionEnvironment.Local => "env-local",
            _ => null,
        };
        if (envClass is not null)
        {
            var square = new Border { Classes = { "square", envClass }, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(square, 2);
            row.Children.Add(square);
        }

        var button = new Button
        {
            Content = row,
            Classes = { "ghost" },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(indented ? 24 : 8, 0, 8, 0),
            MinHeight = 0,
        };
        if (_list?.SelectedProfile?.Id == profile.Id)
            button.Background = Resource<IBrush>("SquillAccentSoftBrush");
        button.Click += (_, _) =>
        {
            if (_list is null)
                return;
            _list.SelectedProfile = profile;
            _list.EditCommand.Execute(null);
        };
        return button;
    }

    private async void OnBrowseDatabase(object? sender, RoutedEventArgs e)
    {
        if (_editor is not null && await PickAsync(UiText.Get("connections.pickDatabase"), [new FilePickerFileType(UiText.Get("connections.fileTypeSqlite")) { Patterns = ["*.db", "*.sqlite", "*.sqlite3", "*.db3"] }, new FilePickerFileType(UiText.Get("connections.fileTypeAll")) { Patterns = ["*"] }]) is { } path)
            _editor.FilePath = path;
    }

    private async void OnBrowseKey(object? sender, RoutedEventArgs e)
    {
        if (_editor is not null && await PickAsync(UiText.Get("connections.pickKey")) is { } path)
            _editor.SshPrivateKeyPath = path;
    }

    private async Task<string?> PickAsync(string title, IReadOnlyList<FilePickerFileType>? fileTypes = null)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = false, FileTypeFilter = fileTypes });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }
}
