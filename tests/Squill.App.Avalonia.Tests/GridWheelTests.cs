using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Squill.App.Avalonia.Controls;
using Squill.App.Avalonia.Editors;
using Squill.App.Avalonia.Services;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Data;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class GridWheelTests
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

    private sealed class Wide : IResultReader
    {
        private bool _read;

        public IReadOnlyList<ColumnInfo> Columns { get; } = [.. Enumerable.Range(0, 20).Select(i => new ColumnInfo("column_" + i, "int", typeof(int), true, i))];
        public long RecordsAffected => -1;
        public IReadOnlyList<string> Messages => [];

        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken)
        {
            if (_read)
                return ValueTask.FromResult<object?[][]?>(null);
            _read = true;
            return ValueTask.FromResult<object?[][]?>([.. Enumerable.Range(0, 200).Select(i => Enumerable.Range(0, 20).Select(c => (object?)(i * c)).ToArray())]);
        }

        public ValueTask<bool> NextResultAsync(CancellationToken cancellationToken) => ValueTask.FromResult(false);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Connection : IDatabaseConnection
    {
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IResultReader>(new Wide());

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Wheel_ScrollsTheGridInBothDirections()
    {
        await Headless.Session.Dispatch(async () =>
        {
            var profile = new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.PostgreSql, "h", 1, "d", "u", null, null);
            var editor = new AvaloniaSqlEditor { Text = "SELECT 1;" };
            var tab = new QueryTabViewModel(new ConnectionSession(profile, new Connection(), SchemaSnapshot.Empty, null), editor, new History(), new Settings(), new ImmediateDispatcher(), new Dialogs());
            var view = new QueryTabView { DataContext = tab };
            var window = new Window { Width = 900, Height = 600, Content = view };
            window.Show();
            try
            {
                await tab.RunCommand.ExecuteAsync(null);
                await tab.RunCommand.ExecuteAsync(null);
                var grid = view.FindControl<DataGridView>("ResultGrid")!;
                var origin = grid.TranslatePoint(new Point(200, 100), window)!.Value;
                window.MouseDown(origin, MouseButton.Left);
                window.MouseUp(origin, MouseButton.Left);
                window.MouseDown(origin, MouseButton.Left);
                window.MouseUp(origin, MouseButton.Left);
                window.MouseWheel(origin, new Vector(0, -1));
                Assert.True(grid.Canvas.ScrollY > 0);
                window.MouseWheel(origin, new Vector(-1, 0));
                Assert.True(grid.Canvas.ScrollX > 0);
                var before = grid.Canvas.ScrollY;
                var bar = grid.TranslatePoint(new Point(grid.Bounds.Width - 5, 100), window)!.Value;
                window.MouseWheel(bar, new Vector(0, -1));
                Assert.True(grid.Canvas.ScrollY > before);
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken);
    }
}
