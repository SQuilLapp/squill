using Avalonia.Controls;
using Squill.App.Avalonia.Editors;
using Squill.App.Avalonia.Services;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Data;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class MultiResultUiTests
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

    /// <summary>Returns two one-column sets (2 and 4 rows) then two server messages.</summary>
    private sealed class MultiReader : IResultReader
    {
        private static readonly int[] Sets = [2, 4];
        private int _set;
        private bool _read;

        public IReadOnlyList<ColumnInfo> Columns => [new("c", "int", typeof(int), true, 0)];
        public long RecordsAffected => -1;
        public IReadOnlyList<string> Messages => ["hello", "world"];

        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken)
        {
            if (_read)
                return ValueTask.FromResult<object?[][]?>(null);
            _read = true;
            return ValueTask.FromResult<object?[][]?>([.. Enumerable.Range(0, Sets[_set]).Select(i => new object?[] { i })]);
        }

        public ValueTask<bool> NextResultAsync(CancellationToken cancellationToken)
        {
            _read = false;
            return ValueTask.FromResult(++_set < Sets.Length);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Connection : IDatabaseConnection
    {
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IResultReader>(new MultiReader());

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task TwoSetsAndMessages_ShowThreeTabs_AndTheLastShowsTheText()
    {
        using var _ = UiText.Override(UiLanguage.English);
        await await Headless.Session.Dispatch(async () =>
        {
            var profile = new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.PostgreSql, "h", 1, "d", "u", null, null);
            var editor = new AvaloniaSqlEditor { Text = "SELECT 1;" };
            var tab = new QueryTabViewModel(new ConnectionSession(profile, new Connection(), SchemaSnapshot.Empty, null), editor, new History(), new Settings(), new ImmediateDispatcher(), new Dialogs());
            var view = new QueryTabView { DataContext = tab };
            var window = new Window { Width = 900, Height = 500, Content = view };
            window.Show();
            try
            {
                await tab.RunCommand.ExecuteAsync(null);

                var strip = view.FindControl<ListBox>("ResultTabs")!;
                Assert.Equal(3, strip.ItemCount);
                var message = view.FindControl<SelectableTextBlock>("Message")!;
                tab.SelectedResult = tab.Results[0];
                Assert.False(message.IsVisible);
                Assert.True(view.FindControl<Control>("ResultGrid")!.IsVisible);

                tab.SelectedResult = tab.Results[2];

                Assert.True(message.IsVisible);
                Assert.Equal("hello" + Environment.NewLine + "world", message.Text);
                Assert.Contains("mono", message.Classes);
                Assert.False(view.FindControl<Control>("ResultGrid")!.IsVisible);
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken);
    }
}
