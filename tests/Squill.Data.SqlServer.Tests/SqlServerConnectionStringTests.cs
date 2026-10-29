using Microsoft.Data.SqlClient;
using Squill.Core;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class SqlServerConnectionStringTests
{
    private static readonly ConnectionProfile Profile =
        new(Guid.NewGuid(), "ms", DatabaseDialect.SqlServer, "db.local", null, "app", "me", null, null);

    [Fact]
    public void Dialect_Provider_IsSqlServer() =>
        Assert.Equal(DatabaseDialect.SqlServer, new SqlServerDatabaseProvider().Dialect);

    [Fact]
    public void BuildConnectionString_DefaultPort_Uses1433()
    {
        var builder = new SqlConnectionStringBuilder(SqlServerDatabaseProvider.BuildConnectionString(Profile, "pw"));
        Assert.Equal("db.local,1433", builder.DataSource);
        Assert.Equal("app", builder.InitialCatalog);
        Assert.Equal("me", builder.UserID);
        Assert.Equal("pw", builder.Password);
        Assert.False(builder.TrustServerCertificate);
    }

    [Fact]
    public void BuildConnectionString_ExplicitPort_IsKept() =>
        Assert.Equal("db.local,1444", new SqlConnectionStringBuilder(SqlServerDatabaseProvider.BuildConnectionString(Profile with { Port = 1444 }, null)).DataSource);

    [Theory]
    [InlineData("localhost", true)]
    [InlineData("LOCALHOST", true)]
    [InlineData("db.local", false)]
    public void BuildConnectionString_Host_DecidesTrustServerCertificate(string host, bool expected) =>
        Assert.Equal(expected, new SqlConnectionStringBuilder(SqlServerDatabaseProvider.BuildConnectionString(Profile with { Host = host }, null)).TrustServerCertificate);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BuildConnectionString_MissingHost_ThrowsArgumentException(string? host) =>
        Assert.Throws<ArgumentException>(() => SqlServerDatabaseProvider.BuildConnectionString(Profile with { Host = host }, null));
}
