# P3-06 - Sql: syntax tree and expression parser

Status: done
Depends on: P3-05

## Goal
Error-tolerant expression parsing with source positions.

## Design decisions (final)
- Namespace `Squill.Sql.Syntax`. `abstract record SyntaxNode(int Start, int Length)`. Expressions: `ColumnRef(string? Qualifier, string Name)`, `LiteralExpr(LiteralKind Kind)`, `ParameterExpr(string Name)`, `StarExpr(string? Qualifier)`, `FunctionCall(string Name, IReadOnlyList<Expression> Args, bool Distinct)`, `BinaryExpr(string Op, Expression Left, Expression Right)`, `UnaryExpr(string Op, Expression Operand)`, `CaseExpr(...)`, `CastExpr(Expression, string TypeName)`, `ParenExpr(Expression)`, `SubqueryExpr(SelectStatement)` (type defined in P3-07, so here `SubqueryExpr` holds `SyntaxNode Query`), `ErrorNode`. Abstract base `Expression : SyntaxNode`.
- `ParseDiagnostic(int Start, int Length, string Message)`. `SqlParserCore` (internal) is a cursor over the token list that skips trivia, with `Peek/Next/Match(keyword)` helpers and a `List<ParseDiagnostic>`.
- `ExpressionParser` (internal): Pratt parser with precedence OR < AND < NOT < comparison (`= <> != < > <= >= IS IN BETWEEN LIKE ILIKE`) < `||` < `+ -` < `* / %` < unary minus < `::`/postfix. Never throws: on an unexpected token it records a diagnostic, returns `ErrorNode` covering the token, and stops at `,` `)` `;` or a clause keyword without consuming it. A trailing `.` (`a.`) yields `ColumnRef("a", "")` plus a diagnostic.
- Dialect differences kept to: `::` cast (Postgres), `[x]` identifiers and `@p` parameters come from tokens, nothing else.

## Out of scope
- Statements (P3-07, P3-08).

## Tasks
- [x] Create the syntax and parser files.
- [x] Tests via `InternalsVisibleTo`: precedence, `a.b.c`, function calls with `DISTINCT`, `CASE`, `IN (1,2)`, `BETWEEN`, incomplete inputs (`a +`, `f(1,`, `a.`) produce diagnostics and no exception; every node range lies inside the input.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add SQL syntax tree and error-tolerant expression parser
