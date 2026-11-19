using System.Text.RegularExpressions;
using Xunit;

namespace Squill.App.WinUI.Tests;

/// <summary>Guards the WinUI head against token brushes read through the application resources, which follow the system theme.</summary>
public partial class ThemeLookupScanTests
{
    // Files and keys that may read a Brush/Color from the application resources. Every key checked in SquillTokens.xaml:
    // none of them is a token; Tokens.cs itself implements the lookup.
    private static readonly string[] AllowedFiles = ["Tokens.cs"];

    [GeneratedRegex("""Resources\[[^\]]*(Brush|Color)"[^\]]*\]""", RegexOptions.IgnoreCase)]
    private static partial Regex Lookup();

    [Fact]
    public void Token_brushes_are_read_through_Tokens()
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "Squill.slnx")))
            root = Path.GetDirectoryName(root);
        Assert.NotNull(root);

        var offenders = new List<string>();
        var src = Path.Combine(root, "src", "Squill.App.WinUI");
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") || AllowedFiles.Contains(Path.GetFileName(file)))
                continue;
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (Lookup().IsMatch(lines[i]))
                    offenders.Add($"{Path.GetRelativePath(src, file)}:{i + 1}: {lines[i].Trim()}");
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }
}
