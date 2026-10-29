namespace Squill.Sql.Syntax;

/// <summary>Category of a <see cref="LiteralExpr"/>.</summary>
public enum LiteralKind
{
    /// <summary>An integer, decimal or exponent number.</summary>
    Number,
    /// <summary>A quoted string.</summary>
    Text,
    /// <summary><c>TRUE</c> or <c>FALSE</c>.</summary>
    Boolean,
    /// <summary><c>NULL</c>.</summary>
    Null,
}

/// <summary>A possibly qualified column or name reference such as <c>a.b.c</c>.</summary>
/// <param name="Qualifier">Everything before the last dot (<c>a.b</c>), or <see langword="null"/>.</param>
/// <param name="Name">The last part, unquoted; empty after a trailing dot.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record ColumnRef(string? Qualifier, string Name, int Start, int Length) : Expression(Start, Length);

/// <summary>A number, string, boolean or <c>NULL</c> literal.</summary>
/// <param name="Kind">The literal category.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record LiteralExpr(LiteralKind Kind, int Start, int Length) : Expression(Start, Length);

/// <summary>A bind parameter or variable such as <c>@p</c>, <c>:p</c> or <c>$1</c>.</summary>
/// <param name="Name">The parameter text as written, including its prefix.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record ParameterExpr(string Name, int Start, int Length) : Expression(Start, Length);

/// <summary>A <c>*</c> or <c>qualifier.*</c> wildcard.</summary>
/// <param name="Qualifier">The qualifier before <c>.*</c>, or <see langword="null"/>.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record StarExpr(string? Qualifier, int Start, int Length) : Expression(Start, Length);

/// <summary>A function call such as <c>count(DISTINCT a)</c>.</summary>
/// <param name="Name">The function name, qualifier included, unquoted.</param>
/// <param name="Args">The arguments, in order.</param>
/// <param name="Distinct">Whether the argument list starts with <c>DISTINCT</c>.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record FunctionCall(string Name, IReadOnlyList<Expression> Args, bool Distinct, int Start, int Length)
    : Expression(Start, Length);

/// <summary>A binary operation; also the <c>IN</c>, <c>BETWEEN</c>, <c>IS</c> and <c>LIKE</c> forms.</summary>
/// <param name="Op">The upper-case operator, for example <c>+</c>, <c>AND</c> or <c>NOT IN</c>.</param>
/// <param name="Left">The left operand.</param>
/// <param name="Right">The right operand; a <see cref="ListExpr"/> for <c>IN</c>, an <c>AND</c> pair for <c>BETWEEN</c>.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record BinaryExpr(string Op, Expression Left, Expression Right, int Start, int Length)
    : Expression(Start, Length);

/// <summary>A prefix operation such as <c>-a</c>, <c>NOT a</c> or <c>EXISTS (...)</c>.</summary>
/// <param name="Op">The upper-case operator.</param>
/// <param name="Operand">The operand.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record UnaryExpr(string Op, Expression Operand, int Start, int Length) : Expression(Start, Length);

/// <summary>One <c>WHEN ... THEN ...</c> branch of a <see cref="CaseExpr"/>.</summary>
/// <param name="Condition">The tested expression.</param>
/// <param name="Result">The value when the branch matches.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record WhenClause(Expression Condition, Expression Result, int Start, int Length)
    : SyntaxNode(Start, Length);

/// <summary>A searched or simple <c>CASE</c> expression.</summary>
/// <param name="Operand">The compared expression of a simple <c>CASE</c>, or <see langword="null"/>.</param>
/// <param name="Whens">The branches, in order.</param>
/// <param name="Else">The <c>ELSE</c> value, or <see langword="null"/>.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record CaseExpr(
    Expression? Operand, IReadOnlyList<WhenClause> Whens, Expression? Else, int Start, int Length)
    : Expression(Start, Length);

/// <summary>A <c>CAST(x AS type)</c> or <c>x::type</c> conversion.</summary>
/// <param name="Operand">The converted expression.</param>
/// <param name="TypeName">The target type as written.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record CastExpr(Expression Operand, string TypeName, int Start, int Length) : Expression(Start, Length);

/// <summary>A parenthesized expression.</summary>
/// <param name="Inner">The expression between the parentheses.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record ParenExpr(Expression Inner, int Start, int Length) : Expression(Start, Length);

/// <summary>A parenthesized, comma-separated list such as the right side of <c>IN (1, 2)</c>.</summary>
/// <param name="Items">The list items.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record ListExpr(IReadOnlyList<Expression> Items, int Start, int Length) : Expression(Start, Length);

/// <summary>A parenthesized query used as a value.</summary>
/// <param name="Query">The query node.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record SubqueryExpr(SyntaxNode Query, int Start, int Length) : Expression(Start, Length);

/// <summary>Placeholder for input that could not be parsed; empty when something is missing.</summary>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record ErrorNode(int Start, int Length) : Expression(Start, Length);
