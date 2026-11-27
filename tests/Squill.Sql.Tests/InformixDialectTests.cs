using Squill.Core;
using Squill.Sql.Ddl;
using Squill.Sql.Lexing;
using Squill.Sql.Transactions;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class InformixDialectTests
{
    [Fact]
    public void Tokenizer_NamedParameter_IsAParameterToken()
    {
        var tokens = SqlTokenizers.For(DatabaseDialect.Informix).Tokenize("select :id, ':no' from t");

        Assert.Single(tokens, t => t.Kind == TokenKind.Parameter);
    }

    [Fact]
    public void Tokenizer_BraceComment_IsOneCommentToken()
    {
        var tokens = SqlTokenizers.For(DatabaseDialect.Informix).Tokenize("select { :a } 1");

        Assert.Single(tokens, t => t.Kind == TokenKind.BlockComment);
        Assert.DoesNotContain(tokens, t => t.Kind == TokenKind.Parameter);
    }

    [Fact]
    public void TransactionSql_UsesTheStandardStatements()
    {
        Assert.Equal("BEGIN", TransactionSql.Begin(DatabaseDialect.Informix));
        Assert.Equal("COMMIT", TransactionSql.Commit(DatabaseDialect.Informix));
        Assert.Equal("ROLLBACK", TransactionSql.Rollback(DatabaseDialect.Informix));
    }

    [Fact]
    public void Ddl_DropWorks_DesignerIsNotAvailable()
    {
        var generator = DdlGenerators.For(DatabaseDialect.Informix);

        Assert.False(DdlGenerators.CanDesign(DatabaseDialect.Informix));
        Assert.Equal(["DROP TABLE \"t1\""], generator.DropTable(null, "t1"));
        Assert.Throws<NotSupportedException>(() => generator.CreateTable(null!));
    }
}
