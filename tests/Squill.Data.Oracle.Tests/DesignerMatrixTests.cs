using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.Oracle.Tests;

/// <summary>Applies every table designer operation to a populated Oracle table and checks the schema and the data.</summary>
public sealed class DesignerMatrixTests(OracleFixture server) : IClassFixture<OracleFixture>
{
    private const DatabaseDialect Dialect = DatabaseDialect.Oracle;
    private static readonly string Schema = OracleFixture.UserName.ToUpperInvariant();

    /// <summary>The case names, for <c>MemberData</c>.</summary>
    public static TheoryData<string> Names() => DesignerScenarios.Names(Dialect);

    private async Task<IDatabaseConnection> OpenAsync() => await new OracleDatabaseProvider().OpenAsync(
        new ConnectionProfile(Guid.NewGuid(), "designer", Dialect, server.Host, server.Port, OracleFixture.Service, OracleFixture.UserName, null, null),
        OracleFixture.Password,
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

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task TypeChangeOfPopulatedColumn_IsRefusedByTheServer_AndKeepsData()
    {
        await using var connection = await OpenAsync();
        Assert.True(await DesignerScenarios.TypeChangeOfPopulatedColumnIsRefusedAsync(connection, Dialect, Schema));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task FailureMidScript_LeavesEarlierDdlApplied_AndTheDesignerSaysSo()
    {
        await using var connection = await OpenAsync();
        Assert.True(await DesignerScenarios.FirstStatementSurvivesFailureAsync(connection, Dialect, Schema));
        Assert.False(Squill.Sql.Ddl.DdlGenerators.UsesTransactionalDdl(Dialect));
        Assert.NotEqual("designer.mySqlNoRollback", Squill.Presentation.Localization.UiText.Get("designer.mySqlNoRollback"));
    }
}
