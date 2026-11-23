using System.Globalization;
using Squill.Core;
using Squill.Platform.Storage;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Explorer;

public sealed partial class ExplorerTreeViewModelTests
{
    private static string Badge(int n, int m) =>
        UiText.Format("explorer.filterBadge", n.ToString(CultureInfo.InvariantCulture), m.ToString(CultureInfo.InvariantCulture));

    private (ExplorerTreeViewModel Tree, ExplorerNode Connection, ConnectionSession Root, MemoryFilters Filters, ConnectionProfile Profile) FilteredServer()
    {
        var filters = new MemoryFilters();
        var tree = new ExplorerTreeViewModel(_list, _sessions, filters);
        var profile = Profile("srv");
        _list.Profiles.Add(profile);
        var snapshot = new SchemaSnapshot(null, [Table("public", "own")]);
        var root = new ConnectionSession(profile, new FakeConnection(snapshot), snapshot, null) { Databases = ["d", "other", "bad"] };
        _sessions.Add(root);
        return (tree, tree.Roots.Single(), root, filters, profile);
    }

    private (ExplorerTreeViewModel Tree, ExplorerNode Database, ConnectionSession Session, MemoryFilters Filters, ConnectionProfile Profile) TwoSchemas(params TableInfo[] extra)
    {
        var filters = new MemoryFilters();
        var tree = new ExplorerTreeViewModel(_list, _sessions, filters);
        var profile = Profile("sch");
        _list.Profiles.Add(profile);
        var snapshot = new SchemaSnapshot("public", [Table("public", "users"), Table("public", "logs"), Table("ventes", "orders"), .. extra])
        {
            Routines = [new RoutineInfo("ventes", "f", RoutineKind.Function, "f()", "int", null)],
            Triggers = [new TriggerInfo("ventes", "orders", "tr", "BEFORE", "INSERT", null)],
        };
        var session = Session(profile, snapshot);
        _sessions.Add(session);
        return (tree, tree.Roots.Single(n => n.Profile == profile).Children.Single(), session, filters, profile);
    }

    [Fact]
    public void Databases_BadgeCountsAndHidingRemovesTheNode()
    {
        var (tree, connection, _, filters, profile) = FilteredServer();
        Assert.Equal(Badge(3, 3), connection.FilterBadge);

        tree.DatabaseLevel(profile.Id)!.Hide("other");
        tree.ApplyVisibility(profile.Id);

        Assert.Equal(["d", "bad"], connection.Children.Select(c => c.Title));
        Assert.Equal(Badge(2, 3), connection.FilterBadge);
        Assert.Equal(["other"], filters.Saved[profile.Id].Databases!.Hidden);
    }

    [Fact]
    public void Databases_PatternKeepsOnlyMatchingNames()
    {
        var (tree, connection, _, _, profile) = FilteredServer();

        tree.DatabaseLevel(profile.Id)!.AddPattern("o*");
        tree.ApplyVisibility(profile.Id);

        Assert.Equal(["other"], connection.Children.Select(c => c.Title));
        Assert.Equal(Badge(1, 3), connection.FilterBadge);
    }

    [Fact]
    public void DatabaseLevel_IsNullForAnUnknownProfile()
    {
        var (tree, _, _, _, _) = FilteredServer();

        Assert.Null(tree.DatabaseLevel(Guid.NewGuid()));
    }

    [Fact]
    public void HidingTheProfilesOwnDatabase_DoesNotThrow_AndReloadStillWorks()
    {
        var (tree, connection, root, _, profile) = FilteredServer();

        tree.DatabaseLevel(profile.Id)!.Hide("d");
        tree.ApplyVisibility(profile.Id);
        tree.Reload(root);

        Assert.DoesNotContain(connection.Children, c => c.Title == "d");
    }

    [Fact]
    public void Schemas_HidingOneRemovesItsObjectsAndUpdatesTheBadge()
    {
        var (tree, database, session, filters, profile) = TwoSchemas();
        Assert.Equal(Badge(2, 2), database.FilterBadge);

        tree.SchemaLevel(session)!.Hide("ventes");
        tree.ApplyVisibility(profile.Id);
        database = tree.Roots.Single(n => n.Profile == profile).Children.Single();

        Assert.Equal(["public"], database.Children.Select(c => c.Title));
        Assert.DoesNotContain(Descendants(database), n => n.Kind is ExplorerNodeKind.Function or ExplorerNodeKind.Trigger || n.Title == "orders");
        Assert.Equal(Badge(1, 2), database.FilterBadge);
        Assert.Equal(["ventes"], filters.Saved[profile.Id].Schemas["d"].Hidden);
    }

    [Fact]
    public void Tables_SchemaBadgeReplacesItsCount_AndHiddenCountStaysMeaningful()
    {
        var (tree, database, _, _, _) = TwoSchemas();
        var publicSchema = database.Children.Single(c => c.Title == "public");
        Assert.Equal(Badge(2, 2), publicSchema.FilterBadge);

        tree.HideTableCommand.Execute(Descendants(database).First(n => n.Title == "logs"));
        tree.HideTableCommand.Execute(Descendants(database).First(n => n.Title == "users"));

        publicSchema = database.Children.Single(c => c.Title == "public");
        Assert.Equal(2, database.HiddenCount);
        Assert.Equal(Badge(0, 2), publicSchema.FilterBadge);
        Assert.Null(publicSchema.Trailing);
    }

    [Fact]
    public void WithoutSchemas_TheDatabaseBadgeCountsTables()
    {
        var filters = new MemoryFilters();
        var tree = new ExplorerTreeViewModel(_list, _sessions, filters);
        var profile = Profile("lite");
        _list.Profiles.Add(profile);
        _sessions.Add(Session(profile, new SchemaSnapshot(null, [Table(null, "a"), Table(null, "b"), Table(null, "c")])));
        var database = tree.Roots.Single(n => n.Profile == profile).Children.Single();
        Assert.Equal(Badge(3, 3), database.FilterBadge);

        tree.HideTableCommand.Execute(Descendants(database).First(n => n.Title == "a"));

        Assert.Equal(Badge(2, 3), database.FilterBadge);
    }

    [Fact]
    public async Task SavedFile_WithAllThreeParts_IsAppliedWhenTheConnectionOpens()
    {
        var saved = new MemoryFilters();
        var profile = Profile("all");
        saved.Saved[profile.Id] = new TableFilter(new Dictionary<string, IReadOnlyList<string>> { ["d"] = ["public.logs"] })
        {
            Databases = new NameFilter(["other"], []),
            Schemas = new Dictionary<string, NameFilter> { ["d"] = new NameFilter(["ventes"], []) },
        };
        var tree = new ExplorerTreeViewModel(_list, _sessions, saved);
        _list.Profiles.Add(profile);
        var snapshot = new SchemaSnapshot(null, [Table("public", "users"), Table("public", "logs"), Table("ventes", "orders")]);

        _sessions.Add(new ConnectionSession(profile, new FakeConnection(snapshot), snapshot, null) { Databases = ["d", "other"] });
        var connection = tree.Roots.Single(n => n.Profile == profile);
        await WaitAsync(() => connection.Children.Count == 1 && connection.Children[0].Children.Count == 1);

        var database = connection.Children[0];
        Assert.Equal(["public"], database.Children.Select(c => c.Title));
        Assert.Equal(["users"], TableTitles(database));
    }

    [Fact]
    public void AHiddenSelectedTable_LeavesTheSelection()
    {
        var (tree, database, _, _, _) = TwoSchemas();
        var users = Descendants(database).First(n => n.Title == "users");
        tree.SetSelectedNodes([users]);

        tree.HideTableCommand.Execute(users);

        Assert.Empty(tree.SelectedNodes);
    }

    [Fact]
    public void ApplyVisibility_KeepsTheExpandedSchemas()
    {
        var (tree, database, session, _, profile) = TwoSchemas(Table("extra", "e"));
        database.Children.Single(c => c.Title == "ventes").IsExpanded = true;

        tree.SchemaLevel(session)!.Hide("extra");
        tree.ApplyVisibility(profile.Id);

        Assert.True(tree.Roots.Single(n => n.Profile == profile).Children.Single().Children.Single(c => c.Title == "ventes").IsExpanded);
    }
}
