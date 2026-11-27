using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Oracle.Tests;

public sealed class DropTablesTests(OracleFixture server) : IClassFixture<OracleFixture>
{
    private async Task<IDatabaseConnection> OpenAsync() =>
        await new OracleDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "ora", DatabaseDialect.Oracle, server.Host, server.Port, OracleFixture.Service, OracleFixture.UserName, null, null),
            OracleFixture.Password,
            CancellationToken.None);

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task DropOrder_ChainOfForeignKeys_DropsAllTables()
    {
        await using var connection = await OpenAsync();
        await DropTablesScenario.OrderDropsReferencingTablesFirstAsync(connection, DatabaseDialect.Oracle, null);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task DropParentFirst_ReferencedByChild_IsRefusedAndKeepsTables()
    {
        await using var connection = await OpenAsync();
        await DropTablesScenario.WrongOrderIsRefusedAsync(connection, DatabaseDialect.Oracle, null);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Plan_UnselectedChild_ReportsOneBlocker()
    {
        await using var connection = await OpenAsync();
        await DropTablesScenario.UnselectedReferencingTableBlocksAsync(connection, DatabaseDialect.Oracle, null);
    }

    // Oracle commits each DROP implicitly, so both drops stay dropped and the failing third statement changes nothing.
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task FailedDropInTransaction_AfterTwoDrops_LeavesThemDropped()
    {
        await using var connection = await OpenAsync();
        await DropTablesScenario.FailureInTransactionRollsBackOrIsPartialAsync(connection, DatabaseDialect.Oracle, null);
    }
}
