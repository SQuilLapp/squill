using Squill.Core;
using Squill.Sql.Completion;
using Squill.Sql.Lexing;
using Squill.Sql.Syntax;

namespace Squill.Sql.Analysis;

/// <summary>Tells what the name under the caret is and where it is defined and used; built on the scope and reference code of completion and diagnostics.</summary>
public static class SqlSymbolResolver
{
    /// <summary>Resolves the identifier at <paramref name="offset"/>; never throws.</summary>
    /// <param name="script">A script parsed by <see cref="SqlParser.Parse"/>.</param>
    /// <param name="offset">The caret offset in <see cref="SqlScript.Text"/>; just after an identifier counts as on it.</param>
    /// <param name="schema">The loaded schema, or <see langword="null"/> to resolve inside the script only.</param>
    /// <returns>The symbol, or <see langword="null"/> when the caret is not on a name, the name is unknown or an unqualified column is ambiguous.</returns>
    public static SqlSymbol? SymbolAt(SqlScript script, int offset, SchemaSnapshot? schema)
    {
        var context = new Context(script, schema);
        return context.TokenAt(offset) is { } token ? context.Resolve(token) : null;
    }

    /// <summary>Lists the places that name <paramref name="symbol"/>, its declaration included.</summary>
    /// <param name="script">A script parsed by <see cref="SqlParser.Parse"/>.</param>
    /// <param name="symbol">A symbol returned by <see cref="SymbolAt"/>.</param>
    /// <param name="schema">The schema that was given to <see cref="SymbolAt"/>; it lets unqualified columns resolve the same way.</param>
    /// <returns>The identifier ranges in source order. Aliases and derived names are limited to their statement by their declaration, tables, views and columns span the script.</returns>
    public static IReadOnlyList<TextRange> Usages(SqlScript script, SqlSymbol symbol, SchemaSnapshot? schema = null)
    {
        var context = new Context(script, schema);
        var found = new List<TextRange>();
        foreach (var token in context.Tokens)
        {
            if (IsName(token) && Same(context.Name(token), symbol.Name) && context.Resolve(token) is { } other && Matches(other, symbol))
            {
                found.Add(new TextRange(token.Start, token.Length));
            }
        }

        if (symbol.DefinitionInScript is { } declaration && !found.Contains(declaration))
        {
            found.Add(declaration);
            found.Sort((a, b) => a.Start.CompareTo(b.Start));
        }

        return found;
    }

    private static bool IsName(Token token) => token.Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier;

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>Compares two symbols; a missing schema or table on either side matches any, so a call without a snapshot still finds the qualified spellings.</summary>
    private static bool Matches(SqlSymbol a, SqlSymbol b)
    {
        var sameKind = a.Kind == b.Kind || (a.Kind is SqlSymbolKind.Table or SqlSymbolKind.View && b.Kind is SqlSymbolKind.Table or SqlSymbolKind.View);
        return sameKind
            && Same(a.Name, b.Name)
            && (a.Schema is null || b.Schema is null || Same(a.Schema, b.Schema))
            && (a.Table is null || b.Table is null || Same(a.Table, b.Table))
            && a.DefinitionInScript == b.DefinitionInScript;
    }

    /// <summary>What one source says about a column: the symbol when it has it, or that its columns are unknown.</summary>
    private readonly record struct Probe(SqlSymbol? Symbol, bool Unknown, VisibleSource Source);

    /// <summary>The parsed script with its tokens and references, shared by the lookups of one call.</summary>
    private sealed class Context
    {
        private readonly SqlScript script;
        private readonly SchemaSnapshot? schema;
        private readonly List<TreeRef> refs;

        public Context(SqlScript script, SchemaSnapshot? schema)
        {
            this.script = script;
            this.schema = schema;
            Tokens = SqlTokenizers.For(script.Dialect).Tokenize(script.Text);
            refs = SqlTreeWalker.References(script);
        }

        public IReadOnlyList<Token> Tokens { get; }

        public string Name(Token token)
        {
            var text = token.Text(script.Text);
            return token.Kind == TokenKind.QuotedIdentifier && text.Length >= 2 ? text[1..^1] : text;
        }

        public Token? TokenAt(int offset)
        {
            Token? after = null;
            foreach (var token in Tokens)
            {
                if (!IsName(token))
                {
                    continue;
                }

                if (token.Start <= offset && offset < token.End)
                {
                    return token;
                }

                if (token.End == offset)
                {
                    after = token;
                }
            }

            return after;
        }

        public SqlSymbol? Resolve(Token token)
        {
            var best = default(TreeRef);
            foreach (var r in refs)
            {
                if (r.Node.Start <= token.Start && token.End <= r.Node.End && (best is null || r.Node.Length < best.Node.Length))
                {
                    best = r;
                }
            }

            return best?.Node switch
            {
                TableRef t => ResolveTableRef(t, token),
                ColumnRef c => ResolveColumnRef(c, token),
                _ => ResolveDeclaration(token),
            };
        }

        /// <summary>Lists the identifier tokens of a span, in order.</summary>
        private List<Token> Ids(int start, int end)
        {
            var lo = 0;
            var hi = Tokens.Count;
            while (lo < hi)
            {
                var mid = (lo + hi) / 2;
                if (Tokens[mid].Start < start)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            var ids = new List<Token>();
            for (var i = lo; i < Tokens.Count && Tokens[i].End <= end; i++)
            {
                if (IsName(Tokens[i]))
                {
                    ids.Add(Tokens[i]);
                }
            }

            return ids;
        }

        private static TextRange Range(Token token) => new(token.Start, token.Length);

        private SqlSymbol? ResolveTableRef(TableRef t, Token token)
        {
            var ids = Ids(t.Start, t.End);
            if (t.IsOpaque || ids.Count == 0)
            {
                return null;
            }

            if (t.Alias is not null)
            {
                if (ids.Count < 2)
                {
                    return null;
                }

                return token == ids[^1] ? SourceSymbol(SourceOf(t)) : token == ids[^2] ? TableSymbol(t) : null;
            }

            return token == ids[^1] ? TableSymbol(t) : null;
        }

        private VisibleSource SourceOf(TableRef t)
            => SqlScopeResolver.Resolve(script, t.Start, false).Sources.FirstOrDefault(s => ReferenceEquals(s.Origin, t))
                ?? new VisibleSource(t.Schema, t.Name, t.Alias, null) { Origin = t };

        /// <summary>The CTE a table reference names: a visible one, or the one being defined when it is recursive.</summary>
        private CommonTableExpr? CteOf(VisibleSource source)
        {
            if (source.Cte is not null || source.Origin is not TableRef { Schema: null } t)
            {
                return source.Cte;
            }

            foreach (var statement in SqlScopeResolver.Chain(script, t.Start))
            {
                if (statement is SelectStatement { Ctes.Count: > 0 } s)
                {
                    foreach (var cte in s.Ctes)
                    {
                        if (Same(cte.Name, t.Name) && cte.Start <= t.Start && t.End <= cte.End && IsRecursive(s, cte))
                        {
                            return cte;
                        }
                    }
                }
            }

            return null;
        }

        private bool IsRecursive(SelectStatement s, CommonTableExpr cte)
            => script.Text.AsSpan(0, s.Ctes[0].Start).TrimEnd().EndsWith("recursive", StringComparison.OrdinalIgnoreCase)
                || (script.Dialect is DatabaseDialect.Oracle or DatabaseDialect.SqlServer && cte.Query is SetOperation);

        private SqlSymbol? TableSymbol(TableRef t)
        {
            if (CteOf(SourceOf(t)) is { } cte)
            {
                return CteSymbol(cte);
            }

            var info = schema?.FindTable(t.Schema, t.Name);
            return new SqlSymbol(
                info?.Kind.IsViewLike() == true ? SqlSymbolKind.View : SqlSymbolKind.Table,
                info?.Name ?? t.Name, info?.Schema ?? t.Schema, null, null, info is not null);
        }

        private SqlSymbol? CteSymbol(CommonTableExpr cte)
            => Ids(cte.Start, cte.End) is [var first, ..]
                ? new SqlSymbol(SqlSymbolKind.Cte, cte.Name, null, null, Range(first), false)
                : null;

        /// <summary>The symbol a source is named by: its alias, its CTE, or its table.</summary>
        private SqlSymbol? SourceSymbol(VisibleSource source)
        {
            switch (source.Origin)
            {
                case SubquerySource q:
                    return AliasToken(q) is { } a && source.Alias is not null
                        ? new SqlSymbol(SqlSymbolKind.DerivedTable, source.Alias, null, null, Range(a), false)
                        : null;
                case TableRef t when t.Alias is not null:
                    return AliasToken(t) is { } b
                        ? new SqlSymbol(SqlSymbolKind.Alias, t.Alias, null, null, Range(b), TableSymbol(t)?.DefinitionInSchema == true)
                        : null;
                case TableRef t:
                    return TableSymbol(t);
                default:
                    return null;
            }
        }

        private Token? AliasToken(TableSource origin)
        {
            var ids = origin switch
            {
                SubquerySource q => Ids(q.Query.End, origin.End),
                _ => Ids(origin.Start, origin.End),
            };
            return ids.Count == 0 ? null : ids[^1];
        }

        private SqlSymbol? ResolveColumnRef(ColumnRef c, Token token)
        {
            var ids = Ids(c.Start, c.End);
            if (ids.Count == 0)
            {
                return null;
            }

            var scope = SqlScopeResolver.Resolve(script, token.Start, false);
            List<string> qualifiers = c.Qualifier is null ? [] : [.. c.Qualifier.Split('.')];
            if (token == ids[^1])
            {
                return ColumnSymbol(c, token, scope, qualifiers);
            }

            if (qualifiers.Count > 0 && ids.Count >= 2 && token == ids[^2])
            {
                var hits = Pick(scope, s => SchemaCompletionProvider.Matches(s, qualifiers));
                return hits.Count == 1 ? SourceSymbol(hits[0]) : null;
            }

            return null;
        }

        /// <summary>Takes the matching sources of the innermost statement, or of the enclosing ones when it has none.</summary>
        private static List<VisibleSource> Pick(ScopeInfo scope, Func<VisibleSource, bool> test)
        {
            var local = scope.Sources.Take(scope.LocalCount).Where(test).ToList();
            return local.Count > 0 ? local : [.. scope.Sources.Skip(scope.LocalCount).Where(test)];
        }

        private SqlSymbol? ColumnSymbol(ColumnRef c, Token token, ScopeInfo scope, List<string> qualifiers)
        {
            var select = SqlScopeResolver.Chain(script, token.Start) is [.., SelectStatement s] ? s : null;
            var alias = qualifiers.Count == 0 && select is not null ? SelectAliasSymbol(select, c.Name) : null;
            if (alias is not null && select!.OrderBy.Any(o => o.Start <= token.Start && token.End <= o.End))
            {
                return alias;
            }

            foreach (var group in new[] { scope.Sources.Take(scope.LocalCount).ToList(), [.. scope.Sources.Skip(scope.LocalCount)] })
            {
                var probes = group.Where(s => SchemaCompletionProvider.Matches(s, qualifiers)).Select(s => ProbeColumn(s, c.Name)).ToList();
                if (probes.Count == 0)
                {
                    continue;
                }

                if (qualifiers.Count > 0)
                {
                    return probes.Count == 1 ? probes[0].Symbol ?? Attribute(probes[0], c.Name) : null;
                }

                var hits = probes.Where(p => p.Symbol is not null).ToList();
                var unknown = probes.Where(p => p.Unknown).ToList();
                if (hits.Count == 1 && unknown.Count == 0)
                {
                    return hits[0].Symbol;
                }

                if (hits.Count == 0 && unknown.Count == 0)
                {
                    continue;
                }

                return hits.Count == 0 && unknown.Count == 1 ? Attribute(unknown[0], c.Name) : null;
            }

            return alias;
        }

        /// <summary>A column of a table the schema does not describe, trusted when nothing else could own it.</summary>
        private static SqlSymbol? Attribute(Probe probe, string column)
            => probe.Unknown && probe.Source.Origin is TableRef && probe.Source.Cte is null
                ? new SqlSymbol(SqlSymbolKind.Column, column, probe.Source.Schema, probe.Source.Name, null, false)
                : null;

        private SqlSymbol? SelectAliasSymbol(SelectStatement select, string name)
        {
            foreach (var item in select.Items)
            {
                if (item.Alias is not null && Same(item.Alias, name) && Ids(item.Start, item.End) is [.., var last])
                {
                    return new SqlSymbol(SqlSymbolKind.SelectAlias, item.Alias, null, null, Range(last), false);
                }
            }

            return null;
        }

        private Probe ProbeColumn(VisibleSource source, string name)
        {
            var cte = CteOf(source);
            if (cte is not null || source.Origin is SubquerySource)
            {
                var columns = cte is null ? source.DerivedColumns : cte.Columns.Count > 0 ? cte.Columns : SqlScopeResolver.OutputNames(cte.Query);
                if (columns is null)
                {
                    return new Probe(null, true, source);
                }

                var found = columns.FirstOrDefault(x => Same(x, name));
                return new Probe(
                    found is null ? null : new SqlSymbol(SqlSymbolKind.DerivedColumn, found, null, cte?.Name ?? source.Alias ?? source.Name, DerivedDefinition(source, cte, found), false),
                    false, source);
            }

            var info = schema?.FindTable(source.Schema, source.Name);
            if (info is null)
            {
                return new Probe(null, true, source);
            }

            var column = info.Columns.FirstOrDefault(x => Same(x.Name, name));
            return new Probe(column is null ? null : new SqlSymbol(SqlSymbolKind.Column, column.Name, info.Schema, info.Name, null, true), false, source);
        }

        /// <summary>Finds the column in the inner select list, or in the declared column list of a CTE.</summary>
        private TextRange? DerivedDefinition(VisibleSource source, CommonTableExpr? cte, string name)
        {
            if (cte is { Columns.Count: > 0 })
            {
                var declared = Ids(cte.Start, cte.Query.Start).Skip(1).Where(t => Same(Name(t), name)).Select(t => (Token?)t).FirstOrDefault();
                return declared is { } d ? Range(d) : null;
            }

            var query = cte?.Query ?? (source.Origin as SubquerySource)?.Query;
            while (query is SetOperation o)
            {
                query = o.Left;
            }

            if (query is SelectStatement s)
            {
                foreach (var item in s.Items)
                {
                    if (Same(item.Alias ?? (item.Value as ColumnRef)?.Name, name) && Ids(item.Start, item.End) is [.., var last])
                    {
                        return Range(last);
                    }
                }
            }

            return null;
        }

        /// <summary>Resolves a name that no table or column reference holds: a CTE name or its column list, a select alias, a sub-query alias.</summary>
        private SqlSymbol? ResolveDeclaration(Token token)
        {
            if (SqlScopeResolver.Chain(script, token.Start) is not [.., SelectStatement s])
            {
                return null;
            }

            var word = Name(token);
            foreach (var cte in s.Ctes)
            {
                if (cte.Start <= token.Start && token.End <= cte.End)
                {
                    var ids = Ids(cte.Start, cte.Query.Start);
                    if (ids.Count > 0 && token == ids[0])
                    {
                        return CteSymbol(cte);
                    }

                    return ids.Contains(token) && cte.Columns.Any(x => Same(x, word))
                        ? new SqlSymbol(SqlSymbolKind.DerivedColumn, word, null, cte.Name, Range(token), false)
                        : null;
                }
            }

            foreach (var item in s.Items)
            {
                if (item.Alias is not null && Same(item.Alias, word) && item.Start <= token.Start && token.End <= item.End
                    && Ids(item.Start, item.End) is [.., var last] && last == token)
                {
                    return new SqlSymbol(SqlSymbolKind.SelectAlias, item.Alias, null, null, Range(token), false);
                }
            }

            return SubqueryAlias(s.From, token, word);
        }

        private static SqlSymbol? SubqueryAlias(TableSource? source, Token token, string word) => source switch
        {
            SubquerySource q when q.Alias is not null && Same(q.Alias, word) && q.Query.End <= token.Start && token.End <= q.End
                => new SqlSymbol(SqlSymbolKind.DerivedTable, q.Alias, null, null, Range(token), false),
            JoinSource j => SubqueryAlias(j.Left, token, word) ?? SubqueryAlias(j.Right, token, word),
            _ => null,
        };
    }
}
