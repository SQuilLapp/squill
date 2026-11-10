using Squill.Core;
using Squill.Core.Plans;
using Xunit;

namespace Squill.Data.Tests;

public sealed class PlanReaderTests
{
    private static readonly QueryPlan Sample = new(
        DatabaseDialect.PostgreSql,
        PlanMode.Estimated,
        "select 1",
        new PlanNode(0, "Result", PlanNodeKind.Result, null, null, 1, 0.01, 0, null, null, null, null, [], [], false, []),
        [], [], null, null, null, false, PlanSourceFormat.Json, "{}");

    private static async Task<IDatabaseConnection> OpenAsync(FakeDatabaseProvider provider) =>
        await provider.OpenAsync(new ConnectionProfile(Guid.NewGuid(), "t", DatabaseDialect.PostgreSql, null, null, null, null, null, null), null, CancellationToken.None);

    [Fact]
    public async Task Fake_ReturnsConfiguredPlanAndRecordsCalls()
    {
        var provider = new FakeDatabaseProvider(DatabaseDialect.PostgreSql, [], []) { Plan = Sample };
        var reader = Assert.IsAssignableFrom<IPlanReader>(await OpenAsync(provider));

        var plan = await reader.ExplainAsync("select 1", [], PlanMode.Actual, CancellationToken.None);

        Assert.Same(Sample, plan);
        Assert.True(reader.SupportsActualPlan);
        Assert.Equal([("select 1", PlanMode.Actual)], provider.PlanCalls);
    }

    [Fact]
    public async Task Fake_ThrowsConfiguredException()
    {
        var provider = new FakeDatabaseProvider(DatabaseDialect.PostgreSql, [], []) { PlanException = new PlanUnavailableException("version", "too old") };
        var reader = (IPlanReader)await OpenAsync(provider);

        var ex = await Assert.ThrowsAsync<PlanUnavailableException>(async () => await reader.ExplainAsync("select 1", [], PlanMode.Estimated, CancellationToken.None));

        Assert.Equal("version", ex.ReasonCode);
        Assert.Equal("too old", ex.Message);
        Assert.Single(provider.PlanCalls);
    }

    [Fact]
    public void ParseException_KeepsSourceTextAndInner()
    {
        var inner = new FormatException("bad");

        var ex = new PlanParseException("cannot parse", "raw", inner);

        Assert.Equal("raw", ex.SourceText);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal("cannot parse", ex.Message);
    }
}
