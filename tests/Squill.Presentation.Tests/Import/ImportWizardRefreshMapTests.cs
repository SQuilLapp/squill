using Squill.Presentation.Import;
using Xunit;

namespace Squill.Presentation.Tests.Import;

public sealed class ImportWizardRefreshMapTests
{
    [Fact]
    public void Step_RedrawsThePageAndTheFooter() =>
        Assert.Equal(ImportWizardRefresh.Page | ImportWizardRefresh.Footer, ImportWizardRefreshMap.For(nameof(ImportWizardViewModel.Step)));

    [Fact]
    public void CanGoNext_RedrawsFooterSourceAndRun() =>
        Assert.Equal(ImportWizardRefresh.Footer | ImportWizardRefresh.Source | ImportWizardRefresh.Run, ImportWizardRefreshMap.For(nameof(ImportWizardViewModel.CanGoNext)));

    [Fact]
    public void Inserted_RedrawsRunAndFooter() =>
        Assert.Equal(ImportWizardRefresh.Run | ImportWizardRefresh.Footer, ImportWizardRefreshMap.For(nameof(ImportWizardViewModel.Inserted)));

    [Theory]
    [InlineData(null)]
    [InlineData("Unknown")]
    public void UnknownProperty_RedrawsNothing(string? name) => Assert.Equal(ImportWizardRefresh.None, ImportWizardRefreshMap.For(name));
}
