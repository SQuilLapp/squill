using Squill.Core;
using Squill.Core.Security;
using Squill.Presentation.Security;
using Squill.Presentation;
using Squill.Presentation.Query;
using Squill.Presentation.Safety;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class SecurityViewModelFlowTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    private const string Setup = """
        drop schema if exists secflow cascade;
        drop role if exists flow_role;
        create role flow_role nologin;
        create schema secflow;
        create table secflow.a(id int);
        create table secflow.b(id int);
        grant usage on schema secflow to flow_role;
        grant select on secflow.a to flow_role;
        """;

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Apply_RevokeOneGrantAnother_ReloadMatchesTheServer()
    {
        var profile = new ConnectionProfile(Guid.NewGuid(), "flow", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null);
        await using var connection = await new PostgresDatabaseProvider().OpenAsync(profile, PostgresFixture.Password, CancellationToken.None);
        await using (var setup = await connection.ExecuteAsync(Setup, [], CancellationToken.None))
            await setup.ReadAllAsync(10, CancellationToken.None);
        using var vm = new SecurityViewModel(profile, (ISecurityReader)connection, connection, null, new Dialogs());
        await vm.LoadAsync();
        await vm.SelectRoleAsync(vm.UserRoles.Concat(vm.GroupRoles).Single(r => r.Name == "flow_role"));
        await vm.SetSchemasAsync(["secflow"]);
        var a = vm.Rows.Select((r, i) => (r, i)).Single(x => x.r.Name == "a").i;
        var b = vm.Rows.Select((r, i) => (r, i)).Single(x => x.r.Name == "b").i;

        vm.ToggleCell(a, (int)PrivilegeKind.Select); // revoke
        vm.ToggleCell(b, (int)PrivilegeKind.Insert); // grant
        await vm.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.ChangeCount);
        Assert.Equal(PrivilegeCellDisplay.Empty, vm.Rows[a].Cells[(int)PrivilegeKind.Select].Display);
        Assert.Equal(PrivilegeCellDisplay.Granted, vm.Rows[b].Cells[(int)PrivilegeKind.Insert].Display);
    }

    private sealed class Dialogs : IDialogService
    {
        public ValueTask<bool> ConfirmSshHostKeyAsync(Squill.Platform.Ssh.SshHostKeyRequest request) => ValueTask.FromResult(false);

        public ValueTask<ProductionConfirmationResult> ConfirmProductionAsync(ProductionConfirmationRequest request) =>
            ValueTask.FromResult(new ProductionConfirmationResult(true, false));

        public ValueTask<TransactionChoice> AskTransactionAsync(TransactionCloseRequest request) => ValueTask.FromResult(TransactionChoice.Rollback);

        public ValueTask<bool> ConfirmAsync(string title, string message) => ValueTask.FromResult(true);

        public ValueTask<string?> PromptAsync(string title, string message, string? initial) => ValueTask.FromResult(initial);

        public ValueTask<string?> PickSaveFileAsync(string suggestedName, string extension) => ValueTask.FromResult<string?>(null);

        public ValueTask ShowErrorAsync(string title, string message) => ValueTask.FromException(new InvalidOperationException($"{title}: {message}"));

        public ValueTask<IReadOnlyList<ParameterValue>?> PromptParametersAsync(IReadOnlyList<string> names, IReadOnlyDictionary<string, ParameterValue> defaults) =>
            ValueTask.FromResult<IReadOnlyList<ParameterValue>?>(null);
    }
}
