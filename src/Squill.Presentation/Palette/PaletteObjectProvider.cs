using System.Globalization;
using System.Text;
using Squill.Core;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;
using Squill.Presentation.Sessions;
using Squill.Sql.Ddl;

namespace Squill.Presentation.Palette;

/// <summary>Searches the tables, views, routines and triggers of every open connection by name, off the UI thread.</summary>
/// <param name="host">The workspace, or a fake of it.</param>
public sealed class PaletteObjectProvider(IPaletteObjectHost host)
{
    /// <summary>The most rows a search returns.</summary>
    public const int MaxResults = 200;

    private enum ObjectKind
    {
        Table,
        View,
        MaterializedView,
        Function,
        Procedure,
        Trigger,
    }

    private readonly record struct Entry(string Name, string Folded, ObjectKind Kind, string? Schema, int Session, object Source);

    private sealed record Index(IReadOnlyList<ConnectionSession> Sessions, SchemaSnapshot[] Schemas, Entry[] Entries);

    private readonly Lock _gate = new();
    private Task<Index>? _index;

    /// <summary>Finds the objects whose name matches <paramref name="term"/>.</summary>
    /// <param name="term">The typed text without the mode prefix; empty returns the first objects.</param>
    /// <param name="ct">Cancels the search; a cancelled search publishes nothing.</param>
    /// <returns>At most <see cref="MaxResults"/> items, best first; empty when nothing is connected.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
    public async ValueTask<IReadOnlyList<PaletteItem>> Search(string term, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(term);
        ct.ThrowIfCancellationRequested();
        var sessions = host.Sessions;
        if (sessions.Count == 0)
            return [];
        var active = host.ActiveSession;
        var index = await GetIndex(sessions).WaitAsync(ct);
        return await Task.Run(() => Match(index, term, active, ct), ct);
    }

    private Task<Index> GetIndex(IReadOnlyList<ConnectionSession> sessions)
    {
        lock (_gate)
        {
            if (_index is { IsCompletedSuccessfully: true } done && IsCurrent(done.Result, sessions))
                return _index;
            var snapshot = sessions.ToArray();
            var schemas = snapshot.Select(s => s.Schema).ToArray();
            return _index = Task.Run(() => Build(snapshot, schemas));
        }
    }

    private static bool IsCurrent(Index index, IReadOnlyList<ConnectionSession> sessions)
    {
        if (index.Sessions.Count != sessions.Count)
            return false;
        for (var i = 0; i < sessions.Count; i++)
        {
            if (!ReferenceEquals(index.Sessions[i], sessions[i]) || !ReferenceEquals(index.Schemas[i], sessions[i].Schema))
                return false;
        }
        return true;
    }

    private static Index Build(ConnectionSession[] sessions, SchemaSnapshot[] schemas)
    {
        List<Entry> entries = [];
        for (var s = 0; s < sessions.Length; s++)
        {
            foreach (var t in schemas[s].Tables)
            {
                var kind = t.Kind switch
                {
                    TableKind.View => ObjectKind.View,
                    TableKind.MaterializedView => ObjectKind.MaterializedView,
                    _ => ObjectKind.Table,
                };
                entries.Add(new Entry(t.Name, Fold(t.Name), kind, t.Schema, s, t));
            }
            foreach (var r in schemas[s].Routines)
                entries.Add(new Entry(r.Name, Fold(r.Name), r.Kind == RoutineKind.Procedure ? ObjectKind.Procedure : ObjectKind.Function, r.Schema, s, r));
            foreach (var g in schemas[s].Triggers)
                entries.Add(new Entry(g.Name, Fold(g.Name), ObjectKind.Trigger, g.Schema, s, g));
        }
        return new Index(sessions, schemas, [.. entries]);
    }

    private static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private IReadOnlyList<PaletteItem> Match(Index index, string term, ConnectionSession? active, CancellationToken ct)
    {
        var needle = Fold(term).Where(c => !char.IsWhiteSpace(c)).ToArray();
        List<(int Entry, FuzzyMatch Match)> hits = [];
        var entries = index.Entries;
        for (var i = 0; i < entries.Length; i++)
        {
            if ((i & 1023) == 0)
                ct.ThrowIfCancellationRequested();
            if (!IsSubsequence(needle, entries[i].Folded))
                continue;
            if (FuzzyMatcher.Match(entries[i].Name, term) is { } m)
                hits.Add((i, m));
        }
        ct.ThrowIfCancellationRequested();

        int Order((int Entry, FuzzyMatch Match) a, (int Entry, FuzzyMatch Match) b)
        {
            var c = b.Match.Score.CompareTo(a.Match.Score);
            if (c != 0)
                return c;
            var aActive = ReferenceEquals(index.Sessions[entries[a.Entry].Session], active);
            var bActive = ReferenceEquals(index.Sessions[entries[b.Entry].Session], active);
            if (aActive != bActive)
                return aActive ? -1 : 1;
            c = entries[a.Entry].Name.Length.CompareTo(entries[b.Entry].Name.Length);
            return c != 0 ? c : string.Compare(entries[a.Entry].Name, entries[b.Entry].Name, StringComparison.OrdinalIgnoreCase);
        }

        hits.Sort(Order);
        if (hits.Count > MaxResults)
            hits.RemoveRange(MaxResults, hits.Count - MaxResults);
        return [.. hits.Select(h => ToItem(index.Sessions[entries[h.Entry].Session], entries[h.Entry], h.Match))];
    }

    private static bool IsSubsequence(char[] needle, string text)
    {
        var n = 0;
        foreach (var c in text)
        {
            if (n < needle.Length && needle[n] == c)
                n++;
        }
        return n == needle.Length;
    }

    private PaletteItem ToItem(ConnectionSession session, Entry e, FuzzyMatch match)
    {
        var profile = session.Profile;
        var place = e.Schema is { Length: > 0 } schema && !string.Equals(schema, profile.Database, StringComparison.OrdinalIgnoreCase)
            ? $"{profile.Database}.{schema}"
            : profile.Database;
        var (key, icon) = e.Kind switch
        {
            ObjectKind.View => ("palette.kind.view", "view"),
            ObjectKind.MaterializedView => ("palette.kind.materializedView", "view"),
            ObjectKind.Function => ("palette.kind.function", "function"),
            ObjectKind.Procedure => ("palette.kind.procedure", "procedure"),
            ObjectKind.Trigger => ("palette.kind.trigger", "trigger"),
            _ => ("palette.kind.table", "table"),
        };
        return new PaletteItem(
            PaletteItemKind.SchemaObject, e.Name, $"{UiText.Get(key)} · {place}", profile.Name, profile.Environment, null, icon,
            match.Ranges, true, match.Score, (mode, _) => Run(session, e, mode));
    }

    private ValueTask Run(ConnectionSession session, Entry e, PaletteRunMode mode)
    {
        var dialect = session.Profile.Dialect;
        switch (e.Source)
        {
            case TableInfo table when mode == PaletteRunMode.NewTab:
                host.OpenScript(session, "SELECT * FROM " + SqlQuoting.QualifiedName(table.Schema, table.Name, dialect));
                return ValueTask.CompletedTask;
            case TableInfo table:
                return host.OpenTableAsync(session, table);
            default:
                var text = e.Source switch
                {
                    RoutineInfo routine => DefinitionScript.Routine(dialect, routine),
                    TriggerInfo trigger => DefinitionScript.Trigger(dialect, trigger),
                    _ => null,
                };
                if (text is not null)
                    host.OpenScript(session, text);
                return ValueTask.CompletedTask;
        }
    }
}
