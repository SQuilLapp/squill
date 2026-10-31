using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Squill.App.Avalonia.Bench;
using Squill.App.Avalonia.Services;
using Squill.Data;
using Squill.Data.MySql;
using Squill.Data.Postgres;
using Squill.Data.Sqlite;
using Squill.Data.SqlServer;
using Squill.Presentation;
using Squill.Platform;
using Squill.Platform.Settings;
using Squill.Presentation.Editor;

namespace Squill.App.Avalonia;

/// <summary>Application root: loads the resources and builds the shared object graph.</summary>
public sealed partial class App : Application
{
    private AppPaths? _paths;

    /// <summary>Gets the root view-models shared with the other heads.</summary>
    public AppServices? Services { get; private set; }

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var providers = new ProviderRegistry();
            providers.Register(new SqliteDatabaseProvider());
            providers.Register(new PostgresDatabaseProvider());
            providers.Register(new MySqlDatabaseProvider());
            providers.Register(new SqlServerDatabaseProvider());
            var platform = new AvaloniaPlatformServices(providers, () => new Editors.AvaloniaSqlEditor());
            // SQUILL_DATA_DIR redirects profiles, settings and history (used to run against a throwaway data directory).
            var dataDir = Environment.GetEnvironmentVariable("SQUILL_DATA_DIR");
            _paths = new AppPaths(string.IsNullOrEmpty(dataDir) ? null : dataDir);
            Services = AppComposition.Create(platform, _paths);
            desktop.MainWindow = GridBench.IsRequested(desktop.Args) ? GridBench.Start(desktop) : CreateMainWindow(Services);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private MainWindow CreateMainWindow(AppServices services)
    {
        services.Settings.ThemeChanged += (_, _) => ApplyTheme(services.Settings.Theme);
        services.Settings.LoadCommand.Execute(null);
        services.Workspace.ConnectionList.LoadCommand.Execute(null);
        var window = new MainWindow { DataContext = services.Workspace };
        window.Attach(services, _paths!.DataDirectory);
        return window;
    }

    private void ApplyTheme(ThemeMode mode) => RequestedThemeVariant = mode switch
    {
        ThemeMode.Light => ThemeVariant.Light,
        ThemeMode.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };
}
