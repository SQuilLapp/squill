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
}
