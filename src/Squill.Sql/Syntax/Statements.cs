namespace Squill.Sql.Syntax;

/// <summary>A node that stands for one SQL statement.</summary>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public abstract record Statement(int Start, int Length) : SyntaxNode(Start, Length);

/// <summary>A statement that is not parsed (yet); only its span is known.</summary>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record UnknownStatement(int Start, int Length) : Statement(Start, Length);

/// <summary>A <c>SELECT</c> query with its optional <c>WITH</c> prefix.</summary>
/// <param name="Ctes">The common table expressions, in declaration order.</param>
/// <param name="Distinct">Whether <c>DISTINCT</c> was given.</param>
/// <param name="Items">The select list.</param>
/// <param name="From">The <c>FROM</c> source, or <see langword="null"/> when absent.</param>
/// <param name="Where">The <c>WHERE</c> condition.</param>
/// <param name="GroupBy">The <c>GROUP BY</c> expressions.</param>
/// <param name="Having">The <c>HAVING</c> condition.</param>
/// <param name="OrderBy">The <c>ORDER BY</c> items.</param>
/// <param name="Limit">The <c>LIMIT</c> count.</param>
/// <param name="Offset">The <c>OFFSET</c> (also for T-SQL <c>OFFSET .. FETCH</c>).</param>
/// <param name="Top">The T-SQL <c>TOP</c> count.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record SelectStatement(
    IReadOnlyList<CommonTableExpr> Ctes,
    bool Distinct,
    IReadOnlyList<SelectItem> Items,
    TableSource? From,
    Expression? Where,
    IReadOnlyList<Expression> GroupBy,
    Expression? Having,
    IReadOnlyList<OrderItem> OrderBy,
    Expression? Limit,
    Expression? Offset,
    Expression? Top,
    int Start,
    int Length) : Statement(Start, Length);

/// <summary>The operator of a <see cref="SetOperation"/>.</summary>
public enum SetOperator
{
    /// <summary><c>UNION</c>.</summary>
    Union,

    /// <summary><c>INTERSECT</c>.</summary>
    Intersect,

    /// <summary><c>EXCEPT</c>.</summary>
    Except,
}

/// <summary>Two queries combined by <c>UNION</c>, <c>INTERSECT</c> or <c>EXCEPT</c> (left associative).</summary>
/// <param name="Op">The set operator.</param>
/// <param name="All">Whether <c>ALL</c> was given.</param>
/// <param name="Left">The left query.</param>
/// <param name="Right">The right query.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record SetOperation(SetOperator Op, bool All, Statement Left, Statement Right, int Start, int Length)
    : Statement(Start, Length);

/// <summary>One entry of a <c>WITH</c> clause.</summary>
/// <param name="Name">The CTE name.</param>
/// <param name="Columns">The declared column names, empty when none.</param>
/// <param name="Query">The defining query.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record CommonTableExpr(string Name, IReadOnlyList<string> Columns, Statement Query, int Start, int Length)
    : SyntaxNode(Start, Length);

/// <summary>One entry of a select list.</summary>
/// <param name="Value">The selected expression.</param>
/// <param name="Alias">The output name given with or without <c>AS</c>.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record SelectItem(Expression Value, string? Alias, int Start, int Length) : SyntaxNode(Start, Length);

/// <summary>One entry of an <c>ORDER BY</c> clause.</summary>
/// <param name="Value">The sort expression.</param>
/// <param name="Descending">Whether <c>DESC</c> was given.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record OrderItem(Expression Value, bool Descending, int Start, int Length) : SyntaxNode(Start, Length);

/// <summary>A node that produces rows in a <c>FROM</c> clause.</summary>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public abstract record TableSource(int Start, int Length) : SyntaxNode(Start, Length);

/// <summary>A named table or view; the name is empty when it is missing from the input.</summary>
/// <param name="Schema">The schema part, or <see langword="null"/>; for a three-part name the middle part.</param>
/// <param name="Name">The table name.</param>
/// <param name="Alias">The alias, or <see langword="null"/>.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record TableRef(string? Schema, string Name, string? Alias, int Start, int Length) : TableSource(Start, Length);

/// <summary>A parenthesized query used as a table.</summary>
/// <param name="Query">The query.</param>
/// <param name="Alias">The alias, or <see langword="null"/>.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record SubquerySource(Statement Query, string? Alias, int Start, int Length) : TableSource(Start, Length);

/// <summary>The kind of a join.</summary>
public enum JoinKind
{
    /// <summary><c>[INNER] JOIN</c>.</summary>
    Inner,

    /// <summary><c>LEFT [OUTER] JOIN</c>.</summary>
    Left,

    /// <summary><c>RIGHT [OUTER] JOIN</c>.</summary>
    Right,

    /// <summary><c>FULL [OUTER] JOIN</c>.</summary>
    Full,

    /// <summary><c>CROSS JOIN</c>.</summary>
    Cross,

    /// <summary>A comma between sources.</summary>
    Comma,
}

/// <summary>Two sources joined together (left associative).</summary>
/// <param name="Left">The left source.</param>
/// <param name="Kind">The join kind.</param>
/// <param name="Right">The right source.</param>
/// <param name="On">The <c>ON</c> condition; <see langword="null"/> when absent or <c>USING</c> was used.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public sealed record JoinSource(TableSource Left, JoinKind Kind, TableSource Right, Expression? On, int Start, int Length)
    : TableSource(Start, Length);
