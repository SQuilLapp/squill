using Avalonia.Controls;
using Avalonia.LogicalTree;
using Squill.App.Avalonia.Editors;
using Squill.App.Avalonia.Services;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class CellPreviewPanelUiTests
{
    private sealed class History : IQueryHistoryStore
    {
        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<HistoryEntry>>([]);

        public ValueTask ClearAsync(CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class Settings : ISettingsStore
    {
        public ValueTask<AppSettings> LoadAsync(CancellationToken ct) => ValueTask.FromResult(AppSettings.Default);

        public ValueTask SaveAsync(AppSettings settings, CancellationToken ct) => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task PreviewPane_TruncatedValue_ShowsHeadNoticeAndIsReadOnly()
    {
        var file = Path.Combine(Path.GetTempPath(), $"squill-preview-{Guid.NewGuid():N}.db");
        await Headless.Session.Dispatch(async () =>
        {
            var profile = new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.Sqlite, null, null, null, null, file, null);
            await using var connection = await new SqliteDatabaseProvider().OpenAsync(profile, null, CancellationToken.None);
            var schema = await connection.ReadSchemaAsync(CancellationToken.None);
            var editor = new AvaloniaSqlEditor { Text = "SELECT replace(hex(zeroblob(75000)), '0', 'k') AS t;" };
            var tab = new QueryTabViewModel(new ConnectionSession(profile, connection, schema, null), editor, new History(), new Settings(), new UiThreadDispatcher(), new Dialogs());
            await tab.RunCommand.ExecuteAsync(null);
            var result = tab.Results[0];
            var pane = new CellPreviewPanel();
            pane.Attach(result.Preview);

            result.Selection!.SelectCell(0, 0, false);

            var preview = result.Preview!;
            Assert.True(preview.IsTruncated);
            var box = pane.GetLogicalDescendants().OfType<TextBox>().Single();
            Assert.Equal(preview.Text, box.Text);
            Assert.Equal(CellPreviewViewModel.MaxShownChars, box.Text!.Length);
            Assert.True(box.IsReadOnly);
            var texts = pane.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.IsVisible).Select(t => t.Text).ToArray();
            Assert.Contains(preview.Notice, texts);
            var buttons = pane.GetLogicalDescendants().OfType<Button>().ToArray();
            Assert.Contains(buttons, b => b.IsVisible && b.IsEnabled && (string?)b.Content == UiText.Get("preview.showAll"));
        }, TestContext.Current.CancellationToken);
        try
        {
            File.Delete(file);
        }
        catch (IOException)
        {
        }
    }
}
