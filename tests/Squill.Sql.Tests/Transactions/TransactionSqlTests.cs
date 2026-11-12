using Squill.Core;
using Squill.Sql.Transactions;
using Xunit;

namespace Squill.Sql.Tests.Transactions;

public sealed class TransactionSqlTests
{
    [Theory]
    [InlineData(DatabaseDialect.Sqlite, "BEGIN", "COMMIT", "ROLLBACK")]
    [InlineData(DatabaseDialect.PostgreSql, "BEGIN", "COMMIT", "ROLLBACK")]
    [InlineData(DatabaseDialect.MySql, "START TRANSACTION", "COMMIT", "ROLLBACK")]
    [InlineData(DatabaseDialect.SqlServer, "BEGIN TRANSACTION", "COMMIT TRANSACTION", "ROLLBACK TRANSACTION")]
    [InlineData(DatabaseDialect.Oracle, null, "COMMIT", "ROLLBACK")]
    public void BeginCommitRollback(DatabaseDialect d, string? begin, string commit, string rollback)
    {
        Assert.Equal(begin, TransactionSql.Begin(d));
        Assert.Equal(commit, TransactionSql.Commit(d));
        Assert.Equal(rollback, TransactionSql.Rollback(d));
    }

    [Theory]
    [InlineData(DatabaseDialect.Sqlite, "SAVEPOINT sp", "ROLLBACK TO SAVEPOINT sp", "RELEASE SAVEPOINT sp")]
    [InlineData(DatabaseDialect.PostgreSql, "SAVEPOINT sp", "ROLLBACK TO SAVEPOINT sp", "RELEASE SAVEPOINT sp")]
    [InlineData(DatabaseDialect.MySql, "SAVEPOINT sp", "ROLLBACK TO SAVEPOINT sp", "RELEASE SAVEPOINT sp")]
    [InlineData(DatabaseDialect.SqlServer, "SAVE TRANSACTION sp", "ROLLBACK TRANSACTION sp", null)]
    public void Savepoints(DatabaseDialect d, string save, string back, string? release)
    {
        Assert.Equal(save, TransactionSql.Savepoint(d, "sp"));
        Assert.Equal(back, TransactionSql.RollbackToSavepoint(d, "sp"));
        Assert.Equal(release, TransactionSql.ReleaseSavepoint(d, "sp"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1a")]
    [InlineData("a b")]
    [InlineData("a;drop table t")]
    [InlineData("\"a\"")]
    [InlineData("a-b")]
    public void InvalidName_Rejected(string name)
    {
        Assert.Throws<ArgumentException>(() => TransactionSql.Savepoint(DatabaseDialect.Sqlite, name));
        Assert.Throws<ArgumentException>(() => TransactionSql.RollbackToSavepoint(DatabaseDialect.SqlServer, name));
        Assert.Throws<ArgumentException>(() => TransactionSql.ReleaseSavepoint(DatabaseDialect.PostgreSql, name));
    }

    [Fact]
    public void NameLimit_41Characters_Accepted_42Rejected()
    {
        Assert.Equal("SAVEPOINT " + new string('a', 41), TransactionSql.Savepoint(DatabaseDialect.PostgreSql, new string('a', 41)));
        Assert.Throws<ArgumentException>(() => TransactionSql.Savepoint(DatabaseDialect.Sqlite, new string('a', 42)));
    }

    [Fact]
    public void SavepointName_IsGeneratedForm() => Assert.Equal("squill_sp_3", TransactionSql.SavepointName(3));

    [Fact]
    public void SupportsSavepoints_ExceptOracle()
    {
        Assert.True(TransactionSql.SupportsSavepoints(DatabaseDialect.SqlServer));
        Assert.False(TransactionSql.SupportsSavepoints(DatabaseDialect.Oracle));
    }
}
