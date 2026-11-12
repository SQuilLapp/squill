using Squill.Core;
using Squill.Core.Transactions;
using Squill.Presentation.Localization;
using Squill.Presentation.Safety;
using Squill.Presentation.Sessions;
using Squill.Sql.Transactions;
using Xunit;

namespace Squill.Presentation.Tests.Safety;

public sealed class ProductionGuardTests
{
    private static ConnectionProfile Profile(ConnectionEnvironment env, DatabaseDialect d = DatabaseDialect.PostgreSql, string? db = "shop", string? file = null) =>
        new(Guid.NewGuid(), "prod-pg", d, "h", 1, db, "u", file, null, env);

    [Theory]
    [InlineData(ConnectionEnvironment.Production, "SELECT 1", false, false)]
    [InlineData(ConnectionEnvironment.Production, "SELECT * FROM t WHERE a = 1", false, false)]
    [InlineData(ConnectionEnvironment.Production, "SET search_path = x", false, false)]
    [InlineData(ConnectionEnvironment.Production, "INSERT INTO t VALUES (1)", true, false)]
    [InlineData(ConnectionEnvironment.Production, "UPDATE t SET a = 1 WHERE b = 2", true, false)]
    [InlineData(ConnectionEnvironment.Production, "UPDATE t SET a = 1", true, true)]
    [InlineData(ConnectionEnvironment.Production, "DELETE FROM t", true, true)]
    [InlineData(ConnectionEnvironment.Production, "DELETE FROM t WHERE a = 1", true, false)]
    [InlineData(ConnectionEnvironment.Production, "TRUNCATE t", true, true)]
    [InlineData(ConnectionEnvironment.Production, "DROP TABLE t", true, true)]
    [InlineData(ConnectionEnvironment.Production, "ALTER TABLE t DROP COLUMN a", true, true)]
    [InlineData(ConnectionEnvironment.Production, "CREATE TABLE t (a int)", true, false)]
    [InlineData(ConnectionEnvironment.Production, "CALL do_it()", true, false)]
    [InlineData(ConnectionEnvironment.Production, "FROBNICATE t", true, false)]
    [InlineData(ConnectionEnvironment.Production, "COMMIT", true, false)]
    [InlineData(ConnectionEnvironment.Staging, "DELETE FROM t", false, false)]
    [InlineData(ConnectionEnvironment.Development, "DROP TABLE t", false, false)]
    [InlineData(ConnectionEnvironment.None, "TRUNCATE t", false, false)]
    public void Truth_table(ConnectionEnvironment env, string sql, bool required, bool typing)
    {
        var r = ProductionGuard.Evaluate(Profile(env), [sql]);
        Assert.Equal(required, r.Required);
        Assert.Equal(typing, r.RequiresTyping);
        Assert.Single(r.Traits);
    }

    [Fact]
    public void One_modifying_statement_among_reads_requires_confirmation()
    {
        Assert.True(ProductionGuard.Evaluate(Profile(ConnectionEnvironment.Production), ["SELECT 1", "INSERT INTO t VALUES (1)"]).Required);
    }

    [Fact]
    public void Required_text_is_the_database_the_file_name_or_the_profile_name()
    {
        Assert.Equal("shop", ProductionGuard.RequiredText(Profile(ConnectionEnvironment.Production)));
        Assert.Equal("data.db", ProductionGuard.RequiredText(Profile(ConnectionEnvironment.Production, DatabaseDialect.Sqlite, null, @"C:\x\data.db")));
        Assert.Equal("prod-pg", ProductionGuard.RequiredText(Profile(ConnectionEnvironment.Production, db: null)));
    }

    [Theory]
    [InlineData(" SHOP ", "shop", true)]
    [InlineData("shop", "shop", true)]
    [InlineData("sho", "shop", false)]
    [InlineData(null, "shop", false)]
    public void Matches_trims_and_ignores_case(string? typed, string expected, bool ok) =>
        Assert.Equal(ok, ProductionGuard.Matches(typed, expected));

    private static ProductionConfirmationRequest Run(string[] sql, TransactionMode mode = TransactionMode.AutoCommit, long? estimate = null, DatabaseDialect d = DatabaseDialect.PostgreSql)
    {
        var p = Profile(ConnectionEnvironment.Production, d);
        return ProductionRequestBuilder.ForRun(p, sql, ProductionGuard.Evaluate(p, sql), mode, estimate);
    }

    [Fact]
    public void Delete_without_where_in_french_with_figure()
    {
        using var _ = UiText.Override(UiLanguage.French);
        var r = Run(["DELETE FROM orders"], estimate: 1248310);
        Assert.Equal("Supprimer toutes les lignes de orders ?", r.Title);
        Assert.Equal(string.Format(UiText.Culture, "Supprimer {0:N0} lignes", 1248310), r.ConfirmLabel);
        Assert.Contains("environ 1", r.Message);
        Assert.Contains("estimation du planificateur", r.Message);
        Assert.Equal("shop", r.RequiredText);
        Assert.Equal(1248310, r.EstimatedRows);
        Assert.True(r.ShowTransactionOption);
        Assert.False(r.TransactionOptionLocked);
        Assert.Equal("shop", r.Database);
        Assert.Equal("prod-pg", r.ConnectionName);
    }

    [Fact]
    public void Figures_are_formatted_in_the_ui_culture()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var r = Run(["DELETE FROM orders"], estimate: 1248310);
        Assert.Equal("Delete every row of orders?", r.Title);
        Assert.Equal("Delete 1,248,310 rows", r.ConfirmLabel);
        Assert.Contains("about 1,248,310 rows (planner estimate)", r.Message);
    }

    [Fact]
    public void Update_without_where_labels_modify_and_a_single_row_is_singular()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var r = Run(["UPDATE orders SET a = 1"], estimate: 1);
        Assert.Equal("Modify every row of orders?", r.Title);
        Assert.Equal("Modify 1 row", r.ConfirmLabel);
    }

    [Fact]
    public void Delete_without_figure_says_run()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var r = Run(["DELETE FROM orders"]);
        Assert.Equal("Run", r.ConfirmLabel);
        Assert.Null(r.EstimatedRows);
        Assert.DoesNotContain("estimate", r.Message);
    }

    [Theory]
    [InlineData("DELETE FROM t WHERE a = 1", "Delete 5 rows")]
    [InlineData("UPDATE t SET a = 1 WHERE a = 2", "Modify 5 rows")]
    [InlineData("INSERT INTO t SELECT * FROM u", "Run")]
    public void Other_single_statements_use_the_generic_title_and_the_verb_when_a_figure_exists(string sql, string label)
    {
        using var _ = UiText.Override(UiLanguage.English);
        var r = Run([sql], estimate: 5);
        Assert.Equal("Run this statement on production?", r.Title);
        Assert.Equal(label, r.ConfirmLabel);
        Assert.Null(r.RequiredText);
    }

    [Fact]
    public void Several_statements_list_five_then_the_rest_and_carry_no_figure()
    {
        using var _ = UiText.Override(UiLanguage.French);
        var sql = Enumerable.Range(1, 6).Select(i => $"INSERT INTO t VALUES ({i})").ToArray();
        var r = Run(sql, estimate: 9);
        Assert.Equal("Exécuter ces 6 instructions en production ?", r.Title);
        Assert.Equal("Exécuter", r.ConfirmLabel);
        Assert.Null(r.EstimatedRows);
        Assert.Equal(6, r.Statements.Count);
        Assert.Equal("+ 1 autres", r.Statements[5].Text);
        Assert.Equal(StatementKind.Empty, r.Statements[5].Kind);
        Assert.Equal(sql[0], r.Statements[0].Text);
    }

    [Fact]
    public void Manual_mode_adds_the_sentence_and_locks_the_option()
    {
        using var _ = UiText.Override(UiLanguage.French);
        var r = Run(["DELETE FROM t WHERE a = 1"], TransactionMode.Manual);
        Assert.Contains("La transaction manuelle est active : vous pourrez encore annuler avant de valider.", r.Message);
        Assert.True(r.ShowTransactionOption);
        Assert.True(r.TransactionOptionLocked);
    }

    [Fact]
    public void An_implicit_commit_statement_hides_the_option()
    {
        var r = Run(["ALTER TABLE t ADD c int"], d: DatabaseDialect.MySql);
        Assert.False(r.ShowTransactionOption);
        var vacuum = Run(["VACUUM"]);
        Assert.False(vacuum.ShowTransactionOption);
    }

    [Fact]
    public void Commit_lists_the_journal_without_option_or_typing()
    {
        using var _ = UiText.Override(UiLanguage.French);
        var journal = new[] { new PendingStatement("DELETE FROM t WHERE a = 1", "Q1", DateTimeOffset.UtcNow, 2) };
        var r = ProductionRequestBuilder.ForCommit(Profile(ConnectionEnvironment.Production), journal, 1);
        Assert.Equal(ConfirmationKind.Commit, r.Kind);
        Assert.Equal("Valider", r.ConfirmLabel);
        Assert.Equal("Valider la transaction en production ?", r.Title);
        Assert.Equal("1 instruction non validée sera validée définitivement en production.", r.Message);
        Assert.Single(r.Statements);
        Assert.Null(r.RequiredText);
        Assert.False(r.ShowTransactionOption);
    }
}
