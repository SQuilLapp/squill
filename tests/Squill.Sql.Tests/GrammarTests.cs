using System.Text.Json;
using Squill.Core;
using Squill.Sql.Highlighting;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class GrammarTests
{
    private static string GrammarDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Squill.slnx")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "assets", "grammars");
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "postgresql")]
    [InlineData(DatabaseDialect.MySql, "mysql")]
    [InlineData(DatabaseDialect.SqlServer, "tsql")]
    [InlineData(DatabaseDialect.Sqlite, "sqlite")]
    public void Committed_grammar_matches_generator(DatabaseDialect d, string name)
    {
        var generated = SqlGrammarGenerator.Generate(d);
        Assert.Equal(generated, File.ReadAllText(Path.Combine(GrammarDir(), $"sql-{name}.tmLanguage.json")).ReplaceLineEndings("\n"));

        using var doc = JsonDocument.Parse(generated);
        Assert.Equal($"source.sql.{name}", doc.RootElement.GetProperty("scopeName").GetString());
        Assert.Contains("SELECT", generated);
    }
}
