namespace Squill.Sql.Completion;

/// <summary>The kind of a completion item; numeric values follow the LSP <c>CompletionItemKind</c>.</summary>
public enum CompletionKind
{
    /// <summary>Plain text.</summary>
    Text = 1,

    /// <summary>A method.</summary>
    Method = 2,

    /// <summary>A function.</summary>
    Function = 3,

    /// <summary>A column.</summary>
    Field = 5,

    /// <summary>A table.</summary>
    Class = 7,

    /// <summary>A schema.</summary>
    Module = 9,

    /// <summary>A keyword.</summary>
    Keyword = 14,

    /// <summary>A snippet.</summary>
    Snippet = 15,

    /// <summary>A view.</summary>
    Struct = 22,
}
