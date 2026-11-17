using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;
using Squill.Platform.Settings;
using Squill.Platform.Ssh;
using Squill.Platform.Storage;
using Squill.Presentation.Connections;
using Squill.Presentation.Editor;
using Squill.Presentation.Home;
using Squill.Presentation.Localization;
using Squill.Presentation.Sessions;
using Squill.Presentation.Workspace;
using Xunit;

namespace Squill.Presentation.Tests.Home;

public sealed class HomeViewModelTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    private readonly List<ConnectionProfile> _profiles = [];
    private readonly FakeRecent _recent = new();
    private readonly FakeSettings _settings = new();
    private readonly FakeProvider _provider = new();
    private readonly WorkspaceViewModel _workspace;

    public HomeViewModelTests()
    {
        var registry = new ProviderRegistry();
        registry.Register(_provider);
        var store = new FakeStore(_profiles);
        var dialogs = new FakeDialogService();
        _workspace = new WorkspaceViewModel(
            new SessionManager(registry, store, new FakeTunnels()),
            new ConnectionListViewModel(store, registry, dialogs),
            new FakeHistory(),
            _settings,
            new ImmediateDispatcher(),
            dialogs,
            () => new StubSqlEditor(),
            store,
            time: new FixedClock(),
            recent: _recent);
    }

    public void Dispose() => _workspace.Dispose();

    private static ConnectionProfile Profile(string name, ConnectionEnvironment env = ConnectionEnvironment.None) =>
        new(Guid.NewGuid(), name, DatabaseDialect.PostgreSql, "db.local", 5432, "shop", "bob", null, null, env);

    private async Task LoadAsync()
    {
        await _workspace.ConnectionList.LoadCommand.ExecuteAsync(null);
        await _workspace.InitializeHomeAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Initialize_SevenRecents_ShowsSixNewestFirstWithShortcuts()
    {
        for (var i = 0; i < 7; i++)
        {
            var p = Profile($"c{i}");
            _profiles.Add(p);
            _recent.Items.Add(new RecentConnection(p.Id, Now.AddHours(-i - 1)));
        }

        await LoadAsync();

        Assert.Equal(["c0", "c1", "c2", "c3", "c4", "c5"], _workspace.Home.RecentCards.Select(c => c.Name));
        Assert.Equal(["Ctrl+1", "Ctrl+2", "Ctrl+3", "Ctrl+4", "Ctrl+5", "Ctrl+6"], _workspace.Home.RecentCards.Select(c => c.Shortcut));
    }

    [Fact]
    public async Task Initialize_DeletedProfile_IsSkippedAndForgotten()
    {
        var kept = Profile("kept");
        var gone = Profile("gone");
        _profiles.Add(kept);
        _recent.Items.Add(new RecentConnection(gone.Id, Now.AddHours(-1)));
        _recent.Items.Add(new RecentConnection(kept.Id, Now.AddHours(-2)));

        await LoadAsync();

        Assert.Equal(["kept"], _workspace.Home.RecentCards.Select(c => c.Name));
        Assert.Equal([gone.Id], _recent.Forgotten);
    }

    [Fact]
    public async Task Card_Subtitle_HasUserHostPortDatabaseAndNoPassword()
    {
        var p = Profile("prod", ConnectionEnvironment.Production);
        _profiles.Add(p);
        _recent.Items.Add(new RecentConnection(p.Id, Now.AddMinutes(-12)));

        await LoadAsync();

        var card = Assert.Single(_workspace.Home.RecentCards);
        Assert.Equal("bob@db.local:5432/shop", card.Subtitle);
        Assert.DoesNotContain("secret", card.Subtitle);
        Assert.Equal("PG", card.EngineLetters);
    }

    [Theory]
    [InlineData(ConnectionEnvironment.Production, UiLanguage.French, "PROD")]
    [InlineData(ConnectionEnvironment.Staging, UiLanguage.English, "STAGING")]
    [InlineData(ConnectionEnvironment.Development, UiLanguage.English, "DEV")]
    [InlineData(ConnectionEnvironment.Local, UiLanguage.French, "LOCAL")]
    [InlineData(ConnectionEnvironment.None, UiLanguage.French, "")]
    public async Task Card_EnvironmentText_IsTheShortForm(ConnectionEnvironment env, UiLanguage language, string expected)
    {
        using var _ = UiText.Override(language);
        var p = Profile("x", env);
        _profiles.Add(p);
        _recent.Items.Add(new RecentConnection(p.Id, Now));

        await LoadAsync();

        Assert.Equal(expected, _workspace.Home.RecentCards.Single().EnvironmentText);
    }

    [Fact]
    public async Task Card_FileProfile_ShowsThePath()
    {
        var p = new ConnectionProfile(Guid.NewGuid(), "lite", DatabaseDialect.Sqlite, null, null, null, null, @"C:\data\app.db", null);
        _profiles.Add(p);
        _recent.Items.Add(new RecentConnection(p.Id, Now));

        await LoadAsync();

        Assert.Equal(@"C:\data\app.db", _workspace.Home.RecentCards.Single().Subtitle);
    }

    [Theory]
    [InlineData(UiLanguage.French, -12 * 60, "il y a 12 min")]
    [InlineData(UiLanguage.English, -12 * 60, "12 min ago")]
    [InlineData(UiLanguage.French, -(15 * 60 + 18) * 60, "hier, 18:42")]
    [InlineData(UiLanguage.English, -(15 * 60 + 18) * 60, "yesterday, 18:42")]
    [InlineData(UiLanguage.French, -2 * 86400, "lundi")]
    [InlineData(UiLanguage.English, -2 * 86400, "Monday")]
    [InlineData(UiLanguage.French, -28 * 86400, "21 oct.")]
    [InlineData(UiLanguage.English, -28 * 86400, "21 Oct")]
    [InlineData(UiLanguage.French, -3 * 3600, "il y a 3 h")]
    [InlineData(UiLanguage.French, -10, "à l'instant")]
    public void RelativeTimeFormatter_Format_FollowsTheRanges(UiLanguage language, int secondsFromNow, string expected)
    {
        using var _ = UiText.Override(language);
        Assert.Equal(expected, RelativeTimeFormatter.Format(Now.AddSeconds(secondsFromNow), Now));
    }

    [Fact]
    public async Task NextTip_WrapsAndPersists()
    {
        _settings.Current = AppSettings.Default with { HomeTipIndex = HomeTips.Keys.Count - 1 };
        await LoadAsync();

        await _workspace.Home.NextTipCommand.ExecuteAsync(null);

        Assert.Equal(0, _workspace.Home.TipIndex);
        Assert.Equal(0, _settings.Current.HomeTipIndex);
        Assert.Equal($"1 / {HomeTips.Keys.Count}", _workspace.Home.TipPosition);
    }

    [Fact]
    public async Task HideTips_Persists()
    {
        await LoadAsync();

        await _workspace.Home.HideTipsCommand.ExecuteAsync(null);

        Assert.True(_workspace.Home.TipsHidden);
        Assert.True(_settings.Current.HomeTipsHidden);
    }

    [Fact]
    public async Task ShowAtStartup_WhenChanged_Persists()
    {
        await LoadAsync();

        _workspace.Home.ShowAtStartup = false;

        Assert.False(_settings.Current.ShowHomeAtStartup);
    }

    [Theory]
    [InlineData(UiLanguage.French)]
    [InlineData(UiLanguage.English)]
    public void Tips_AreInTheCatalogAndMarkersParse(UiLanguage language)
    {
        using var _ = UiText.Override(language);
        Assert.InRange(HomeTips.Keys.Count, 8, 12);
        foreach (var key in HomeTips.Keys)
            Assert.True(UiText.Has(key), key);
        var segments = HomeTips.Parse("Press {kbd:Ctrl+K} now {kbd:Esc");
        Assert.Equal([new TipSegment("Press ", false), new TipSegment("Ctrl+K", true), new TipSegment(" now {kbd:Esc", false)], segments);
    }

    [Fact]
    public async Task Visibility_FollowsTheSettingAtLaunch()
    {
        _settings.Current = AppSettings.Default with { ShowHomeAtStartup = false };
        await LoadAsync();
        Assert.False(_workspace.IsHomeVisible);
    }

    [Fact]
    public async Task Visibility_OpeningAConnectionHidesAndClosingTheLastShows()
    {
        var p = Profile("a");
        _profiles.Add(p);
        await LoadAsync();
        Assert.True(_workspace.IsHomeVisible);

        await _workspace.ConnectCommand.ExecuteAsync(p);
        Assert.False(_workspace.IsHomeVisible);

        await _workspace.DisconnectCommand.ExecuteAsync(null);
        Assert.True(_workspace.IsHomeVisible);
    }

    [Fact]
    public async Task OpeningACard_ConnectsAndTouchesTheStore()
    {
        var p = Profile("a");
        _profiles.Add(p);
        _recent.Items.Add(new RecentConnection(p.Id, Now.AddDays(-3)));
        await LoadAsync();

        await _workspace.Home.RecentCards.Single().OpenCommand.ExecuteAsync(null);

        Assert.Single(_workspace.Sessions);
        Assert.Equal([p.Id], _recent.Touched);
        Assert.Equal("à l'instant", _workspace.Home.RecentCards.Single().LastOpened);
    }

    [Fact]
    public async Task OpeningAConnectionElsewhere_AlsoTouchesTheStore()
    {
        var p = Profile("a");
        _profiles.Add(p);
        await LoadAsync();

        await _workspace.ConnectCommand.ExecuteAsync(p);

        Assert.Equal([p.Id], _recent.Touched);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new NoTimer();
    }

    private sealed class NoTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeRecent : IRecentConnectionsStore
    {
        public List<RecentConnection> Items { get; } = [];

        public List<Guid> Touched { get; } = [];

        public List<Guid> Forgotten { get; } = [];

        public Task<IReadOnlyList<RecentConnection>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RecentConnection>>([.. Items.OrderByDescending(i => i.LastOpened)]);

        public Task TouchAsync(Guid profileId, DateTimeOffset openedAt, CancellationToken ct)
        {
            Touched.Add(profileId);
            Items.RemoveAll(i => i.ProfileId == profileId);
            Items.Add(new RecentConnection(profileId, openedAt));
            return Task.CompletedTask;
        }

        public Task ForgetAsync(Guid profileId, CancellationToken ct)
        {
            Forgotten.Add(profileId);
            Items.RemoveAll(i => i.ProfileId == profileId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSettings : ISettingsStore
    {
        public AppSettings Current { get; set; } = AppSettings.Default;

        public ValueTask<AppSettings> LoadAsync(CancellationToken ct) => ValueTask.FromResult(Current);

        public ValueTask SaveAsync(AppSettings settings, CancellationToken ct)
        {
            Current = settings;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeStore(List<ConnectionProfile> profiles) : IProfileStore
    {
        public ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<ConnectionProfile>>([.. profiles]);

        public ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask DeleteAsync(Guid id, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>("secret");

        public ValueTask<string?> GetSshSecretAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);
    }

    private sealed class FakeTunnels : ISshTunnelFactory
    {
        public ValueTask<ISshTunnel> OpenAsync(SshTunnelSettings settings, string? secret, string remoteHost, int remotePort, SshHostKeyPrompt? prompt, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class FakeHistory : IQueryHistoryStore
    {
        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<HistoryEntry>>([]);

        public ValueTask ClearAsync(CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class FakeProvider : IDatabaseProvider
    {
        public DatabaseDialect Dialect => DatabaseDialect.PostgreSql;

        public ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IDatabaseConnection>(new FakeConnection());
    }

    private sealed class FakeConnection : IDatabaseConnection
    {
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(new SchemaSnapshot("public", []));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
