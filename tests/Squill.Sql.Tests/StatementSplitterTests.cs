using Squill.Core;
using Squill.Sql.Statements;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class StatementSplitterTests
{
    private static string[] Split(DatabaseDialect dialect, string sql) =>
        [.. SqlStatementSplitter.Split(sql, dialect).Select(s => sql.Substring(s.Start, s.Length))];

    [Fact]
    public void Split_PlainScript_ReturnsTrimmedStatements()
    {
        var sql = "  select 1;\n-- note\nselect 2 ; /* c */\nselect 3";
        Assert.Equal(["select 1;", "select 2 ;", "select 3"], Split(DatabaseDialect.PostgreSql, sql));
    }

    [Fact]
    public void Split_SemicolonInStringsAndComments_DoesNotSplit()
    {
        var sql = "select ';' -- a;b\n, /* ; */ 'x'; select 2;";
        Assert.Equal(["select ';' -- a;b\n, /* ; */ 'x';", "select 2;"], Split(DatabaseDialect.SqlServer, sql));
    }

    [Fact]
    public void Split_EmptyAndCommentOnlyStatements_AreDropped()
    {
        Assert.Equal(["select 1;"], Split(DatabaseDialect.Sqlite, ";; -- x\n select 1; ;\n/* y */"));
        Assert.Empty(Split(DatabaseDialect.Sqlite, "  \n-- only\n"));
    }

    [Fact]
    public void Split_PostgresDollarQuotedBody_DoesNotSplit()
    {
        var sql = "create function f() returns int as $$ begin select 1; return 2; end; $$ language plpgsql; select 3;";
        Assert.Equal(
            ["create function f() returns int as $$ begin select 1; return 2; end; $$ language plpgsql;", "select 3;"],
            Split(DatabaseDialect.PostgreSql, sql));
    }

    [Fact]
    public void Split_TSqlGo_SeparatesBatchesAndIsNotAStatement()
    {
        var sql = "select 1\nGO\nselect 2\n  go 5  \nselect 3;";
        Assert.Equal(["select 1", "select 2", "select 3;"], Split(DatabaseDialect.SqlServer, sql));
    }

    [Fact]
    public void Split_GoNotAloneOnItsLine_IsNotADirective()
    {
        Assert.Equal(["select go from t;"], Split(DatabaseDialect.SqlServer, "select go from t;"));
        Assert.Equal(["go 1;"], Split(DatabaseDialect.PostgreSql, "go 1;"));
    }

    [Fact]
    public void Split_TSqlProcedureBody_KeepsBeginEndTogether()
    {
        var sql = "create procedure p as begin begin transaction; select case when 1=1 then 1 end; commit; end;\nselect 2;";
        Assert.Equal(
            ["create procedure p as begin begin transaction; select case when 1=1 then 1 end; commit; end;", "select 2;"],
            Split(DatabaseDialect.SqlServer, sql));
    }

    [Fact]
    public void Split_MySqlDelimiterProcedure_UsesCustomTerminator()
    {
        var sql = "DELIMITER //\nCREATE PROCEDURE p()\nBEGIN\n  SELECT 1;\n  SELECT 2;\nEND//\nDELIMITER ;\nSELECT 3;";
        Assert.Equal(
            ["CREATE PROCEDURE p()\nBEGIN\n  SELECT 1;\n  SELECT 2;\nEND", "SELECT 3;"],
            Split(DatabaseDialect.MySql, sql));
    }

    [Fact]
    public void Split_MySqlProcedureWithoutDelimiter_TracksNestedEnds()
    {
        var sql = "CREATE PROCEDURE p() BEGIN IF x THEN SELECT 1; END IF; SELECT CASE WHEN 1 THEN 2 END; END; SELECT 9;";
        Assert.Equal(
            ["CREATE PROCEDURE p() BEGIN IF x THEN SELECT 1; END IF; SELECT CASE WHEN 1 THEN 2 END; END;", "SELECT 9;"],
            Split(DatabaseDialect.MySql, sql));
    }

    [Fact]
    public void Split_SqliteTrigger_KeepsBodyTogether()
    {
        var sql = "CREATE TRIGGER t AFTER INSERT ON a BEGIN INSERT INTO b VALUES (1); UPDATE c SET x = 1; END; SELECT 1;";
        Assert.Equal(
            ["CREATE TRIGGER t AFTER INSERT ON a BEGIN INSERT INTO b VALUES (1); UPDATE c SET x = 1; END;", "SELECT 1;"],
            Split(DatabaseDialect.Sqlite, sql));
    }

    [Fact]
    public void Split_BeginWithoutCreateObject_SplitsNormally()
    {
        Assert.Equal(["BEGIN;", "SELECT 1;", "COMMIT;"], Split(DatabaseDialect.Sqlite, "BEGIN; SELECT 1; COMMIT;"));
        Assert.Equal(["CREATE TABLE t(a);", "SELECT 1;"], Split(DatabaseDialect.Sqlite, "CREATE TABLE t(a); SELECT 1;"));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 0)]
    [InlineData(9, 0)]
    [InlineData(10, 0)]
    [InlineData(12, 0)]
    [InlineData(13, 13)]
    [InlineData(100, 13)]
    [InlineData(-4, 0)]
    public void GetStatementAt_CaretPositions_PicksContainingOrPreviousStatement(int offset, int expectedStart)
    {
        // "select 1;" = 0..9, gap 9..13 ("  \n "), "select 2;" starts at 13.
        var sql = "select 1;  \n select 2;";
        var span = SqlStatementSplitter.GetStatementAt(sql, offset, DatabaseDialect.PostgreSql);
        Assert.Equal(expectedStart, span?.Start);
    }

    [Fact]
    public void GetStatementAt_LeadingGapOrEmptyText_FirstStatementOrNull()
    {
        Assert.Equal(6, SqlStatementSplitter.GetStatementAt("-- c\n select 1;", 1, DatabaseDialect.Sqlite)?.Start);
        Assert.Null(SqlStatementSplitter.GetStatementAt("", 0, DatabaseDialect.Sqlite));
        Assert.Null(SqlStatementSplitter.GetStatementAt("-- only", 3, DatabaseDialect.Sqlite));
    }
}
