using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class DropTablesTests
{
    private static async Task<IDatabaseConnection> OpenAsync() =>
        await new SqliteDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "t", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null), null, CancellationToken.None);

    [Fact]
    public async Task DropOrder_ChainOfForeignKeys_DropsAllTables()
    {
        await using var connection = await OpenAsync();
        Assert.Null(await Record.ExceptionAsync(() => DropTablesScenario.OrderDropsReferencingTablesFirstAsync(connection, DatabaseDialect.Sqlite, null)));
    }

    [Fact]
    public async Task DropParentFirst_ReferencedByChild_IsRefusedAndKeepsTables()
    {
        await using var connection = await OpenAsync();
        Assert.Null(await Record.ExceptionAsync(() => DropTablesScenario.WrongOrderIsRefusedAsync(connection, DatabaseDialect.Sqlite, null)));
    }

    [Fact]
    public async Task Plan_UnselectedChild_ReportsOneBlocker()
    {
        await using var connection = await OpenAsync();
        Assert.Null(await Record.ExceptionAsync(() => DropTablesScenario.UnselectedReferencingTableBlocksAsync(connection, DatabaseDialect.Sqlite, null)));
    }

    [Fact]
    public async Task FailedDropInTransaction_AfterTwoDrops_RollsBack()
    {
        await using var connection = await OpenAsync();
        Assert.Null(await Record.ExceptionAsync(() => DropTablesScenario.FailureInTransactionRollsBackOrIsPartialAsync(connection, DatabaseDialect.Sqlite, null)));
    }
}
