using Squill.Core;
using Squill.Core.Security;
using Squill.Data;
using Squill.Presentation.Localization;
using Squill.Presentation.Safety;
using Squill.Presentation.Security;
using Xunit;

namespace Squill.Presentation.Tests.Security;

public sealed class SecurityViewModelTests : IDisposable
{
    private static readonly RoleInfo Analysts = new("analysts", false, false, true, [], ["alice", "bob"]);
    private static readonly RoleInfo Alice = new("alice", true, false, true, ["analysts"], []);
    private static readonly RoleInfo Root = new("root", true, true, true, [], []);

    private readonly FakeReader _reader = new();
    private readonly FakeConnection _connection = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly List<SecurityViewModel> _created = [];

    public void Dispose()
    {
        foreach (var vm in _created)
            vm.Dispose();
    }

    private SecurityViewModel Create(ConnectionEnvironment environment = ConnectionEnvironment.None)
    {
        var profile = new ConnectionProfile(Guid.NewGuid(), "main", DatabaseDialect.PostgreSql, "h", 1, "appdb", "me", null, null, environment);
        var vm = new SecurityViewModel(profile, _reader, _connection, null, _dialogs);
        _created.Add(vm);
        return vm;
    }

    private static ObjectPrivileges Table(string name, params (PrivilegeKind Kind, PrivilegeState State)[] states) =>
        new("public", name, ObjectKind.Table, states.ToDictionary(s => s.Kind, s => s.State));

    private async Task<SecurityViewModel> LoadedAsync(ConnectionEnvironment environment = ConnectionEnvironment.None)
    {
        _reader.Privileges["alice"] =
        [
            Table("orders", (PrivilegeKind.Select, PrivilegeState.Granted), (PrivilegeKind.Insert, PrivilegeState.Inherited("analysts"))),
            Table("items"),
        ];
        var vm = Create(environment);
        await vm.LoadAsync();
        await vm.SelectRoleAsync(Alice);
        return vm;
    }

    [Fact]
    public async Task Load_SplitsGroupsAndUsers_AndFiltersByName()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var vm = Create();
        await vm.LoadAsync();

        Assert.Equal([Analysts], vm.GroupRoles);
        Assert.Equal([Alice, Root], vm.UserRoles);
        Assert.Equal("Group roles 1", vm.GroupsHeading);
        Assert.Equal("Users 2", vm.UsersHeading);
        Assert.Equal(["public"], vm.SelectedSchemas);

        vm.RoleFilter = "ALI";

        Assert.Empty(vm.GroupRoles);
        Assert.Equal([Alice], vm.UserRoles);
        Assert.Equal("Users 2", vm.UsersHeading); // headings count every role, not the filtered ones
    }

    [Fact]
    public void RoleSubtitle_ShowsMembersForGroupsAndBadgesForUsers()
    {
        using var _ = UiText.Override(UiLanguage.English);
        Assert.Equal("2 members", SecurityViewModel.RoleSubtitle(Analysts));
        Assert.Equal("LOGIN SUPERUSER", SecurityViewModel.RoleSubtitle(Root));
    }

    [Fact]
    public async Task SelectingARole_LoadsItsPrivileges_AndFillsTheHeader()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var vm = await LoadedAsync();

        Assert.Equal(["orders", "items"], vm.Rows.Select(r => r.Name));
        Assert.Equal(PrivilegeCellDisplay.Granted, vm.Rows[0].Cells[(int)PrivilegeKind.Select].Display);
        Assert.Equal(["LOGIN", "INHERIT"], vm.RoleBadges);
        Assert.Equal("User · member of analysts", vm.RoleSubtitleText);
        Assert.Equal("alice", _reader.Calls[^1].Role);
        Assert.Equal(["public"], _reader.Calls[^1].Schemas);
    }

    [Fact]
    public async Task StaleResult_OfAnEarlierRole_IsDropped()
    {
        var vm = Create();
        await vm.LoadAsync();
        _reader.Privileges["alice"] = [Table("old")];
        _reader.Privileges["root"] = [Table("fresh")];
        _reader.Gate = new TaskCompletionSource();

        var first = vm.SelectRoleAsync(Alice);
        _reader.Gate = null;
        await vm.SelectRoleAsync(Root);
        _reader.ReleaseGated();
        await first;

        Assert.Equal(["fresh"], vm.Rows.Select(r => r.Name));
    }

    [Fact]
    public async Task Toggles_BuildPendingChanges_AndTheScriptFollows()
    {
        var vm = await LoadedAsync();

        Assert.Equal(ToggleResult.Added, vm.ToggleCell(0, (int)PrivilegeKind.Select)); // granted -> revoke
        Assert.Equal(ToggleResult.Added, vm.ToggleCell(1, (int)PrivilegeKind.Delete)); // none -> grant

        Assert.Equal(2, vm.ChangeCount);
        Assert.Equal(PrivilegeCellDisplay.PendingRevoke, vm.Rows[0].Cells[(int)PrivilegeKind.Select].Display);
        Assert.Equal(PrivilegeCellDisplay.PendingGrant, vm.Rows[1].Cells[(int)PrivilegeKind.Delete].Display);
        Assert.Contains("GRANT DELETE ON public.items TO alice;", vm.ScriptText);
        Assert.Contains("REVOKE SELECT ON public.orders FROM alice;", vm.ScriptText);

        Assert.Equal(ToggleResult.Removed, vm.ToggleCell(1, (int)PrivilegeKind.Delete));
        Assert.Equal(1, vm.ChangeCount);
        Assert.DoesNotContain("items", vm.ScriptText);
    }

    [Fact]
    public async Task InheritedCell_RefusesToggle_ShownOrHidden()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var vm = await LoadedAsync();
        var inherited = vm.Rows[0].Cells[(int)PrivilegeKind.Insert];

        Assert.Equal(PrivilegeCellDisplay.Inherited, inherited.Display);
        Assert.False(inherited.CanToggle);
        Assert.StartsWith("Inherited from analysts", inherited.Tooltip);
        Assert.Equal(ToggleResult.InheritedCannotRevoke, vm.ToggleCell(0, (int)PrivilegeKind.Insert));

        vm.ShowInherited = false;

        Assert.Equal(PrivilegeCellDisplay.Empty, vm.Rows[0].Cells[(int)PrivilegeKind.Insert].Display);
        Assert.Equal(ToggleResult.InheritedCannotRevoke, vm.ToggleCell(0, (int)PrivilegeKind.Insert));
        Assert.Equal(0, vm.ChangeCount);
    }

    [Fact]
    public async Task ToggleRow_GrantsMissing_ThenRevokesAll()
    {
        var vm = await LoadedAsync();

        vm.ToggleRow(1);
        Assert.Equal(7, vm.ChangeCount);

        vm.ToggleRow(1);
        Assert.Equal(0, vm.ChangeCount);
    }

    [Fact]
    public async Task DiscardChanges_DropsEverything()
    {
        var vm = await LoadedAsync();
        vm.ToggleCell(1, 0);

        vm.DiscardChangesCommand.Execute(null);

        Assert.Equal(0, vm.ChangeCount);
        Assert.Equal("", vm.ScriptText);
    }

    [Fact]
    public async Task Apply_RunsStatementsInOneTransaction_ThenReloadsAndClearsChanges()
    {
        var vm = await LoadedAsync();
        vm.ToggleCell(0, (int)PrivilegeKind.Select);
        vm.ToggleCell(1, (int)PrivilegeKind.Delete);
        var reads = _reader.Calls.Count;
        _reader.Privileges["alice"] = [Table("orders"), Table("items", (PrivilegeKind.Delete, PrivilegeState.Granted))];

        await vm.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(
            ["BEGIN", "GRANT DELETE ON public.items TO alice;", "REVOKE SELECT ON public.orders FROM alice;", "COMMIT"],
            _connection.Sql);
        Assert.Equal(0, vm.ChangeCount);
        Assert.Equal(reads + 1, _reader.Calls.Count);
        Assert.Equal(PrivilegeCellDisplay.Granted, vm.Rows[1].Cells[(int)PrivilegeKind.Delete].Display);
    }

    [Fact]
    public async Task Apply_Failure_RollsBack_AndKeepsTheChangesWithTheEngineMessage()
    {
        var vm = await LoadedAsync();
        vm.ToggleCell(1, (int)PrivilegeKind.Delete);
        _connection.FailOn = "GRANT";

        await vm.ApplyCommand.ExecuteAsync(null);

        Assert.Equal("ROLLBACK", _connection.Sql[^1]);
        Assert.Equal(1, vm.ChangeCount);
        Assert.Equal("permission denied", vm.Error);
        Assert.Equal("permission denied", Assert.Single(_dialogs.Errors).Message);
    }

    [Fact]
    public async Task Apply_OnProduction_AsksTheTypedConfirmation_AndRunsNothingWhenRefused()
    {
        var vm = await LoadedAsync(ConnectionEnvironment.Production);
        vm.ToggleCell(0, (int)PrivilegeKind.Select);
        _dialogs.ProductionResult = new ProductionConfirmationResult(false, false);

        await vm.ApplyCommand.ExecuteAsync(null);

        var request = Assert.Single(_dialogs.ProductionRequests);
        Assert.Equal("appdb", request.RequiredText);
        Assert.Contains(request.Statements, s => s.Text.StartsWith("REVOKE SELECT", StringComparison.Ordinal));
        Assert.Empty(_connection.Sql);
        Assert.Equal(1, vm.ChangeCount);

        _dialogs.ProductionResult = new ProductionConfirmationResult(true, false);
        await vm.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(2, _dialogs.ProductionRequests.Count);
        Assert.Equal("COMMIT", _connection.Sql[^1]);
    }

    [Fact]
    public async Task Apply_OutsideProduction_NeverAsksTheProductionConfirmation()
    {
        var vm = await LoadedAsync(ConnectionEnvironment.Development);
        vm.ToggleCell(1, 0);

        await vm.ApplyCommand.ExecuteAsync(null);

        Assert.Empty(_dialogs.ProductionRequests);
        Assert.NotEmpty(_connection.Sql);
    }

    [Fact]
    public async Task SwitchingRole_WithPendingChanges_Asks_AndKeepsTheRoleWhenRefused()
    {
        var vm = await LoadedAsync();
        vm.ToggleCell(1, 0);
        _dialogs.ConfirmResult = false;

        Assert.False(await vm.SelectRoleAsync(Root));
        Assert.Same(Alice, vm.SelectedRole);
        Assert.Equal(1, vm.ChangeCount);
        Assert.Single(_dialogs.Confirms);

        _dialogs.ConfirmResult = true;
        Assert.True(await vm.SelectRoleAsync(Root));
        Assert.Equal(0, vm.ChangeCount);
    }

    [Fact]
    public void DisabledReason_WithoutSession_SaysNotConnected() =>
        Assert.Equal(UiText.Get("security.disabled.notConnected"), SecurityViewModel.DisabledReason(null));

    // ---- fakes ------------------------------------------------------------------------------------------------------

    private sealed class FakeReader : ISecurityReader
    {
        private readonly List<TaskCompletionSource> _gated = [];

        public Dictionary<string, IReadOnlyList<ObjectPrivileges>> Privileges { get; } = [];

        public List<(string Role, IReadOnlyList<string> Schemas)> Calls { get; } = [];

        public TaskCompletionSource? Gate { get; set; }

        public void ReleaseGated()
        {
            foreach (var gate in _gated)
                gate.SetResult();
        }

        public ValueTask<IReadOnlyList<RoleInfo>> ReadRolesAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<RoleInfo>>([Analysts, Alice, Root]);

        public ValueTask<IReadOnlyList<string>> ListSchemasAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<string>>(["analytics", "public"]);

        public async ValueTask<IReadOnlyList<ObjectPrivileges>> ReadPrivilegesAsync(string role, IReadOnlyList<string> schemas, CancellationToken cancellationToken)
        {
            Calls.Add((role, schemas));
            var result = Privileges.TryGetValue(role, out var p) ? p : [];
            if (Gate is { } gate)
            {
                _gated.Add(gate);
                await gate.Task; // the stale read finishes after the newer one, and must be ignored
            }

            return result;
        }
    }

    private sealed class EmptyReader : IResultReader
    {
        public IReadOnlyList<ColumnInfo> Columns => [];

        public long RecordsAffected => -1;

        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken) => ValueTask.FromResult<object?[][]?>(null);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeConnection : IDatabaseConnection
    {
        public List<string> Sql { get; } = [];

        public string? FailOn { get; set; }

        public string ServerVersion => "16.1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            Sql.Add(sql);
            if (FailOn is not null && sql.StartsWith(FailOn, StringComparison.Ordinal))
                throw new InvalidOperationException("permission denied");
            return ValueTask.FromResult<IResultReader>(new EmptyReader());
        }

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
