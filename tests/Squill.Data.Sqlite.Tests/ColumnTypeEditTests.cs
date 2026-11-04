using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class ColumnTypeEditTests
{
    [Fact]
    public async Task TypedTextIsWrittenAndReadBackEqual()
    {
        await using var connection = await new SqliteDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "t", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null), null, CancellationToken.None);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "sqlite.sql"), TestContext.Current.CancellationToken);
        await using (var load = await connection.ExecuteAsync(script, [], TestContext.Current.CancellationToken))
            Assert.Null(await load.ReadBatchAsync(1, TestContext.Current.CancellationToken));

        await ColumnTypeEditScenario.RunAsync(connection, DatabaseDialect.Sqlite, null,
            ("c_integer", "INTEGER", "42"), ("c_numeric", "NUMERIC(10,2)", "12.5"), ("c_real", "REAL", "1.5"), ("c_text", "TEXT", "héllo"),
            ("c_blob", "BLOB", "0xcafe"), ("c_datetime", "DATETIME", "2024-05-06 07:08:09"), ("c_boolean", "BOOLEAN", "1"));
    }
}
