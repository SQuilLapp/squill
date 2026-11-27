using Squill.Core;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;
using Xunit;

namespace Squill.Presentation.Tests.Explorer;

public sealed partial class ExplorerTreeViewModelTests
{
    private static FilterRow Row(ExplorerFilterViewModel filter, string text, int depth = 0) =>
        filter.Rows.Single(r => r.Text == text && r.Depth == depth);

    [Fact]
    public void CreateFilter_Connection_ListsAllDefaultThenSortedDatabases()
    {
        using var language = UiText.Override(UiLanguage.English);
        var (tree, connection, _, _, _) = FilteredServer();

        var filter = tree.CreateFilter(connection)!;

        Assert.Equal(["All databases", "Default database", "bad", "d", "other"], filter.Rows.Select(r => r.Text));
        Assert.Equal("(d)", filter.Rows[1].Note);
        Assert.Equal("(Default database)", Row(filter, "d").Note);
        Assert.Null(Row(filter, "bad").Note);
        Assert.All(filter.Rows.Where(r => r.Kind == FilterRowKind.Item), r => Assert.True(r.IsStrong));
        Assert.All(filter.Rows, r => Assert.True(r.IsChecked));
        Assert.True(tree.CanFilter(connection));
    }

    [Fact]
    public void CreateFilter_Database_ListsSchemas_AndSchemaOrFolderListsTables()
    {
        using var language = UiText.Override(UiLanguage.English);
        var (tree, database, _, _, _) = TwoSchemas();

        var schemas = tree.CreateFilter(database)!;
        var schema = database.Children.Single(c => c.Title == "public");
        var tables = tree.CreateFilter(schema)!;
        var folder = tree.CreateFilter(schema.Children.Single(c => c.Title == UiText.Get("explorer.tables")))!;

        Assert.Equal(["All schemas", "Default schema", "public", "ventes"], schemas.Rows.Select(r => r.Text));
        Assert.Equal("(Default schema)", Row(schemas, "public").Note);
        Assert.Equal(["All tables", "logs", "users"], tables.Rows.Select(r => r.Text));
        Assert.Equal(["All tables", "logs", "users"], folder.Rows.Select(r => r.Text));
        Assert.All(tables.Rows.Where(r => r.Kind == FilterRowKind.Item), r => Assert.False(r.IsStrong));
    }

    [Fact]
    public void CreateFilter_IsNullForAColumnAndForADisconnectedConnection()
    {
        using var language = UiText.Override(UiLanguage.English);
        var (tree, database, _, _, _) = TwoSchemas();
        var column = Descendants(database).First(n => n.Kind == ExplorerNodeKind.Column);
        _list.Profiles.Add(Profile("off"));
        var disconnected = tree.Roots.Single(n => n.Title == "off");

        Assert.Null(tree.CreateFilter(column));
        Assert.Null(tree.CreateFilter(disconnected));
        Assert.Null(tree.CreateFilter(null));
        Assert.False(tree.CanFilter(column));
    }

    [Fact]
    public void ToggleItem_HidesTheNodeAndSaves_WithoutRebuildingRows()
    {
        using var language = UiText.Override(UiLanguage.English);
        var (tree, connection, _, filters, profile) = FilteredServer();
        var filter = tree.CreateFilter(connection)!;
        var before = filter.Rows.ToList();

        Row(filter, "other").ToggleCommand.Execute(null);

        Assert.Equal(["d", "bad"], connection.Children.Select(c => c.Title));
        Assert.Equal(["other"], filters.Saved[profile.Id].Databases!.Hidden);
        Assert.Equal(before, filter.Rows);
        Assert.False(Row(filter, "other").IsChecked);
        Assert.False(Row(filter, "All databases").IsChecked);

        Row(filter, "other").ToggleCommand.Execute(null);

        Assert.True(Row(filter, "other").IsChecked);
        Assert.True(Row(filter, "All databases").IsChecked);
        Assert.Equal(3, connection.Children.Count);
    }

    [Fact]
    public void ToggleAll_HidesEverything_ThenShowsEverything()
    {
        using var language = UiText.Override(UiLanguage.English);
        var (tree, connection, _, _, _) = FilteredServer();
        var filter = tree.CreateFilter(connection)!;

        Row(filter, "All databases").ToggleCommand.Execute(null);

        Assert.Empty(connection.Children);
        Assert.All(filter.Rows, r => Assert.False(r.IsChecked));

        Row(filter, "All databases").ToggleCommand.Execute(null);

        Assert.Equal(3, connection.Children.Count);
        Assert.All(filter.Rows, r => Assert.True(r.IsChecked));
    }

    [Fact]
    public void DefaultRow_MirrorsTheDefaultItemBothWays()
    {
        using var language = UiText.Override(UiLanguage.English);
        var (tree, connection, _, _, _) = FilteredServer();
        var filter = tree.CreateFilter(connection)!;

        Row(filter, "Default database").ToggleCommand.Execute(null);

        Assert.False(Row(filter, "d").IsChecked);
        Assert.False(Row(filter, "Default database").IsChecked);
        Assert.DoesNotContain(connection.Children, c => c.Title == "d");

        Row(filter, "d").ToggleCommand.Execute(null);

        Assert.True(Row(filter, "Default database").IsChecked);
    }

    [Fact]
    public async Task ExpandingAnOpenedDatabase_AddsItsSchemasAtDepthOne()
    {
        using var language = UiText.Override(UiLanguage.English);
        var (_, root, _) = ConnectServer(Profile("p"));
        await root.OpenDatabaseAsync("other", CancellationToken.None);
        var filter = _tree.CreateFilter(_tree.Roots.Single())!;

        Assert.True(Row(filter, "d").CanExpand);
        Assert.True(Row(filter, "other").CanExpand);
        Assert.False(Row(filter, "bad").CanExpand);
        var count = filter.Rows.Count;

        Row(filter, "other").ToggleExpandCommand.Execute(null);

        Assert.Equal(count + 2, filter.Rows.Count);
        Assert.True(Row(filter, "other").IsExpanded);
        Assert.Equal("All schemas", filter.Rows[filter.Rows.IndexOf(Row(filter, "other")) + 1].Text);
        Assert.Equal(1, Row(filter, "public", 1).Depth);
        Assert.False(Row(filter, "public", 1).IsStrong);

        filter.CollapseAllCommand.Execute(null);
        Assert.Equal(count, filter.Rows.Count);

        filter.ExpandAllCommand.Execute(null);
        Assert.Equal(count + 4, filter.Rows.Count);
    }

    [Fact]
    public void Pattern_OnTheDatabasesLevel_ShowsMatchesOnly_AndRemoveRestores()
    {
        using var language = UiText.Override(UiLanguage.English);
        var (tree, connection, _, _, _) = FilteredServer();
        var filter = tree.CreateFilter(connection)!;
        var all = Row(filter, "All databases");

        all.StartPatternCommand.Execute(null);
        all.PatternText = "o*";
        all.CommitPatternCommand.Execute(null);

        Assert.Equal(["other"], connection.Children.Select(c => c.Title));
        var pattern = filter.Rows.Single(r => r.Kind == FilterRowKind.Pattern);
        Assert.Equal("o*", pattern.Text);
        Assert.False(Row(filter, "All databases").IsChecked);

        pattern.RemovePatternCommand.Execute(null);

        Assert.DoesNotContain(filter.Rows, r => r.Kind == FilterRowKind.Pattern);
        Assert.Equal(3, connection.Children.Count);
    }

    [Fact]
    public void Pattern_BlankOrDuplicate_IsRefusedAndKeepsTheBoxOpen()
    {
        using var language = UiText.Override(UiLanguage.English);
        var (tree, connection, _, _, profile) = FilteredServer();
        var filter = tree.CreateFilter(connection)!;
        var all = Row(filter, "All databases");
        all.StartPatternCommand.Execute(null);
        all.PatternText = "  ";

        all.CommitPatternCommand.Execute(null);

        Assert.True(all.IsAddingPattern);
        Assert.Empty(tree.DatabaseLevel(profile.Id)!.Patterns);

        all.PatternText = "o*";
        all.CommitPatternCommand.Execute(null);
        all = Row(filter, "All databases");
        all.StartPatternCommand.Execute(null);
        all.PatternText = "O*";
        all.CommitPatternCommand.Execute(null);

        Assert.True(all.IsAddingPattern);
        Assert.Single(tree.DatabaseLevel(profile.Id)!.Patterns);

        all.CancelPatternCommand.Execute(null);
        Assert.False(all.IsAddingPattern);
    }

    [Fact]
    public void Pattern_OnATablesLevel_IsStoredWithTheSchemaPrefix_AndShownWithout()
    {
        using var language = UiText.Override(UiLanguage.English);
        var (tree, database, session, _, _) = TwoSchemas();
        var filter = tree.CreateFilter(database.Children.Single(c => c.Title == "public"))!;
        var all = Row(filter, "All tables");

        all.StartPatternCommand.Execute(null);
        all.PatternText = "u*";
        all.CommitPatternCommand.Execute(null);

        Assert.Equal(["public.u*"], tree.TableLevel(session)!.Patterns);
        Assert.Equal("u*", filter.Rows.Single(r => r.Kind == FilterRowKind.Pattern).Text);
        Assert.False(Row(filter, "logs").IsChecked);
        Assert.True(Row(filter, "users").IsChecked);
    }

    [Fact]
    public void ToggleAll_OnATablesLevel_OnlyTouchesItsSchema()
    {
        using var language = UiText.Override(UiLanguage.English);
        var (tree, database, session, _, _) = TwoSchemas();
        var filter = tree.CreateFilter(database.Children.Single(c => c.Title == "public"))!;

        Row(filter, "All tables").ToggleCommand.Execute(null);

        Assert.True(tree.TableLevel(session)!.IsVisible("ventes.orders"));
        Assert.False(tree.TableLevel(session)!.IsVisible("public.users"));
        Assert.False(Row(filter, "users").IsChecked);
    }

    [Fact]
    public async Task Search_KeepsTheParentOfAMatchingChild_Expanded()
    {
        using var language = UiText.Override(UiLanguage.English);
        var (_, root, _) = ConnectServer(Profile("p"));
        await root.OpenDatabaseAsync("other", CancellationToken.None);
        var filter = _tree.CreateFilter(_tree.Roots.Single())!;

        filter.SearchText = "pub";

        Assert.DoesNotContain(filter.Rows, r => r.Text == "bad");
        Assert.True(Row(filter, "other").IsExpanded);
        Assert.Contains(filter.Rows, r => r.Text == "public" && r.Depth == 1);
        Assert.Contains(filter.Rows, r => r.Text == "All databases");

        filter.SearchText = "";

        Assert.Contains(filter.Rows, r => r.Text == "bad");
    }

    [Fact]
    public void Texts_AreLocalized()
    {
        using var language = UiText.Override(UiLanguage.English);
        var (tree, connection, _, _, _) = FilteredServer();

        using (UiText.Override(UiLanguage.French))
        {
            var fr = tree.CreateFilter(connection)!;
            Assert.Equal("Toutes les bases", fr.Rows[0].Text);
            Assert.Equal("Base par défaut", fr.Rows[1].Text);
            Assert.Equal("(base par défaut)", Row(fr, "d").Note);
        }

        using (UiText.Override(UiLanguage.English))
        {
            var en = tree.CreateFilter(connection)!;
            Assert.Equal("All databases", en.Rows[0].Text);
            Assert.Equal("Default database", en.Rows[1].Text);
        }
    }

    [Fact]
    public async Task Refresh_RereadsTheSchemaOfTheSessionsInScope()
    {
        using var language = UiText.Override(UiLanguage.English);
        var profile = Profile("r");
        _list.Profiles.Add(profile);
        _sessions.Add(Session(profile, new SchemaSnapshot(null, [Table("public", "a")]), new SchemaSnapshot(null, [Table("public", "a"), Table("public", "b")])));
        var database = _tree.Roots.Single().Children.Single();
        var filter = _tree.CreateFilter(database.Children.Single(c => c.Kind == ExplorerNodeKind.Schema))!;
        Assert.DoesNotContain(filter.Rows, r => r.Text == "b");

        await filter.RefreshCommand.ExecuteAsync(null);

        Assert.Contains(filter.Rows, r => r.Text == "b");
    }
}
