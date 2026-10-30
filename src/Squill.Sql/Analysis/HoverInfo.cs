using Squill.Core;

namespace Squill.Sql.Analysis;

/// <summary>The description shown when the pointer rests on an object.</summary>
/// <param name="Range">The identifier the description is about.</param>
/// <param name="Markdown">The description in Markdown.</param>
public sealed record HoverInfo(TextRange Range, string Markdown);
