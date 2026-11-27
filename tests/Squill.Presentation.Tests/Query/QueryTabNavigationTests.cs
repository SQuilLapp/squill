using Squill.Core;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation.Editor;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Query;

public sealed class QueryTabNavigationTests : IDisposable
{
    private readonly IDisposable _language = UiText.Override(UiLanguage.English);

    public void Dispose() => _language.Dispose();

    private static readonly TableInfo Users = new("public", "users", TableKind.Table, [new ColumnDefinition("id", "int", false, true, null)], [], []);

    private static readonly SchemaSnapshot Schema = new("public", [Users]);

    private sealed class Store : ISettingsStore
    {
        public ValueTask<AppSettings> LoadAsync(CancellationToken ct) => ValueTask.FromResult(AppSettings.Default);

        public ValueTask SaveAsync(AppSettings settings, CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class History : IQueryHistoryStore
    {
        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<HistoryEntry>>([]);

        public ValueTask ClearAsync(CancellationToken ct) => ValueTask.CompletedTask;
    }

    private static (QueryTabViewModel Tab, StubSqlEditor Editor, List<(TableInfo Table, bool Designer)> Requests) Create(string marked)
    {
        var profile = new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.PostgreSql, "h", 1, "d", "u", null, null);
        var offset = marked.IndexOf('|', StringComparison.Ordinal);
        var editor = new StubSqlEditor { Text = marked.Remove(offset, 1) };
        var tab = new QueryTabViewModel(new ConnectionSession(profile, null!, Schema, null), editor, new History(), new Store(), new ImmediateDispatcher(), new FakeDialogService());
        editor.CaretOffset = offset;
        List<(TableInfo, bool)> requests = [];
        tab.TableDefinitionRequested += (t, d) => requests.Add((t, d));
        return (tab, editor, requests);
    }

    [Fact]
    public void AliasUse_MovesTheCaretToTheDeclarationAndSelectsIt()
    {
        var (tab, editor, requests) = Create("select u.id from users u where |u.id > 1");

        tab.GoToDefinitionCommand.Execute(null);

        var declaration = editor.Text.LastIndexOf("u where", StringComparison.Ordinal);
        Assert.Equal(new TextRange(declaration, 1), editor.Selection);
        Assert.Equal(declaration, editor.CaretOffset);
        Assert.Empty(requests);
    }

    [Fact]
    public void TableName_AsksForTheExplorerNode_AndTheDesignerVariantAsksForTheDesigner()
    {
        var (tab, _, requests) = Create("select * from us|ers");

        tab.GoToDefinitionCommand.Execute(null);
        tab.GoToDefinitionInDesignerCommand.Execute(null);

        Assert.Equal([(Users, false), (Users, true)], requests);
    }

    [Fact]
    public void ColumnOfASchemaTable_SelectsItsTable()
    {
        var (tab, _, requests) = Create("select us|ers.id from users");

        tab.GoToDefinitionCommand.Execute(null);

        Assert.Equal((Users, false), Assert.Single(requests));
    }

    [Fact]
    public void UnknownName_SetsTheNoDefinitionStatus()
    {
        var (tab, _, requests) = Create("select * from nope|");

        tab.GoToDefinitionCommand.Execute(null);

        Assert.Empty(requests);
        Assert.Equal("No definition found for the name under the caret", tab.StatusText);
    }

    [Fact]
    public void FindUsages_HighlightsEveryUseAndListsThem_AndEscapeClears()
    {
        var (tab, editor, _) = Create("select u.id\nfrom users |u\nwhere u.id > 1");

        tab.FindUsagesCommand.Execute(null);

        Assert.Equal(3, editor.Highlights.Count);
        var result = Assert.Single(tab.Results);
        Assert.Equal(ResultKind.Message, result.Kind);
        Assert.Equal("1:8  select u.id\r\n2:12  from users u\r\n3:7  where u.id > 1".Replace("\r\n", Environment.NewLine, StringComparison.Ordinal), result.Text);
        Assert.Equal("3 usages of u", tab.StatusText);

        tab.ClearHighlightsCommand.Execute(null);

        Assert.Empty(editor.Highlights);
    }

    [Fact]
    public void FindUsages_OnNothing_ClearsAndSaysSo()
    {
        var (tab, editor, _) = Create("select 1| from users");
        editor.SetHighlights([new TextRange(0, 1)]);

        tab.FindUsagesCommand.Execute(null);

        Assert.Empty(editor.Highlights);
        Assert.Empty(tab.Results);
        Assert.Equal("No usages found for the name under the caret", tab.StatusText);
    }
}
