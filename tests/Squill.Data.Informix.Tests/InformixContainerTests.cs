using Xunit;

namespace Squill.Data.Informix.Tests;

/// <summary>The layer against a real Informix developer server through IBM's <c>clidriver</c>; runs only with <c>SQUILL_DOCKER_TESTS=1</c> and the driver on disk.</summary>
public sealed class InformixContainerTests(InformixFixture server) : IClassFixture<InformixFixture>
{
    private static void RequireDriver() =>
        Assert.SkipWhen(InformixFixture.FindClidriver() is null,
            "IBM clidriver not found: set SQUILL_CLIDRIVER or restore the Net.IBM.Data.Db2 package (see InformixFixture).");

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Execute_SelectFirstRows_ReadsTextAndColumns()
    {
        RequireDriver();
        using var connection = await OdbcConnection.OpenAsync(server.ConnectionString(), TestContext.Current.CancellationToken);
        using var reader = await connection.ExecuteAsync("select first 3 tabname, 'x' as c from systables order by tabid", TestContext.Current.CancellationToken);

        Assert.Equal(2, reader.FieldCount);
        Assert.Equal("tabname", reader.ColumnName(0));
        var names = new List<object?>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            names.Add(reader.GetValue(0));
            Assert.Equal("x", reader.GetValue(1));
        }

        Assert.Equal(["systables", "syscolumns", "sysindices"], names);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Execute_NullAndLongText_AreReadBack()
    {
        RequireDriver();
        using var connection = await OdbcConnection.OpenAsync(server.ConnectionString(), TestContext.Current.CancellationToken);
        var long1500 = new string('z', 1500);
        using var reader = await connection.ExecuteAsync($"select first 1 cast(null as varchar(5)) as n, cast('{long1500}' as lvarchar(2000)) as l from systables", TestContext.Current.CancellationToken);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Null(reader.GetValue(0));
        Assert.Equal(long1500, reader.GetValue(1));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Open_WrongPassword_ReportsSql30082N()
    {
        RequireDriver();
        var e = await Assert.ThrowsAsync<OdbcException>(async () => await OdbcConnection.OpenAsync(server.ConnectionString("Zq9-not-it"), TestContext.Current.CancellationToken));

        Assert.Contains("SQL30082N", e.Message);
        Assert.DoesNotContain("Zq9-not-it", e.Message, StringComparison.Ordinal);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Execute_SqlError_ReportsSqlStateAndNativeCode()
    {
        RequireDriver();
        using var connection = await OdbcConnection.OpenAsync(server.ConnectionString(), TestContext.Current.CancellationToken);
        var e = await Assert.ThrowsAsync<OdbcException>(async () => await connection.ExecuteAsync("select * from no_such_table", TestContext.Current.CancellationToken));

        Assert.NotEqual("", e.SqlState);
        Assert.NotEqual(0, e.NativeError);
    }
}
