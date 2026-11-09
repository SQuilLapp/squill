using Squill.Presentation.Updates;
using Xunit;

namespace Squill.Presentation.Tests.Updates;

public class UpdateStartupTests
{
    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("true", false)]
    [InlineData(null, false)]
    public void Only_the_value_one_disables_the_startup_flow(string? value, bool disabled) =>
        Assert.Equal(disabled, UpdateStartup.IsDisabled(name => name == UpdateStartup.DisableVariable ? value : null));
}
