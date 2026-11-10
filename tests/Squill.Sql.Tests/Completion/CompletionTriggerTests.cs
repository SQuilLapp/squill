using System.Diagnostics;
using Squill.Core;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class CompletionTriggerTests
{
    private static bool Opens(string text, DatabaseDialect dialect = DatabaseDialect.PostgreSql)
        => CompletionTrigger.OpensAfterSpace(new SqlRequest(text, text.Length, dialect, null));

    [Theory]
    [InlineData("select * from ")]
    [InlineData("select * from a join ")]
    [InlineData("select * from a left join ")]
    [InlineData("insert into ")]
    [InlineData("update ")]
    [InlineData("select * from a join b on ")]
    [InlineData("select * from a, ")]
    [InlineData("select ")]
    [InlineData("SELECT * FROM a JOIN b USING ")]
    public void OpensAfterTheseKeywords(string text) => Assert.True(Opens(text));

    [Theory]
    [InlineData("select * from a ")]
    [InlineData("select * from a where ")]
    [InlineData("select * from a where x = '")]
    [InlineData("select 'from ")]
    [InlineData("select 1 -- from ")]
    [InlineData("select * from a join b on x = y ")]
    [InlineData("select a, ")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("select * from")]
    public void StaysClosedElsewhere(string text) => Assert.False(Opens(text));

    [Fact]
    public void CaretMustFollowTheSpace()
        => Assert.False(CompletionTrigger.OpensAfterSpace(new SqlRequest("select * from x", 15, DatabaseDialect.PostgreSql, null)));

    [Fact]
    public void LargeScriptAnswersQuickly()
    {
        var text = string.Concat(Enumerable.Repeat("select a, b from t where x = 1;\n", 3200)) + "select * from ";
        Assert.True(text.Length > 100_000);
        var watch = Stopwatch.StartNew();
        Assert.True(Opens(text));
        Assert.True(watch.ElapsedMilliseconds < 500, $"took {watch.ElapsedMilliseconds} ms");
    }
}
