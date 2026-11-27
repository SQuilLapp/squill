using Squill.Data;
using Squill.Presentation.Query;
using Xunit;

namespace Squill.Presentation.Tests.Plans;

public sealed class PlanUnavailableTextTests
{
    [Fact]
    public void DetailReasonShowsTheEngineMessage() =>
        Assert.Equal("needs SELECT_CATALOG_ROLE", QueryTabViewModel.UnavailableText(new PlanUnavailableException("detail", "needs SELECT_CATALOG_ROLE")));

    [Fact]
    public void EngineReasonShowsTheGenericText() =>
        Assert.NotEqual("x", QueryTabViewModel.UnavailableText(new PlanUnavailableException("engine", "x")));
}
