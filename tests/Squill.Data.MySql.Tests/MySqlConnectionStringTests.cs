using MySqlConnector;
using Squill.Core;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class MySqlConnectionStringTests
{
    private static readonly ConnectionProfile Profile =
        new(Guid.NewGuid(), "my", DatabaseDialect.MySql, "db.local", null, "app", "me", null, null);

    [Fact]
    public void Dialect_Provider_IsMySql() =>
        Assert.Equal(DatabaseDialect.MySql, new MySqlDatabaseProvider().Dialect);

    [Fact]
    public void BuildConnectionString_DefaultPort_Uses3306()
    {
        var builder = new MySqlConnectionStringBuilder(MySqlDatabaseProvider.BuildConnectionString(Profile, "pw"));
        Assert.Equal("db.local", builder.Server);
        Assert.Equal(3306u, builder.Port);
        Assert.Equal("app", builder.Database);
        Assert.Equal("me", builder.UserID);
        Assert.Equal("pw", builder.Password);
    }

    [Fact]
    public void BuildConnectionString_ExplicitPort_IsKept() =>
        Assert.Equal(3307u, new MySqlConnectionStringBuilder(MySqlDatabaseProvider.BuildConnectionString(Profile with { Port = 3307 }, null)).Port);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BuildConnectionString_MissingHost_ThrowsArgumentException(string? host) =>
        Assert.Throws<ArgumentException>(() => MySqlDatabaseProvider.BuildConnectionString(Profile with { Host = host }, null));

    [Theory]
    [InlineData(TlsMode.Disable, MySqlSslMode.None)]
    [InlineData(TlsMode.Require, MySqlSslMode.Required)]
    [InlineData(TlsMode.VerifyFull, MySqlSslMode.VerifyFull)]
    public void BuildConnectionString_Tls_MapsToSslMode(TlsMode tls, MySqlSslMode expected) =>
        Assert.Equal(expected, new MySqlConnectionStringBuilder(MySqlDatabaseProvider.BuildConnectionString(Profile with { Tls = tls }, null)).SslMode);

    [Fact]
    public void BuildConnectionString_TlsDefault_KeepsDriverDefault() =>
        Assert.Equal(new MySqlConnectionStringBuilder().SslMode, new MySqlConnectionStringBuilder(MySqlDatabaseProvider.BuildConnectionString(Profile, null)).SslMode);

    [Fact]
    public void BuildConnectionString_Timeouts_AreMapped()
    {
        var builder = new MySqlConnectionStringBuilder(MySqlDatabaseProvider.BuildConnectionString(Profile with { ConnectTimeoutSeconds = 7, CommandTimeoutSeconds = 9 }, null));
        Assert.Equal(7u, builder.ConnectionTimeout);
        Assert.Equal(9u, builder.DefaultCommandTimeout);
    }
}
