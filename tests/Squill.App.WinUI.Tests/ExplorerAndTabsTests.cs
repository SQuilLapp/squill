using Squill.App.WinUI.Views;
using Squill.Core;
using Squill.Presentation.Connections;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;
using Xunit;

namespace Squill.App.WinUI.Tests;

public class ExplorerAndTabsTests
{
    private static ConnectionProfile P(string name, string? folder, ConnectionEnvironment environment = ConnectionEnvironment.Production, DatabaseDialect dialect = DatabaseDialect.PostgreSql) =>
        new(Guid.NewGuid(), name, dialect, "h", 5432, "d", "u", null, null, environment, folder);

    [Fact]
    public void Rows_group_by_folder_with_ungrouped_last_and_no_header()
    {
        IReadOnlyList<ConnectionGroup> groups = [new("Clients", [P("b", "Clients"), P("a", "Clients")]), new(null, [P("z", null)])];

        var rows = ConnectionRow.Build(groups);

        Assert.Equal(4, rows.Count);
        Assert.True(rows[0].IsHeader);
        Assert.Equal("Clients", rows[0].Folder);
        Assert.Equal(2, rows[0].Count);
        Assert.True(rows[1].IsProfile && rows[1].Indented);
        Assert.Equal(16, rows[1].IndentLeft);
        Assert.Equal("EngineBadgePostgresStyle", rows[1].EngineStyleKey);
        Assert.Equal("BadgeEnvProdStyle", rows[1].EnvironmentStyleKey);
        Assert.Equal(UiText.Get("status.production"), rows[1].EnvironmentText);
        Assert.True(rows[3].IsProfile && !rows[3].Indented);
        Assert.Equal(0, rows[3].IndentLeft);
    }

    [Fact]
    public void Rows_mark_connected_profiles_and_hide_missing_environment()
    {
        var open = P("open", null, ConnectionEnvironment.None, DatabaseDialect.Sqlite);
        var closed = P("closed", null, ConnectionEnvironment.None, DatabaseDialect.Sqlite);

        var rows = ConnectionRow.Build([new(null, [open, closed])], new HashSet<Guid> { open.Id });

        Assert.True(rows[0].IsConnected);
        Assert.False(rows[1].IsConnected);
        Assert.False(rows[0].HasEnvironment);
        Assert.Equal("SL", rows[0].EngineLetters);
        Assert.Equal("EngineBadgeSqliteStyle", rows[0].EngineStyleKey);
    }

    [Theory]
    [InlineData(ConnectionEnvironment.Production, "Prod")]
    [InlineData(ConnectionEnvironment.Staging, "Staging")]
    [InlineData(ConnectionEnvironment.Development, "Dev")]
    [InlineData(ConnectionEnvironment.Local, "Local")]
    [InlineData(ConnectionEnvironment.None, "")]
    public void Tab_style_suffix_follows_the_environment(ConnectionEnvironment environment, string suffix) =>
        Assert.Equal(suffix, TabInfo.StyleSuffix(environment));

    private static TableInfo Table(TableKind kind = TableKind.Table, string schema = "public") =>
        new(
            schema,
            "clients",
            kind,
            [new("id", "integer", false, true, null), new("pays_id", "integer", true, false, null), new("nom", "text", true, false, null)],
            [new("idx_nom", false, ["nom"])],
            [new("fk_pays", ["pays_id"], null, "pays", ["id"], ReferentialAction.NoAction, ReferentialAction.NoAction)]);

    [Fact]
    public void Properties_model_exists_only_for_tables_and_views()
    {
        Assert.Null(PropertiesModel.For(null));
        Assert.Null(PropertiesModel.For(new ExplorerNode("c", ExplorerNodeKind.Column)));
        Assert.Null(PropertiesModel.For(new ExplorerNode("public", ExplorerNodeKind.Schema)));
        Assert.NotNull(PropertiesModel.For(new ExplorerNode("clients", ExplorerNodeKind.Table, table: Table())));
        Assert.True(PropertiesModel.For(new ExplorerNode("v", ExplorerNodeKind.View, table: Table(TableKind.View)))!.IsView);
    }

    [Fact]
    public void Properties_model_lists_columns_with_key_kinds_and_indexes()
    {
        var model = PropertiesModel.For(new ExplorerNode("clients", ExplorerNodeKind.Table, table: Table()))!;

        Assert.Equal(["id", "pays_id", "nom"], model.Columns.Select(c => c.Name));
        Assert.Equal([true, false, false], model.Columns.Select(c => c.IsPrimaryKey));
        Assert.Equal([false, true, false], model.Columns.Select(c => c.IsForeignOnly));
        Assert.Equal([false, false, true], model.Columns.Select(c => c.IsPlain));
        Assert.Equal(["idx_nom"], model.Indexes);
        Assert.Equal(UiText.Format("properties.columns", 3), model.ColumnsHeader);
        Assert.Equal(UiText.Format("properties.indexes", 1), model.IndexesHeader);
        Assert.Equal("Table · public", model.Subtitle);
    }

    [Fact]
    public void Properties_subtitle_drops_an_empty_schema() =>
        Assert.Equal("Vue", new PropertiesModel(Table(TableKind.View, "")).Subtitle);
}
