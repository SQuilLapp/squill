using Microsoft.UI.Xaml;
using Squill.App.WinUI.Services;
using Squill.Data;
using Squill.Data.MySql;
using Squill.Data.Postgres;
using Squill.Data.Sqlite;
using Squill.Data.SqlServer;
using Squill.Platform;
using Squill.Presentation;
using Squill.App.WinUI.Editors;

namespace Squill.App.WinUI;

/// <summary>Application root: builds the shared object graph and opens the main window.</summary>
public sealed partial class App : Application
{
    private MainWindow? _window;

    /// <summary>Initializes the application.</summary>
    public App() => InitializeComponent();

    /// <summary>Gets the root view-models shared with the other heads.</summary>
    public AppServices? Services { get; private set; }

    /// <inheritdoc />
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var providers = new ProviderRegistry();
        providers.Register(new SqliteDatabaseProvider());
        providers.Register(new PostgresDatabaseProvider());
        providers.Register(new MySqlDatabaseProvider());
        providers.Register(new SqlServerDatabaseProvider());
        var platform = new WinUiPlatformServices(providers, () => new PlainSqlEditor());
        // SQUILL_DATA_DIR redirects profiles, settings and history (used to run against a throwaway data directory).
        var dataDir = Environment.GetEnvironmentVariable("SQUILL_DATA_DIR");
        Services = AppComposition.Create(platform, new AppPaths(string.IsNullOrEmpty(dataDir) ? null : dataDir));
        _window = new MainWindow();
        platform.AttachWindow(_window);
        _window.Activate();
    }
}
