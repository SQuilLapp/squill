using Squill.Core;
using Xunit;

namespace Squill.Data.Informix.Tests;

public sealed class InformixProviderTests
{
    private static ConnectionProfile Profile(string? host = "db", string? user = "informix") =>
        new(Guid.NewGuid(), "p", DatabaseDialect.Informix, host, null, null, user, null, null);

    [Fact]
    public void Provider_ServesTheInformixDialect_OnTheDrdaPort()
    {
        Assert.Equal(DatabaseDialect.Informix, new InformixDatabaseProvider().Dialect);
        Assert.Equal(9089, InformixDatabaseProvider.DefaultPort);
    }

    [Theory]
    [InlineData(null, "informix")]
    [InlineData("db", null)]
    public async Task OpenAsync_MissingHostOrUser_IsRefusedBeforeTheDriverIsLoaded(string? host, string? user) =>
        await Assert.ThrowsAsync<ArgumentException>(async () => await new InformixDatabaseProvider().OpenAsync(Profile(host, user), "pw", TestContext.Current.CancellationToken));
}
