using Squill.Core;
using Squill.Data;
using Squill.Presentation;
using Squill.Presentation.Compare;
using Squill.Presentation.Query;
using Squill.Presentation.Safety;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Data.Tests.Scenarios;

/// <summary>
/// What a user does in the schema compare tab, through the real view-model, the real provider and two live databases: compare, include every
/// difference, run the script on the target, and find nothing left to compare.
/// </summary>
public static class SchemaCompareFlowScenario
{
    private static async Task RunAsync(IDatabaseConnection connection, string sql)
    {
        await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
        await reader.ReadAllAsync(100, CancellationToken.None);
    }

    /// <summary>Runs the scenario.</summary>
    /// <param name="provider">The provider of the engine under test.</param>
    /// <param name="source">Profile of an empty, writable database: the desired state.</param>
    /// <param name="target">Profile of a second empty, writable database: the one the script changes.</param>
    /// <param name="password">The password of both profiles, or <c>null</c>.</param>
    /// <param name="createTargetSql">A statement run through <paramref name="source"/> that creates the target database; <c>null</c> when it already exists (SQLite creates its file).</param>
    public static async Task RunAsync(IDatabaseProvider provider, ConnectionProfile source, ConnectionProfile target, string? password, string? createTargetSql)
    {
        if (createTargetSql is not null)
        {
            await using var admin = await provider.OpenAsync(source, password, CancellationToken.None);
            await RunAsync(admin, createTargetSql);
        }

        var registry = new ProviderRegistry();
        registry.Register(provider);
        var manager = new SessionManager(registry, new ScenarioProfileStore(password), new ScenarioTunnelFactory());
        var sourceSession = await manager.OpenAsync(source, CancellationToken.None);
        var targetSession = await manager.OpenAsync(target, CancellationToken.None);
        try
        {
            await RunAsync(sourceSession.Connection, "create table cmp_a (id integer primary key, name varchar(50), note varchar(20))");
            await RunAsync(sourceSession.Connection, "create table cmp_b (id integer primary key)");
            await RunAsync(targetSession.Connection, "create table cmp_a (id integer primary key, name varchar(50))");
            await RunAsync(targetSession.Connection, "create table cmp_old (id integer primary key)");

            using var vm = new SchemaCompareViewModel(() => [sourceSession, targetSession], new Dialogs());
            vm.SelectSource(sourceSession);
            Assert.Equal(targetSession, vm.Target?.Session);

            await vm.CompareCommand.ExecuteAsync(null);
            Assert.True(vm.State == CompareState.Ready, vm.FailureText);
            var tables = vm.Groups.Single(g => g.Category == Squill.Core.Schema.Compare.ObjectCategory.Table).Items;
            Assert.Equal(
                ["cmp_a", "cmp_b", "cmp_old"],
                tables.Select(i => i.Name[(i.Name.LastIndexOf('.') + 1)..]).Where(n => n.StartsWith("cmp_", StringComparison.Ordinal)).Order());
            Assert.Equal(
                [(Squill.Core.Schema.Compare.DifferenceKind.Modified, true), (Squill.Core.Schema.Compare.DifferenceKind.Added, true), (Squill.Core.Schema.Compare.DifferenceKind.Removed, false)],
                tables.Select(i => (i.Difference.Kind, i.IsIncluded)));

            vm.SetAllIncludedCommand.Execute(true);
            await vm.ScriptTask;
            Assert.Contains("cmp_old", vm.Script, StringComparison.Ordinal);
            Assert.True(vm.ExecuteScriptCommand.CanExecute(null));

            await vm.ExecuteScriptCommand.ExecuteAsync(null);

            Assert.True(vm.ExecuteError.Length == 0, vm.ExecuteError);
            Assert.Equal(CompareState.Ready, vm.State);
            Assert.True(vm.TotalCount == 0, string.Join(", ", vm.Groups.SelectMany(g => g.Items).Select(i => $"{i.Glyph} {i.Name}")));
            Assert.Equal(Squill.Core.Transactions.TransactionState.None, targetSession.Transaction.State);
        }
        finally
        {
            await manager.CloseAsync(targetSession);
            await manager.CloseAsync(sourceSession);
        }
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
    }
}
