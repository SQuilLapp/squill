using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Squill.App.Avalonia.Bench;
using Squill.App.Avalonia.Services;
using Squill.Data;
using Squill.Data.MySql;
using Squill.Data.Oracle;
using Squill.Data.Postgres;
using Squill.Data.Sqlite;
using Squill.Data.SqlServer;
using Squill.Presentation;
using Squill.Platform;
using Squill.Platform.Settings;
using Squill.Presentation.Editor;
using Squill.Platform.Storage;
using Squill.App.Avalonia.Controls;
using Squill.App.Avalonia.Views;

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
            providers.Register(new OracleDatabaseProvider());
            var platform = new AvaloniaPlatformServices(providers, () => new Editors.AvaloniaSqlEditor());
            // SQUILL_DATA_DIR redirects profiles, settings and history (used to run against a throwaway data directory).
            var dataDir = Environment.GetEnvironmentVariable("SQUILL_DATA_DIR");
            _paths = new AppPaths(string.IsNullOrEmpty(dataDir) ? null : dataDir);
            Services = AppComposition.Create(platform, _paths, new JsonDiagramLayoutStore(_paths), ErDiagramCanvas.MeasureText);
            desktop.MainWindow = GridBench.IsRequested(desktop.Args) ? GridBench.Start(desktop)
                : Environment.GetEnvironmentVariable("SQUILL_STYLE_GALLERY") == "1" ? new StyleGallery()
                : CreateMainWindow(Services);
            if (desktop.MainWindow is MainWindow main)
            {
                desktop.ShutdownRequested += main.OnShutdownRequested;
                Program.ShowMainWindow = () => global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (main.WindowState == global::Avalonia.Controls.WindowState.Minimized)
                        main.WindowState = global::Avalonia.Controls.WindowState.Normal;
                    main.Activate();
                });
            }
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
        window.Opened += async (_, _) => await window.StartUpdatesAsync();
        return window;
    }

    private void ApplyTheme(ThemeMode mode) => RequestedThemeVariant = mode switch
    {
        ThemeMode.Light => ThemeVariant.Light,
        ThemeMode.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };
}
