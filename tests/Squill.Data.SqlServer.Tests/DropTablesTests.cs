using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class DropTablesTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    private async Task<IDatabaseConnection> OpenAsync() =>
        await new SqlServerDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "ms", DatabaseDialect.SqlServer, server.Host, server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
            SqlServerFixture.Password,
            CancellationToken.None);

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task DropOrder_ChainOfForeignKeys_DropsAllTables()
    {
        await using var connection = await OpenAsync();
        await DropTablesScenario.OrderDropsReferencingTablesFirstAsync(connection, DatabaseDialect.SqlServer, "dbo");
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task DropParentFirst_ReferencedByChild_IsRefusedAndKeepsTables()
    {
        await using var connection = await OpenAsync();
        await DropTablesScenario.WrongOrderIsRefusedAsync(connection, DatabaseDialect.SqlServer, "dbo");
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Plan_UnselectedChild_ReportsOneBlocker()
    {
        await using var connection = await OpenAsync();
        await DropTablesScenario.UnselectedReferencingTableBlocksAsync(connection, DatabaseDialect.SqlServer, "dbo");
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task FailedDropInTransaction_AfterTwoDrops_RollsBack()
    {
        await using var connection = await OpenAsync();
        await DropTablesScenario.FailureInTransactionRollsBackOrIsPartialAsync(connection, DatabaseDialect.SqlServer, "dbo");
    }
}
