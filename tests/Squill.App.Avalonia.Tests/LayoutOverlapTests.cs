using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Microsoft.Data.Sqlite;
using Squill.App.Avalonia.Controls;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform;
using Squill.Platform.Storage;
using Squill.Presentation;
using Squill.Presentation.Connections;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;
using Xunit;

namespace Squill.App.Avalonia.Tests;

// Every screen (query, panels, plan, designer, diagram, dialogs) at its minimum size and at common sizes, in both languages: no text drawn over another one or cut without an ellipsis.
[Collection("Headless")]
public sealed class LayoutOverlapTests
{
    private const string Schema = """
        CREATE TABLE commandes_fournisseurs_internationaux (id INTEGER PRIMARY KEY, reference_commande_fournisseur TEXT NOT NULL, passee_le TEXT);
        CREATE TABLE lignes_commande (
          commande_id INTEGER NOT NULL REFERENCES commandes_fournisseurs_internationaux(id),
          identifiant_produit_catalogue INTEGER NOT NULL, quantite INTEGER NOT NULL DEFAULT 1,
          prix_unitaire_hors_taxes REAL NOT NULL, PRIMARY KEY (commande_id, identifiant_produit_catalogue));
        CREATE INDEX ix_lignes_commande_produit ON lignes_commande(identifiant_produit_catalogue);
        INSERT INTO commandes_fournisseurs_internationaux VALUES (1, 'REF-0001', '2026-01-01');
        INSERT INTO lignes_commande VALUES (1, 42, 3, 12.5);
        """;

    private static readonly (double Width, double Height)[] MainSizes = [(720, 600), (1100, 700), (1440, 900)];
    private static readonly (double Width, double Height)[] SettingsSizes = [(760, 480), (1100, 720)];
    private static readonly (double Width, double Height)[] ConnectionSizes = [(1000, 640), (1200, 780)];
    private static readonly int[] DesignerSubTabs = [1, 2];
    private static readonly string[] SettingsPages = ["NavAppearance", "NavEditor", "NavGrid", "NavAdvanced"];

    [Theory]
    [InlineData(UiLanguage.English)]
    [InlineData(UiLanguage.French)] // last: leaves the language-dependent static state in the test default
    public async Task NoScreen_DrawsTextOverText(UiLanguage language)
    {
        var dir = Path.Combine(Path.GetTempPath(), "squill-overlap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var found = new List<string>();
        try
        {
            await Headless.Session.Dispatch(async () =>
            {
                using var _ = UiText.Override(language);
                var db = Path.Combine(dir, "t.db");
                using (var connection = new SqliteConnection($"Data Source={db}"))
                {
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = Schema;
                    command.ExecuteNonQuery();
                }

                SqliteConnection.ClearAllPools();
                var providers = new ProviderRegistry();
                providers.Register(new SqliteDatabaseProvider());
                var paths = new AppPaths(dir);
                using var store = new JsonDiagramLayoutStore(paths);
                var services = AppComposition.Create(new TestPlatform(providers), paths, store, ErDiagramCanvas.MeasureText);
                var window = new MainWindow { DataContext = services.Workspace, Width = 1100, Height = 700 };
                window.Attach(services, dir);
                window.Show();
                try
                {
                    var profile = new ConnectionProfile(Guid.NewGuid(), "Base de démonstration des commandes", DatabaseDialect.Sqlite, null, null, null, null, db, null);
                    await services.Workspace.ConnectCommand.ExecuteAsync(profile);
                    var table = services.Workspace.ActiveSession!.Schema.FindTable(null, "lignes_commande")!;
                    window.FindControl<PropertiesPanel>("Properties")!.Show(new ExplorerNode(table.Name, ExplorerNodeKind.Table, table: table), services.Workspace);

                    await services.Workspace.OpenTableCommand.ExecuteAsync(table);
                    for (var i = 0; i < 200 && services.Workspace.ActiveTab?.IsRunning != false; i++)
                        await Task.Delay(10);
                    Check(window, "query", MainSizes, found);
                    window.FindControl<RadioButton>("RailHistory")!.IsChecked = true;
                    Check(window, "history", MainSizes, found);
                    window.FindControl<RadioButton>("RailSnippets")!.IsChecked = true;
                    Check(window, "snippets", MainSizes, found);
                    window.FindControl<RadioButton>("RailExplorer")!.IsChecked = true;

                    await services.Workspace.ActiveTab!.ExplainCommand.ExecuteAsync(null);
                    Assert.NotNull(services.Workspace.ActivePlan);
                    Check(window, "plan", MainSizes, found);

                    services.Workspace.DesignTableCommand.Execute(table);
                    Check(window, "designer", MainSizes, found);
                    foreach (var index in DesignerSubTabs)
                    {
                        window.GetLogicalDescendants().OfType<TableDesignerView>().Single().GetLogicalDescendants().OfType<TabControl>().First().SelectedIndex = index;
                        Check(window, $"designer tab {index}", MainSizes, found);
                    }

                    await services.Workspace.OpenDiagramCommand.ExecuteAsync(null);
                    Check(window, "diagram", MainSizes, found);
                }
                finally
                {
                    window.Close();
                }

                var settings = new SettingsWindow { DataContext = services.Settings, Updates = services.Updates };
                settings.Show();
                foreach (var page in SettingsPages)
                {
                    settings.FindControl<RadioButton>(page)!.IsChecked = true;
                    Check(settings, page, SettingsSizes, found);
                }

                settings.Close();
                var editor = new ConnectionEditorViewModel(null, new ConnectionUiTests.NullStore(), providers, new Dialogs());
                var connections = new ConnectionEditorWindow { DataContext = editor };
                connections.Show();
                foreach (var dialect in Enum.GetValues<DatabaseDialect>())
                {
                    editor.Dialect = dialect;
                    Check(connections, $"connection {dialect}", ConnectionSizes, found);
                }

                connections.Close();
                Window[] fixedWindows = [new AboutWindow(services.About), new UpdateWindow(services.Updates), new UpdateAskWindow()];
                foreach (var fixedWindow in fixedWindows)
                {
                    fixedWindow.Show();
                    Check(fixedWindow, fixedWindow.GetType().Name, [(fixedWindow.Width is > 0 and var w ? w : 480, fixedWindow.Height is > 0 and var h ? h : 400)], found);
                    fixedWindow.Close();
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            // AppComposition.Create applies the OS language process-wide; put the test default (French) back for the other classes.
            UiText.Initialize(UiLanguage.French);
            CultureInfo.DefaultThreadCurrentCulture = UiText.Culture;
            CultureInfo.DefaultThreadCurrentUICulture = UiText.Culture;
            SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
            }
        }

        Assert.True(found.Count == 0, $"{found.Count} layout problems ({language}):\n" + string.Join("\n", found));
    }

    private static void Check(Window window, string screen, (double Width, double Height)[] sizes, List<string> found)
    {
        window.UpdateLayout();
        var content = (Control)window.Content!;
        foreach (var (width, height) in sizes)
        {
            // A headless window ignores a new Width once shown: lay its content out at the size under test instead.
            // Several passes: size-driven rules (the Properties panel hiding, the menu folding) react to one pass and the next lays out their result.
            // Every control is invalidated first: Measure on an unchanged root returns at once, leaving a control changed by such a rule stale.
            for (var pass = 0; pass < 3; pass++)
            {
                foreach (var layoutable in content.GetVisualDescendants().OfType<Layoutable>())
                    layoutable.InvalidateMeasure();
                content.InvalidateMeasure();
                content.Measure(new Size(width, height));
                content.Arrange(new Rect(0, 0, width, height));
            }
            found.AddRange(TextOverlap.Find(content).Select(o => $"{screen} @{width}x{height}: {o}"));
        }
    }
}
