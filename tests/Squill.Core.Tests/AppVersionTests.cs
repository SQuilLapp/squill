using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Squill.Core.Tests;

public sealed partial class AppVersionTests
{
    [GeneratedRegex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$")]
    private static partial Regex SemVer();

    private static string ReadVersionProps()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "eng", "Version.props")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return XDocument.Load(Path.Combine(dir.FullName, "eng", "Version.props")).Descendants("SquillVersion").Single().Value;
    }

    [Fact]
    public void AppVersion_Current_MatchesVersionProps() => Assert.Equal(AppVersion.Current, ReadVersionProps());

    [Fact]
    public void AppVersion_Current_IsValidSemVer() => Assert.Matches(SemVer(), AppVersion.Current);
}
