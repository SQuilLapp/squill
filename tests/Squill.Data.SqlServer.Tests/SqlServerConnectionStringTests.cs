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
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("db.local", false)]
    [InlineData("10.0.0.5", false)]
    public void BuildConnectionString_Host_DecidesTrustServerCertificate(string host, bool expected) =>
        Assert.Equal(expected, new SqlConnectionStringBuilder(SqlServerDatabaseProvider.BuildConnectionString(Profile with { Host = host }, null)).TrustServerCertificate);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BuildConnectionString_MissingHost_ThrowsArgumentException(string? host) =>
        Assert.Throws<ArgumentException>(() => SqlServerDatabaseProvider.BuildConnectionString(Profile with { Host = host }, null));

    [Theory]
    [InlineData(TlsMode.Disable, false)]
    [InlineData(TlsMode.Require, true)]
    [InlineData(TlsMode.VerifyFull, true)]
    public void BuildConnectionString_Tls_MapsToEncrypt(TlsMode tls, bool mandatory) =>
        Assert.Equal(mandatory ? SqlConnectionEncryptOption.Mandatory : SqlConnectionEncryptOption.Optional,
            new SqlConnectionStringBuilder(SqlServerDatabaseProvider.BuildConnectionString(Profile with { Tls = tls }, null)).Encrypt);

    [Theory]
    [InlineData(TlsMode.Default, false, "db.local", false)]
    [InlineData(TlsMode.Default, true, "db.local", true)]
    [InlineData(TlsMode.Require, false, "db.local", true)]
    [InlineData(TlsMode.VerifyFull, true, "db.local", false)]
    [InlineData(TlsMode.VerifyFull, false, "127.0.0.1", false)]
    public void BuildConnectionString_TrustServerCertificate_FollowsModeAndFlag(TlsMode tls, bool flag, string host, bool expected) =>
        Assert.Equal(expected, new SqlConnectionStringBuilder(SqlServerDatabaseProvider.BuildConnectionString(Profile with { Tls = tls, TrustServerCertificate = flag, Host = host }, null)).TrustServerCertificate);

    [Fact]
    public void BuildConnectionString_Timeouts_AreMapped()
    {
        var builder = new SqlConnectionStringBuilder(SqlServerDatabaseProvider.BuildConnectionString(Profile with { ConnectTimeoutSeconds = 7, CommandTimeoutSeconds = 9 }, null));
        Assert.Equal(7, builder.ConnectTimeout);
        Assert.Equal(9, builder.CommandTimeout);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BuildConnectionString_EmptyDatabase_ConnectsToServerDefault(string? database) =>
        Assert.Equal("", new SqlConnectionStringBuilder(SqlServerDatabaseProvider.BuildConnectionString(Profile with { Database = database }, null)).InitialCatalog);
}
