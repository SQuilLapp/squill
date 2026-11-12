using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;
using Squill.Sql.Lexing;
using Squill.Sql.Syntax;
using Xunit;

namespace Squill.Sql.Tests;

/// <summary>PostgreSQL jsonb operators are tokenized, parsed and not flagged by the diagnostics.</summary>
public sealed class PostgresJsonOperatorTests
{
    private static readonly SchemaSnapshot Schema = new(null,
    [
        new TableInfo(null, "t", TableKind.Table, [new("id", "int", false, true, null), new("data", "jsonb", true, false, null), new("tags", "text[]", true, false, null)], [], []),
    ]);

    [Theory]
    [InlineData("select data->'a'->>'b' from t")]
    [InlineData("select data -> 'a' -> 0 ->> 'b' from t")]
    [InlineData("select data->>'name' as name from t where data->>'name' = 'x'")]
    [InlineData("select data #> '{a,b}', data #>> '{a,b}' from t")]
    [InlineData("select data #>> '{a,b}' from t where data #>> '{a,b}' ilike 'x%'")]
    [InlineData("select id from t where data @> '{\"a\":1}'::jsonb")]
    [InlineData("select id from t where '{\"a\":1}'::jsonb <@ data")]
    [InlineData("select id from t where data ? 'k'")]
    [InlineData("select id from t where data ?| array['a','b'] and data ?& array['c']")]
    [InlineData("select id from t where data @? '$.a' or data @@ '$.a == 1'")]
    [InlineData("select data - 'k', data #- '{a}', data || '{\"b\":1}'::jsonb from t")]
    [InlineData("select id from t where (data->>'age')::int > 3")]
    [InlineData("select id from t where data->'a' = '1' and data->>'k' is not null")]
    [InlineData("select id from t order by data->>'a' desc")]
    [InlineData("select id from t where data->'items'->0->>'name' = 'x' group by data->>'a'")]
    [InlineData("update t set data = jsonb_set(data, '{a}', '1'::jsonb) where data @> '{}'")]
    [InlineData("select id from t where data -> 'a' ->> 'b' = any(tags)")]
    public void JsonbExpressions_AreNotFlagged(string sql)
    {
        Assert.Empty(SqlParser.Parse(sql, DatabaseDialect.PostgreSql).Diagnostics);
        Assert.Empty(SqlDiagnosticsProvider.GetDiagnostics(new SqlRequest(sql, 0, DatabaseDialect.PostgreSql, Schema)));
    }

    [Theory]
    [InlineData("#>>")]
    [InlineData("#>")]
    [InlineData("#-")]
    [InlineData("@?")]
    [InlineData("@@")]
    [InlineData("?|")]
    [InlineData("?&")]
    [InlineData("?")]
    public void JsonOperator_IsOneOperatorToken(string op)
    {
        var token = Assert.Single(SqlTokenizers.For(DatabaseDialect.PostgreSql).Tokenize(op));
        Assert.Equal((TokenKind.Operator, op.Length), (token.Kind, token.Length));
    }
}
