using Squill.Presentation.Terminal;
using Xunit;

namespace Squill.Presentation.Tests.Terminal;

public sealed class TerminalCommandLineTests
{
    private static TerminalLaunch Launch(string file, params string[] args) =>
        new(file, args, null, new Dictionary<string, string>());

    [Fact]
    public void Build_PlainWords_AreLeftUnquoted()
    {
        Assert.Equal("psql -h db -p 5432", TerminalCommandLine.Build(Launch("psql", "-h", "db", "-p", "5432"), true));
        Assert.Equal("psql -h db -p 5432", TerminalCommandLine.Build(Launch("psql", "-h", "db", "-p", "5432"), false));
    }

    [Fact]
    public void Build_Windows_QuotesSpacesQuotesAndEmptyValues()
    {
        var line = TerminalCommandLine.Build(Launch(@"C:\Program Files\PowerShell\pwsh.exe", "a b", "say \"hi\"", ""), true);
        Assert.Equal("\"C:\\Program Files\\PowerShell\\pwsh.exe\" \"a b\" \"say \\\"hi\\\"\" \"\"", line);
    }

    [Fact]
    public void Build_Posix_UsesSingleQuotesAndEscapesThem()
    {
        var line = TerminalCommandLine.Build(Launch("/bin/sh", "it's", "a b"), false);
        Assert.Equal(@"/bin/sh 'it'\''s' 'a b'", line);
    }
}
