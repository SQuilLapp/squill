using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Squill.Core;
using Squill.Platform.Profiles;
using Squill.Presentation.Connections;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;
using Windows.Graphics;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace Squill.App.WinUI.Views;

/// <summary>Window, modal to the main window, that edits one connection profile next to the grouped list of all profiles.</summary>
public sealed partial class ConnectionEditorWindow : Window
{
    private const int LogicalWidth = 1200;
    private const int LogicalHeight = 780;
    private const int OwnerIndex = -8; // GWLP_HWNDPARENT

    private readonly HashSet<string> _collapsed = [];
    private readonly ConnectionListViewModel _list;
    private readonly Dictionary<ConnectionEditorFormat.Engine, Button> _engineTiles = [];
    private readonly Dictionary<ConnectionEnvironment, Button> _environmentTiles = [];
    private readonly Window _owner;
    private ConnectionEditorViewModel? _editor;
    private bool _loading;

    /// <summary>Creates the window and shows the first editor.</summary>
    /// <param name="owner">The main window; it stays disabled while this window is open.</param>
    /// <param name="list">The connection list shown on the left.</param>
    /// <param name="editor">The profile to edit first.</param>
    public ConnectionEditorWindow(Window owner, ConnectionListViewModel list, ConnectionEditorViewModel editor)
    {
        InitializeComponent();
        _owner = owner;
        _list = list;
        CaptionBar.Follow(this, Root, ((FrameworkElement)owner.Content));
        Title = UiText.Get("connections.title");
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "squill.ico"));
        SshAuthBox.ItemsSource = new[] { SshAuthMethod.Password.ToString(), SshAuthMethod.PrivateKey.ToString() };
        GeneralTabItem.Text = UiText.Get("connections.tabGeneral");
        SshTabItem.Text = UiText.Get("connections.tabSsh");
        TlsBox.ItemsSource = new[]
        {
            UiText.Get("connections.tlsDefault"), UiText.Get("connections.tlsDisable"),
            UiText.Get("connections.tlsRequire"), UiText.Get("connections.tlsVerifyFull"),
        };
        BuildTiles();
        PlaceModal(owner);

        Root.KeyboardAccelerators.Add(TestAccelerator());
        Root.ActualThemeChanged += (_, _) => Refresh();
        _list.PropertyChanged += OnListChanged;
        Closed += OnClosed;
        SetEditor(editor);
    }

    /// <summary>Shows another profile in the window, dropping unsaved edits of the previous one.</summary>
    /// <param name="editor">The editor of the profile.</param>
    public void SetEditor(ConnectionEditorViewModel editor)
    {
        if (_editor is { } old)
        {
            old.PropertyChanged -= OnEditorChanged;
            old.Saved -= OnSaved;
        }

        _editor = editor;
        editor.PropertyChanged += OnEditorChanged;
        editor.Saved += OnSaved;
        TestButton.Command = editor.TestCommand;
        SaveButton.Command = editor.SaveCommand;
        GeneralTabItem.IsSelected = true;
        Refresh();
    }

    private static void ToggleTile(Button tile, bool selected, Brush border, Brush background)
    {
        tile.BorderThickness = new Thickness(selected ? 2 : 1);
        tile.BorderBrush = border;
        tile.Background = background;
    }

    private KeyboardAccelerator TestAccelerator()
    {
        var accelerator = new KeyboardAccelerator { Key = VirtualKey.Enter, Modifiers = VirtualKeyModifiers.Control };
        accelerator.Invoked += (_, args) =>
        {
            args.Handled = true;
            if (_editor?.TestCommand.CanExecute(null) == true)
                _editor.TestCommand.Execute(null);
        };
        return accelerator;
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _list.PropertyChanged -= OnListChanged;
        if (_editor is { } editor)
        {
            editor.PropertyChanged -= OnEditorChanged;
            editor.Saved -= OnSaved;
        }

        _owner.Activate();
    }

    private void OnSaved(object? sender, EventArgs e) => Close();

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private void OnEditorChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void OnListChanged(object? sender, PropertyChangedEventArgs e) => RebuildList();

    private void OnNewConnection(object sender, RoutedEventArgs e)
    {
        _list.SelectedProfile = null;
        _list.NewCommand.Execute(null);
    }

    private void OnEditSsh(object sender, RoutedEventArgs e) => SshTabItem.IsSelected = true;

    private void OnTabChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var ssh = sender.SelectedItem == SshTabItem;
        GeneralTab.Visibility = ssh ? Visibility.Collapsed : Visibility.Visible;
        SshTab.Visibility = ssh ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BuildTiles()
    {
        var engines = ConnectionEditorFormat.Engines;
        for (var i = 0; i < engines.Length; i++)
        {
            var engine = engines[i];
            var (letters, suffix) = ConnectionEditorFormat.Badge(engine.Dialect, engine.MariaDb);
            var badge = new ContentControl
            {
                Style = ExplorerGlyphs.StyleOf($"EngineIcon{suffix}Style"),
                Content = letters,
                Width = 20,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            var label = new TextBlock { Text = ConnectionEditorFormat.EngineName(engine.Dialect, engine.MariaDb), HorizontalAlignment = HorizontalAlignment.Center, FontSize = 13 };
            var tile = Tile(new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, Children = { badge, label } }, () =>
            {
                _editor!.Dialect = engine.Dialect;
                _editor.MariaDb = engine.MariaDb;
            });
            tile.Height = 52;
            tile.MinHeight = 52;
            tile.Padding = new Thickness(6, 0, 6, 0);
            Grid.SetColumn(tile, i);
            EngineHost.Children.Add(tile);
            _engineTiles[engine] = tile;
        }

        var environments = ConnectionEditorFormat.Environments;
        for (var i = 0; i < environments.Length; i++)
        {
            var environment = environments[i];
            var square = new Border { Style = ExplorerGlyphs.StyleOf($"EnvSquare{TabInfo.StyleSuffix(environment)}Style") };
            var label = new TextBlock { Text = UiText.Get(ConnectionEditorFormat.EnvironmentKey(environment)), FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            var tile = Tile(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { square, label } }, () => _editor!.Environment = environment);
            tile.Height = 34;
            tile.MinHeight = 34;
            tile.Padding = new Thickness(10, 0, 10, 0);
            Grid.SetColumn(tile, i % 2);
            Grid.SetRow(tile, i / 2);
            EnvironmentHost.Children.Add(tile);
            _environmentTiles[environment] = tile;
        }
    }

    private Button Tile(UIElement content, Action select)
    {
        var tile = new Button
        {
            Content = content,
            Style = ExplorerGlyphs.StyleOf("GhostButtonStyle"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(6),
            BorderThickness = new Thickness(1),
        };
        tile.Click += (_, _) =>
        {
            if (_editor is not null)
                select();
        };
        return tile;
    }

    private Brush Token(string key) => Tokens.Brush(Root, key);

    private void Refresh()
    {
        if (_editor is not { } editor)
            return;

        _loading = true;
        try
        {
            SyncText(NameBox, editor.Name);
            SyncText(FileBox, editor.FilePath);
            SyncText(HostBox, editor.Host);
            SyncText(PortBox, ConnectionEditorFormat.PortText(editor.Port));
            SyncText(DatabaseBox, editor.Database);
            SyncText(UserBox, editor.UserName);
            SyncText(SshHostBox, editor.SshHost);
            SyncText(SshPortBox, ConnectionEditorFormat.PortText(editor.SshPort));
            SyncText(SshUserBox, editor.SshUserName);
            SyncText(SshKeyBox, editor.SshPrivateKeyPath);
            if (PasswordInput.Password != editor.Password)
                PasswordInput.Password = editor.Password;
            if (SshSecretInput.Password != editor.SshSecret)
                SshSecretInput.Password = editor.SshSecret;
            var folders = ConnectionEditorFormat.Folders(_list.Groups);
            if (FolderBox.ItemsSource is not string[] shown || !shown.AsSpan().SequenceEqual(folders))
                FolderBox.ItemsSource = folders;
            SyncText(ConnectTimeoutBox, editor.ConnectTimeoutText);
            SyncText(CommandTimeoutBox, editor.CommandTimeoutText);
            TlsBox.SelectedIndex = (int)editor.Tls;
            TrustBox.IsChecked = editor.TrustServerCertificate;
            UseSshBox.IsChecked = editor.UseSsh;
            SshAuthBox.SelectedIndex = editor.SshAuthMethod == SshAuthMethod.PrivateKey ? 1 : 0;
        }
        finally
        {
            _loading = false;
        }

        var sqlite = editor.Dialect == DatabaseDialect.Sqlite;
        SqliteFields.Visibility = sqlite ? Visibility.Visible : Visibility.Collapsed;
        ServerFields.Visibility = sqlite ? Visibility.Collapsed : Visibility.Visible;
        TlsFields.Visibility = editor.SupportsTls ? Visibility.Visible : Visibility.Collapsed;
        TrustBox.Visibility = editor.SupportsTrustCertificate ? Visibility.Visible : Visibility.Collapsed;
        ConnectTimeoutFields.Visibility = editor.SupportsTimeouts ? Visibility.Visible : Visibility.Collapsed;
        SshFields.Visibility = editor.UseSsh ? Visibility.Visible : Visibility.Collapsed;
        SshCard.Visibility = editor.UseSsh ? Visibility.Visible : Visibility.Collapsed;
        SshKeyFields.Visibility = editor.SshAuthMethod == SshAuthMethod.PrivateKey ? Visibility.Visible : Visibility.Collapsed;
        SshSummary.Text = ConnectionEditorFormat.SshSummary(editor.SshHost, editor.SshPort, editor.SshUserName);
        ErrorsText.Text = string.Join('\n', editor.Validate());

        var (letters, suffix) = ConnectionEditorFormat.Badge(editor.Dialect, editor.MariaDb);
        HeaderEngine.Style = ExplorerGlyphs.StyleOf($"EngineIcon{suffix}Style");
        HeaderEngine.Content = letters;
        HeaderEngine.Width = 32;
        HeaderEngine.Height = 32;
        HeaderEngine.FontSize = 13;
        HeaderName.Text = string.IsNullOrWhiteSpace(editor.Name) ? UiText.Get("connections.newConnection") : editor.Name;
        HeaderSubtitle.Visibility = _list.SelectedProfile is null ? Visibility.Visible : Visibility.Collapsed;
        var envKey = ChromeFormat.Environment(editor.Environment);
        HeaderEnv.Visibility = envKey is null ? Visibility.Collapsed : Visibility.Visible;
        if (envKey is { } env)
        {
            HeaderEnv.Style = ExplorerGlyphs.StyleOf($"BadgeEnv{env.Suffix}Style");
            HeaderEnv.Content = UiText.Get(env.LabelKey);
        }

        ProductionHint.Visibility = editor.Environment == ConnectionEnvironment.Production ? Visibility.Visible : Visibility.Collapsed;

        foreach (var (engine, tile) in _engineTiles)
        {
            var selected = engine.Dialect == editor.Dialect && engine.MariaDb == editor.MariaDb;
            ToggleTile(tile, selected, Token(selected ? "SquillAccentBrush" : "SquillBorderBrush"), Token(selected ? "SquillAccentSoftBrush" : "SquillBgRaisedBrush"));
        }
        foreach (var (environment, tile) in _environmentTiles)
        {
            var selected = environment == editor.Environment;
            var tone = TabInfo.StyleSuffix(environment);
            ToggleTile(tile, selected, Token(selected ? $"SquillEnv{tone}Brush" : "SquillBorderBrush"), Token(selected ? $"SquillEnv{tone}SoftBrush" : "SquillBgRaisedBrush"));
        }

        ResultBar.Visibility = editor.TestSucceeded is null ? Visibility.Collapsed : Visibility.Visible;
        var ok = editor.TestSucceeded == true;
        ResultBar.Background = Token(ok ? "SquillSuccessSoftBrush" : "SquillDangerSoftBrush");
        ResultText.Foreground = Token(ok ? "SquillSuccessBrush" : "SquillDangerBrush");
        ResultText.Text = ok ? UiText.Get("connections.testSucceeded") : editor.TestStatus ?? "";
        ResultDetail.Text = ok ? editor.TestServerVersion ?? "" : "";

        RebuildList();
    }

    private static void SyncText(TextBox box, string value)
    {
        if (box.Text != value)
            box.Text = value;
    }

    private void RebuildList()
    {
        ListHost.Children.Clear();
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
        var fg2 = Token("SquillFg2Brush");
        var chevron = Icons.Create("SquillIconCollapse", 12, fg2);
        chevron.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        if (!open)
            chevron.RenderTransform = new RotateTransform { Angle = -90 };
        var folder = Icons.Create("SquillIconFolder", 14, fg2);
        folder.Margin = new Thickness(6, 0, 0, 0);
        var name = new TextBlock
        {
            Text = group.Folder,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Token("SquillFgBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        var count = new TextBlock { Text = DisplayFormat.Count(group.Items.Count), Foreground = Token("SquillFg3Brush"), VerticalAlignment = VerticalAlignment.Center };
        var row = new Grid { Height = 31, ColumnDefinitions = { new() { Width = GridLength.Auto }, new() { Width = GridLength.Auto }, new(), new() { Width = GridLength.Auto } } };
        Grid.SetColumn(folder, 1);
        Grid.SetColumn(name, 2);
        Grid.SetColumn(count, 3);
        row.Children.Add(chevron);
        row.Children.Add(folder);
        row.Children.Add(name);
        row.Children.Add(count);
        var button = RowButton(row, new Thickness(4, 0, 4, 0));
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
        var (letters, suffix) = ConnectionEditorFormat.Badge(profile.Dialect, profile.MariaDb);
        var badge = new ContentControl { Style = ExplorerGlyphs.StyleOf($"EngineIcon{suffix}Style"), Content = letters, VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock
        {
            Text = profile.Name,
            Foreground = Token("SquillFgBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(8, 0, 8, 0),
        };
        var row = new Grid { Height = 31, ColumnDefinitions = { new() { Width = GridLength.Auto }, new(), new() { Width = GridLength.Auto } } };
        Grid.SetColumn(name, 1);
        row.Children.Add(badge);
        row.Children.Add(name);
        if (profile.Environment != ConnectionEnvironment.None)
        {
            var square = new Border { Style = ExplorerGlyphs.StyleOf($"EnvSquare{TabInfo.StyleSuffix(profile.Environment)}Style"), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(square, 2);
            row.Children.Add(square);
        }

        var button = RowButton(row, new Thickness(indented ? 24 : 8, 0, 8, 0));
        if (_list.SelectedProfile?.Id == profile.Id)
            button.Background = Token("SquillAccentSoftBrush");
        button.Click += (_, _) =>
        {
            _list.SelectedProfile = profile;
            _list.EditCommand.Execute(null);
        };
        return button;
    }

    private static Button RowButton(UIElement content, Thickness padding) => new()
    {
        Content = content,
        Style = ExplorerGlyphs.StyleOf("GhostButtonStyle"),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        Padding = padding,
        MinHeight = 0,
    };

    private async void OnBrowseDatabase(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".db");
        picker.FileTypeFilter.Add(".sqlite");
        picker.FileTypeFilter.Add(".sqlite3");
        picker.FileTypeFilter.Add(".db3");
        picker.FileTypeFilter.Add("*");
        // An unpackaged app must tell the picker which window owns it.
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var file = await picker.PickSingleFileAsync();
        if (file is not null && _editor is { } editor)
            editor.FilePath = file.Path;
    }

    private void OnNameChanged(object sender, TextChangedEventArgs e) => Edit(editor => editor.Name = NameBox.Text);

    private void OnFileChanged(object sender, TextChangedEventArgs e) => Edit(editor => editor.FilePath = FileBox.Text);

    private void OnHostChanged(object sender, TextChangedEventArgs e) => Edit(editor => editor.Host = HostBox.Text);

    private void OnPortChanged(object sender, TextChangedEventArgs e) => Edit(editor => editor.Port = ConnectionEditorFormat.ParsePort(PortBox.Text));

    private void OnDatabaseChanged(object sender, TextChangedEventArgs e) => Edit(editor => editor.Database = DatabaseBox.Text);

    private void OnUserChanged(object sender, TextChangedEventArgs e) => Edit(editor => editor.UserName = UserBox.Text);

    private void OnTlsChanged(object sender, SelectionChangedEventArgs e) =>
        Edit(editor => editor.Tls = TlsBox.SelectedIndex is >= 0 and <= 3 ? (TlsMode)TlsBox.SelectedIndex : TlsMode.Default);

    private void OnTrustClicked(object sender, RoutedEventArgs e) => Edit(editor => editor.TrustServerCertificate = TrustBox.IsChecked == true);

    private void OnConnectTimeoutChanged(object sender, TextChangedEventArgs e) => Edit(editor => editor.ConnectTimeoutText = ConnectTimeoutBox.Text);

    private void OnCommandTimeoutChanged(object sender, TextChangedEventArgs e) => Edit(editor => editor.CommandTimeoutText = CommandTimeoutBox.Text);

    private void OnPasswordChanged(object sender, RoutedEventArgs e) => Edit(editor => editor.Password = PasswordInput.Password);

    private void OnSshHostChanged(object sender, TextChangedEventArgs e) => Edit(editor => editor.SshHost = SshHostBox.Text);

    private void OnSshPortChanged(object sender, TextChangedEventArgs e) => Edit(editor => editor.SshPort = ConnectionEditorFormat.ParsePort(SshPortBox.Text));

    private void OnSshUserChanged(object sender, TextChangedEventArgs e) => Edit(editor => editor.SshUserName = SshUserBox.Text);

    private void OnSshKeyChanged(object sender, TextChangedEventArgs e) => Edit(editor => editor.SshPrivateKeyPath = SshKeyBox.Text);

    private void OnSshSecretChanged(object sender, RoutedEventArgs e) => Edit(editor => editor.SshSecret = SshSecretInput.Password);

    private void OnUseSshClicked(object sender, RoutedEventArgs e) => Edit(editor => editor.UseSsh = UseSshBox.IsChecked == true);

    private void OnSshAuthChanged(object sender, SelectionChangedEventArgs e) =>
        Edit(editor => editor.SshAuthMethod = SshAuthBox.SelectedIndex == 1 ? SshAuthMethod.PrivateKey : SshAuthMethod.Password);

    private void OnFolderSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args) => Edit(editor => editor.Folder = args.Text);

    private void OnFolderSelected(object sender, SelectionChangedEventArgs e)
    {
        if (FolderBox.SelectedItem is string folder)
            Edit(editor => editor.Folder = folder);
    }

    // Ignores the events raised while Refresh copies the view-model into the fields.
    private void Edit(Action<ConnectionEditorViewModel> apply)
    {
        if (!_loading && _editor is { } editor)
            apply(editor);
    }

    private void PlaceModal(Window owner)
    {
        var ownerHandle = WindowNative.GetWindowHandle(owner);
        var handle = WindowNative.GetWindowHandle(this);
        Win32.SetWindowLongPtr(handle, OwnerIndex, ownerHandle);
        var scale = Win32.GetDpiForWindow(handle) / 96.0;
        var size = new SizeInt32((int)(LogicalWidth * scale), (int)(LogicalHeight * scale));
        AppWindow.Resize(size);
        var at = owner.AppWindow.Position;
        var around = owner.AppWindow.Size;
        AppWindow.Move(new PointInt32(at.X + Math.Max(0, (around.Width - size.Width) / 2), at.Y + Math.Max(0, (around.Height - size.Height) / 2)));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
            presenter.IsModal = true;
        }
    }

    private static partial class Win32
    {
        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        internal static partial nint SetWindowLongPtr(nint window, int index, nint value);

        [LibraryImport("user32.dll")]
        internal static partial uint GetDpiForWindow(nint window);
    }
}
