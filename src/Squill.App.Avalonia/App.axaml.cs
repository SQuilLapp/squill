using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Squill.App.Avalonia.Bench;
using Squill.App.Avalonia.Services;
using Squill.Data;
using Squill.Data.MySql;
using Squill.Data.Postgres;
using Squill.Data.Sqlite;
using Squill.Data.SqlServer;
using Squill.Presentation;
using Squill.Presentation.Editor;

namespace Squill.App.Avalonia;

/// <summary>Application root: loads the resources and builds the shared object graph.</summary>
public sealed partial class App : Application
{
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
            var platform = new AvaloniaPlatformServices(providers, () => new StubSqlEditor());
            Services = AppComposition.Create(platform);
            desktop.MainWindow = GridBench.IsRequested(desktop.Args) ? GridBench.Start(desktop) : new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
