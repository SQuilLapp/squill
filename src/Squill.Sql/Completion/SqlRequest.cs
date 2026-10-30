using Squill.Core;

namespace Squill.Sql.Completion;

/// <summary>The input shared by completion, hover, diagnostics and signature help.</summary>
/// <param name="Text">The whole script.</param>
/// <param name="Offset">The caret offset in <paramref name="Text"/>.</param>
/// <param name="Dialect">The dialect of the connection.</param>
/// <param name="Schema">The known tables, or <see langword="null"/> when no schema is loaded.</param>
public sealed record SqlRequest(string Text, int Offset, DatabaseDialect Dialect, SchemaSnapshot? Schema);
