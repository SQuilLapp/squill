using Avalonia.Controls;
using Squill.App.Avalonia.Editors;
using Squill.App.Avalonia.Services;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Core.Transactions;
using Squill.Sql.Transactions;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class EditorToolbarTests
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

    private static async Task WithViewAsync(Func<QueryTabView, QueryTabViewModel, Task> body)
    {
        var file = Path.Combine(Path.GetTempPath(), $"squill-toolbar-{Guid.NewGuid():N}.db");
        await Headless.Session.Dispatch(async () =>
        {
            var profile = new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.Sqlite, null, null, null, null, file, null);
            await using var connection = await new SqliteDatabaseProvider().OpenAsync(profile, null, CancellationToken.None);
            var schema = await connection.ReadSchemaAsync(CancellationToken.None);
            var tab = new QueryTabViewModel(new ConnectionSession(profile, connection, schema, null), new AvaloniaSqlEditor(), new History(), new Settings(), new ImmediateDispatcher(), new Dialogs());
            var view = new QueryTabView { DataContext = tab };
            var window = new Window { Width = 900, Height = 500, Content = view };
            window.Show();
            try
            {
                await body(view, tab);
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken);
        try
        {
            File.Delete(file);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public Task Toolbar_BindsTheTabCommands_AndStopIsDisabledWhileIdle() => WithViewAsync((view, tab) =>
    {
        Assert.Same(tab.RunCommand, view.FindControl<Button>("RunButton")!.Command);
        Assert.Same(tab.RunAllCommand, view.FindControl<Button>("ScriptButton")!.Command);
        Assert.Same(tab.CancelCommand, view.FindControl<Button>("StopButton")!.Command);
        Assert.Same(tab.FormatDocumentCommand, view.FindControl<Button>("FormatButton")!.Command);
        Assert.False(tab.IsRunning);
        Assert.False(view.FindControl<Button>("StopButton")!.IsEnabled);
        Assert.Equal(UiText.Get("editor.run"), ((TextBlock)((StackPanel)view.FindControl<Button>("RunButton")!.Content!).Children[0]).Text);
        return Task.CompletedTask;
    });

    private sealed class NoProbeConnection : IDatabaseConnection
    {
        public string ServerVersion => "x";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static async Task OpenInsertAsync(QueryTabViewModel tab, int statements)
    {
        const string insert = "INSERT INTO t VALUES (1)";
        var traits = StatementClassifier.Classify(insert, DatabaseDialect.Sqlite);
        await tab.Transaction.EnsureOpenAsync(traits, CancellationToken.None);
        for (var i = 0; i < statements; i++)
            tab.Transaction.ObserveSuccess(insert, traits, 1, "t");
    }

    [Fact]
    public Task TransactionGroup_FollowsModeAndState_ForZeroOneAndThreeStatements() => WithViewAsync(async (view, tab) =>
    {
        var manual = view.FindControl<RadioButton>("TxManualButton")!;
        var commit = view.FindControl<Button>("TxCommitButton")!;
        var rollback = view.FindControl<Button>("TxRollbackButton")!;
        var pill = view.FindControl<Border>("TxPill")!;
        Assert.True(view.FindControl<RadioButton>("TxAutoButton")!.IsChecked);
        Assert.False(commit.IsVisible);
        Assert.False(rollback.IsVisible);
        Assert.False(pill.IsVisible);

        manual.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(TransactionMode.Manual, tab.Transaction.Mode);
        Assert.True(commit.IsVisible);
        Assert.True(rollback.IsVisible);
        Assert.False(pill.IsVisible);

        await OpenInsertAsync(tab, 1);
        Assert.True(pill.IsVisible);
        Assert.Equal(UiText.FormatPlural("tx.pill", 1), view.FindControl<TextBlock>("TxPillText")!.Text);
        Assert.Contains("warning", pill.Classes);

        await OpenInsertAsync(tab, 2);
        Assert.Equal(UiText.FormatPlural("tx.pill", 3), view.FindControl<TextBlock>("TxPillText")!.Text);
    });

    [Fact]
    public Task TransactionGroup_ButtonsStayVisibleInAutoCommitWhileATransactionIsOpen() => WithViewAsync(async (view, tab) =>
    {
        await OpenInsertAsync(tab, 1);
        Assert.Equal(TransactionMode.AutoCommit, tab.Transaction.Mode);
        Assert.True(view.FindControl<Button>("TxCommitButton")!.IsVisible);
        Assert.True(view.FindControl<Button>("TxRollbackButton")!.IsVisible);
    });

    [Fact]
    public Task TransactionToggle_IsDisabledWithAReason_WhenTheEngineCannotProbe() => WithViewAsync((view, tab) =>
    {
        Assert.True(tab.Transaction.IsSupported);
        Assert.True(view.FindControl<StackPanel>("TxMode")!.IsEnabled);
        return Task.CompletedTask;
    });

    [Fact]
    public async Task TransactionToggle_Unsupported_IsDisabledWithTheReasonTooltip()
    {
        await Headless.Session.Dispatch(async () =>
        {
            var profile = new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.Sqlite, null, null, null, null, "x.db", null);
            var tab = new QueryTabViewModel(new ConnectionSession(profile, new NoProbeConnection(), new SchemaSnapshot("main", []), null), new AvaloniaSqlEditor(), new History(), new Settings(), new ImmediateDispatcher(), new Dialogs());
            var view = new QueryTabView { DataContext = tab };
            var window = new Window { Width = 900, Height = 500, Content = view };
            window.Show();
            var toggle = view.FindControl<StackPanel>("TxMode")!;
            Assert.False(toggle.IsEnabled);
            Assert.Equal(UiText.Get("tx.unsupported"), ToolTip.GetTip(toggle));
            Assert.False(view.FindControl<Button>("TxCommitButton")!.IsVisible);
            window.Close();
            await Task.CompletedTask;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task HistoryButton_TogglesTheSidePanel() => WithViewAsync((view, _) =>
    {
        var panel = view.FindControl<Border>("SidePanel")!;
        var button = view.FindControl<Button>("HistoryButton")!;
        Assert.False(panel.IsVisible);

        button.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.True(panel.IsVisible);

        button.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.False(panel.IsVisible);
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData(ConnectionEnvironment.None, false)]
    [InlineData(ConnectionEnvironment.Production, true)]
    [InlineData(ConnectionEnvironment.Local, true)]
    public Task EnvironmentStripe_ShowsOnlyForAnEnvironment(ConnectionEnvironment environment, bool visible) => WithViewAsync((view, _) =>
    {
        view.SetEnvironment(environment);
        Assert.Equal(visible, view.FindControl<Border>("EnvStripe")!.IsVisible);
        return Task.CompletedTask;
    });

    [Fact]
    public void ParameterRow_NeedsAValueUnlessNullIsPassed()
    {
        var row = new ParameterPromptRow("id");
        Assert.Equal(UiText.Get("editor.valueRequired"), row.Error);
        Assert.False(ParameterPromptRow.AllValid([row]));

        row.Text = "7";
        Assert.Null(row.Error);

        row.Text = "";
        row.IsNull = true;
        Assert.Null(row.Error);
        Assert.True(ParameterPromptRow.AllValid([row, new ParameterPromptRow("x") { Text = "a" }]));
    }

    [Fact]
    public void HistoryDetail_ShowsTimeDurationAndKnownRowCount()
    {
        using var language = UiText.Override(UiLanguage.French);
        var now = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        var today = new HistoryEntry(now, Guid.Empty, "select 1", true, 1208, TimeSpan.FromMilliseconds(42));
        var older = new HistoryEntry(now.AddDays(-3), Guid.Empty, "select 1", false, null, TimeSpan.FromSeconds(2.5));

        var first = HistoryFormat.Detail(today, now);
        var second = HistoryFormat.Detail(older, now);

        Assert.StartsWith(now.ToLocalTime().ToString("t", UiText.Culture), first);
        Assert.Contains("42 ms", first);
        Assert.EndsWith("lignes", first);
        Assert.Contains("1", first);
        Assert.StartsWith(now.AddDays(-3).ToLocalTime().ToString("g", UiText.Culture), second);
        Assert.EndsWith("2,5 s", second);
    }
}
