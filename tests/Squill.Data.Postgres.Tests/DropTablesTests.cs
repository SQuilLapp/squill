using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class DropTablesTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    private async Task<IDatabaseConnection> OpenAsync() =>
        await new PostgresDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "pg", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
            PostgresFixture.Password,
            CancellationToken.None);

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task DropOrder_ChainOfForeignKeys_DropsAllTables()
    {
        await using var connection = await OpenAsync();
        await DropTablesScenario.OrderDropsReferencingTablesFirstAsync(connection, DatabaseDialect.PostgreSql, "public");
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task DropParentFirst_ReferencedByChild_IsRefusedAndKeepsTables()
    {
        await using var connection = await OpenAsync();
        await DropTablesScenario.WrongOrderIsRefusedAsync(connection, DatabaseDialect.PostgreSql, "public");
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Plan_UnselectedChild_ReportsOneBlocker()
    {
        await using var connection = await OpenAsync();
        await DropTablesScenario.UnselectedReferencingTableBlocksAsync(connection, DatabaseDialect.PostgreSql, "public");
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task FailedDropInTransaction_AfterTwoDrops_RollsBack()
    {
        await using var connection = await OpenAsync();
        await DropTablesScenario.FailureInTransactionRollsBackOrIsPartialAsync(connection, DatabaseDialect.PostgreSql, "public");
    }
}
