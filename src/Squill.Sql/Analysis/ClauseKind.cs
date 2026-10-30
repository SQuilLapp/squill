namespace Squill.Sql.Analysis;

/// <summary>The clause of a statement that holds a caret position.</summary>
public enum ClauseKind
{
    /// <summary>Outside any analyzable statement.</summary>
    None,

    /// <summary>The select list (also after <c>DISTINCT</c> and <c>TOP</c>).</summary>
    Select,

    /// <summary>The <c>FROM</c> list, including a <c>DELETE FROM</c> target.</summary>
    From,

    /// <summary>The table after a <c>JOIN</c> keyword.</summary>
    Join,

    /// <summary>The condition of <c>ON</c> or <c>USING</c>.</summary>
    On,

    /// <summary>The <c>WHERE</c> condition.</summary>
    Where,

    /// <summary>The <c>GROUP BY</c> list.</summary>
    GroupBy,

    /// <summary>The <c>HAVING</c> condition.</summary>
    Having,

    /// <summary>The <c>ORDER BY</c> list.</summary>
    OrderBy,

    /// <summary>The assignments of <c>UPDATE ... SET</c>.</summary>
    Set,

    /// <summary>The rows of <c>VALUES</c>.</summary>
    Values,

    /// <summary>Any other clause (<c>INSERT INTO</c>, <c>LIMIT</c>, <c>WITH</c>, ...).</summary>
    Other,
}
