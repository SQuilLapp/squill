using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class DropTablesTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    private async Task<IDatabaseConnection> OpenAsync() =>
        await new MySqlDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "my", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null),
            MySqlFixture.Password,
            CancellationToken.None);

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task DropOrder_ChainOfForeignKeys_DropsAllTables()
    {
        await using var connection = await OpenAsync();
        await DropTablesScenario.OrderDropsReferencingTablesFirstAsync(connection, DatabaseDialect.MySql, null);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task DropParentFirst_ReferencedByChild_IsRefusedAndKeepsTables()
    {
        await using var connection = await OpenAsync();
        await DropTablesScenario.WrongOrderIsRefusedAsync(connection, DatabaseDialect.MySql, null);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Plan_UnselectedChild_ReportsOneBlocker()
    {
        await using var connection = await OpenAsync();
        await DropTablesScenario.UnselectedReferencingTableBlocksAsync(connection, DatabaseDialect.MySql, null);
    }

    // Expected before running: both drops stay dropped (implicit commit), the failing third statement changes nothing.
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task FailedDropInTransaction_AfterTwoDrops_LeavesThemDropped()
    {
        await using var connection = await OpenAsync();
        await DropTablesScenario.FailureInTransactionRollsBackOrIsPartialAsync(connection, DatabaseDialect.MySql, null);
    }
}
