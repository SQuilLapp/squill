using Squill.Core;
using Squill.Sql.Syntax;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class DmlParserTests
{
    private static T Parse<T>(string sql, DatabaseDialect dialect = DatabaseDialect.PostgreSql, bool clean = true)
        where T : Statement
    {
        var script = SqlParser.Parse(sql, dialect);
        if (clean)
        {
            Assert.Empty(script.Diagnostics);
        }

        var statement = Assert.IsType<T>(Assert.Single(script.Statements));
        Assert.InRange(statement.End, statement.Start, sql.Length);
        return statement;
    }

    [Fact]
    public void InsertValuesWithColumnsAndRows()
    {
        var insert = Parse<InsertStatement>("insert into public.users (id, \"name\") values (1, 'a'), (2, 'b')");
        Assert.Equal("public", insert.Target.Schema);
        Assert.Equal("users", insert.Target.Name);
        Assert.Equal(["id", "name"], insert.Columns);
        var values = Assert.IsType<ValuesStatement>(insert.Source);
        Assert.Equal(2, values.Rows.Count);
        Assert.Equal(2, values.Rows[0].Count);
    }

    [Fact]
    public void InsertSelectSource()
    {
        var insert = Parse<InsertStatement>("insert into t select a from s");
        Assert.Empty(insert.Columns);
        Assert.IsType<SelectStatement>(insert.Source);
    }

    [Fact]
    public void InsertSkipsDialectSpecificTrailingClauses()
    {
        Assert.NotNull(Parse<InsertStatement>("insert into t (a) values (1) returning id").Source);
        Assert.NotNull(Parse<InsertStatement>("insert into t (a) values (1) on conflict (a) do nothing").Source);
        Assert.NotNull(Parse<InsertStatement>("insert into t (a) values (1) on duplicate key update a = 2", DatabaseDialect.MySql).Source);
        Assert.NotNull(Parse<InsertStatement>("insert into t (a) output inserted.a values (1)", DatabaseDialect.SqlServer).Source);
    }

    [Fact]
    public void InsertDefaultValuesHasNoSource()
    {
        Assert.Null(Parse<InsertStatement>("insert into t default values").Source);
    }

    [Fact]
    public void IncompleteInsert()
    {
        var insert = Parse<InsertStatement>("insert into t (", clean: false);
        Assert.Equal("t", insert.Target.Name);
        Assert.Null(insert.Source);
        Assert.NotEmpty(SqlParser.Parse("insert into t (", DatabaseDialect.PostgreSql).Diagnostics);
        Assert.NotEmpty(SqlParser.Parse("insert into t values (1,", DatabaseDialect.PostgreSql).Diagnostics);
        Assert.Equal(string.Empty, Parse<InsertStatement>("insert into", clean: false).Target.Name);
    }

    [Fact]
    public void UpdateWithAliasSetAndWhere()
    {
        var update = Parse<UpdateStatement>("update users u set name = 'x', u.age = age + 1 where id = 3");
        Assert.Equal("users", update.Target.Name);
        Assert.Equal("u", update.Target.Alias);
        Assert.Equal(["name", "age"], update.Set.Select(a => a.Column));
        Assert.NotNull(update.Where);
        Assert.Null(update.From);
    }

    [Fact]
    public void UpdateFromJoin()
    {
        var update = Parse<UpdateStatement>("update t set a = s.a from s join r on r.id = s.id where t.id = s.id");
        Assert.IsType<JoinSource>(update.From);
        Assert.NotNull(update.Where);
    }

    [Fact]
    public void UpdateSkipsReturningAndOutput()
    {
        Assert.NotNull(Parse<UpdateStatement>("update t set a = 1 where b = 2 returning *").Where);
        var tsql = Parse<UpdateStatement>("update t set a = 1 output inserted.a where b = 2", DatabaseDialect.SqlServer);
        Assert.NotNull(tsql.Where);
    }

    [Fact]
    public void IncompleteUpdate()
    {
        var update = Parse<UpdateStatement>("update t set", clean: false);
        Assert.Empty(update.Set);
        Assert.NotEmpty(SqlParser.Parse("update t set", DatabaseDialect.PostgreSql).Diagnostics);
        var half = Parse<UpdateStatement>("update t set a =", clean: false);
        Assert.Single(half.Set);
    }

    [Fact]
    public void DeleteFromWhere()
    {
        var delete = Parse<DeleteStatement>("delete from t where a = 1");
        Assert.Equal("t", delete.Target.Name);
        Assert.Null(delete.From);
        Assert.NotNull(delete.Where);
    }

    [Fact]
    public void TSqlDeleteWithJoin()
    {
        var delete = Parse<DeleteStatement>("delete t from t join s on s.id = t.id where s.x = 1", DatabaseDialect.SqlServer);
        Assert.Equal("t", delete.Target.Name);
        Assert.IsType<JoinSource>(delete.From);
        Assert.NotNull(delete.Where);
    }

    [Fact]
    public void IncompleteDelete()
    {
        var delete = Parse<DeleteStatement>("delete from", clean: false);
        Assert.Equal(string.Empty, delete.Target.Name);
        Assert.NotEmpty(SqlParser.Parse("delete from", DatabaseDialect.PostgreSql).Diagnostics);
    }

    [Fact]
    public void CreateTableColumnsSkipConstraints()
    {
        var create = Parse<CreateTableStatement>("""
            create table if not exists app.t (
                id integer primary key,
                name varchar(50) not null default 'x',
                price numeric(10, 2),
                ratio double precision,
                constraint uq unique (name, price),
                primary key (id)
            )
            """);
        Assert.Equal("app", create.Name.Schema);
        Assert.Equal("t", create.Name.Name);
        Assert.Equal(
            [("id", "integer"), ("name", "varchar(50)"), ("price", "numeric(10, 2)"), ("ratio", "double precision")],
            create.Columns.Select(c => (c.Name, c.TypeName)));
    }

    [Fact]
    public void CreateTableDialectVariants()
    {
        var tsql = Parse<CreateTableStatement>("create table [dbo].[t] ([id] int identity(1,1) not null, name nvarchar(max))", DatabaseDialect.SqlServer);
        Assert.Equal(["int", "nvarchar(max)"], tsql.Columns.Select(c => c.TypeName));
        var mysql = Parse<CreateTableStatement>("create temporary table `t` (id int auto_increment, key idx (id))", DatabaseDialect.MySql);
        Assert.Equal("t", mysql.Name.Name);
        Assert.Single(mysql.Columns);
    }

    [Fact]
    public void IncompleteCreateTable()
    {
        var create = Parse<CreateTableStatement>("create table t (a int,", clean: false);
        Assert.Single(create.Columns);
        Assert.NotEmpty(SqlParser.Parse("create table t (a int,", DatabaseDialect.PostgreSql).Diagnostics);
    }

    [Theory]
    [InlineData("create index i on t (a)", "create")]
    [InlineData("alter table t add b int", "alter")]
    [InlineData("pragma foreign_keys = on", "pragma")]
    [InlineData("set search_path to x", "set")]
    public void OtherStatementsAreUnknownWithoutDiagnostics(string sql, string keyword)
    {
        var unknown = Parse<UnknownStatement>(sql);
        Assert.Equal(keyword, unknown.FirstKeyword, ignoreCase: true);
        Assert.Equal(sql.Length, unknown.Length);
    }

    [Fact]
    public void ScriptMixesStatementKinds()
    {
        var script = SqlParser.Parse("insert into t values (1); update t set a = 2; delete from t; select 1", DatabaseDialect.PostgreSql);
        Assert.Empty(script.Diagnostics);
        Assert.Collection(
            script.Statements,
            s => Assert.IsType<InsertStatement>(s),
            s => Assert.IsType<UpdateStatement>(s),
            s => Assert.IsType<DeleteStatement>(s),
            s => Assert.IsType<SelectStatement>(s));
    }
}
