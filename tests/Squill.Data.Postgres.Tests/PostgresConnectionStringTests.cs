using Npgsql;
using Squill.Core;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class PostgresConnectionStringTests
{
    private static readonly ConnectionProfile Profile =
        new(Guid.NewGuid(), "pg", DatabaseDialect.PostgreSql, "db.local", null, "app", "me", null, null);

    [Fact]
    public void Dialect_Provider_IsPostgreSql() =>
        Assert.Equal(DatabaseDialect.PostgreSql, new PostgresDatabaseProvider().Dialect);

    [Fact]
    public void BuildConnectionString_DefaultPort_Uses5432()
    {
        var builder = new NpgsqlConnectionStringBuilder(PostgresDatabaseProvider.BuildConnectionString(Profile, "pw"));
        Assert.Equal("db.local", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("app", builder.Database);
        Assert.Equal("me", builder.Username);
        Assert.Equal("pw", builder.Password);
    }

    [Fact]
    public void BuildConnectionString_ExplicitPort_IsKept() =>
        Assert.Equal(6543, new NpgsqlConnectionStringBuilder(PostgresDatabaseProvider.BuildConnectionString(Profile with { Port = 6543 }, null)).Port);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BuildConnectionString_MissingHost_ThrowsArgumentException(string? host) =>
        Assert.Throws<ArgumentException>(() => PostgresDatabaseProvider.BuildConnectionString(Profile with { Host = host }, null));
}
