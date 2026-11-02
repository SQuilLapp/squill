using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class DmlRoundTripTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"squill-dml-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // The provider pool may still hold the file; it is a temp file.
            }
        }
    }

    private async Task<IDatabaseConnection> OpenAsync() => await new SqliteDatabaseProvider().OpenAsync(
        new ConnectionProfile(Guid.NewGuid(), "file", DatabaseDialect.Sqlite, null, null, null, null, _path, null), null, CancellationToken.None);

    [Fact]
    public async Task GeneratedStatements_ApplyAndReadBack()
    {
        await using var connection = await OpenAsync();
        Assert.Null(await Record.ExceptionAsync(() => DmlScenario.RunAsync(connection, DatabaseDialect.Sqlite, null)));
    }

    [Fact]
    public async Task TypedParameters_ReadBackEqual()
    {
        await using var connection = await OpenAsync();
        Assert.Null(await Record.ExceptionAsync(() => DmlScenario.RunTypedAsync(connection, DatabaseDialect.Sqlite, null)));
    }
}
