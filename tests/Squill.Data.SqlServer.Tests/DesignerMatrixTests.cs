using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

/// <summary>Applies every table designer operation to a populated SqlServer table and checks the schema and the data.</summary>
public sealed class DesignerMatrixTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    private const DatabaseDialect Dialect = DatabaseDialect.SqlServer;
    private const string? Schema = "dbo";

    /// <summary>The case names, for <c>MemberData</c>.</summary>
    public static TheoryData<string> Names() => DesignerScenarios.Names(Dialect);

    private async Task<IDatabaseConnection> OpenAsync() => await new SqlServerDatabaseProvider().OpenAsync(
        new ConnectionProfile(Guid.NewGuid(), "designer", Dialect, server.Host, server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
        SqlServerFixture.Password,
        CancellationToken.None);

    [Theory(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    [MemberData(nameof(Names))]
    public async Task Operation_ProducesSqlTheServerAcceptsAndKeepsData(string name)
    {
        await using var connection = await OpenAsync();
        await DesignerScenarios.RunAsync(connection, Dialect, Schema, name);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task AutoIncrementChange_IsRefusedByTheValidator()
    {
        await using var connection = await OpenAsync();
        await DesignerScenarios.AutoIncrementChangeIsRefusedAsync(connection, Dialect, Schema);
    }
}
