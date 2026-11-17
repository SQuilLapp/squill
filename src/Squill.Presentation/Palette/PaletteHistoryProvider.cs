using System.Text;
using Squill.Core;
using Squill.Platform.Profiles;
using Squill.Platform.Storage;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;

namespace Squill.Presentation.Palette;

/// <summary>Searches the query history for the <c>!</c> mode; a chosen entry is inserted in an editor, never executed.</summary>
/// <param name="store">The query history.</param>
/// <param name="profiles">The saved profiles, used for the connection name and environment of an entry.</param>
/// <param name="host">The workspace, or a fake of it.</param>
/// <param name="time">The clock used for relative times; the system clock when <see langword="null"/>.</param>
public sealed class PaletteHistoryProvider(IQueryHistoryStore store, IProfileStore profiles, IPaletteEditorHost host, TimeProvider? time = null)
{
    private const int MaxEntries = 500;
    private const int MaxTitle = 120;
    private const int MaxRows = 200;
    private const char Lf = (char)10;

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private IReadOnlyList<HistoryEntry>? _entries;
    private IReadOnlyList<ConnectionProfile> _profiles = [];

    /// <summary>Reads the history and the profiles again; the palette calls it each time it opens, so typing never touches the disk.</summary>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>A task that completes when the cache is filled.</returns>
    public async ValueTask ReloadAsync(CancellationToken ct)
    {
        var list = await store.ListAsync(MaxEntries, ct);
        _profiles = await profiles.ListAsync(ct);
        _entries = [.. list.Reverse()];
    }

    /// <summary>Finds the entries whose SQL matches <paramref name="term"/>.</summary>
    /// <param name="term">The text after the <c>!</c> prefix; empty returns the most recent entries.</param>
    /// <param name="ct">Cancels the search and the first read.</param>
    /// <returns>At most 200 rows; most recent first among equal scores.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
    public async ValueTask<IReadOnlyList<PaletteItem>> Search(string term, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(term);
        if (_entries is null)
            await ReloadAsync(ct);
        var now = _time.GetLocalNow();
        List<(PaletteItem Item, int Order)> hits = [];
        var order = 0;
        foreach (var entry in _entries!)
        {
            ct.ThrowIfCancellationRequested();
            var title = Collapse(entry.Sql);
            if (FuzzyMatcher.Match(title, term) is { } m)
                hits.Add((ToItem(entry, title, m, now), order++));
        }
        return [.. hits.OrderByDescending(h => h.Item.Score).ThenBy(h => h.Order).Take(MaxRows).Select(h => h.Item)];
    }

    private PaletteItem ToItem(HistoryEntry entry, string title, FuzzyMatch match, DateTimeOffset now)
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == entry.ProfileId);
        var shown = title.Length > MaxTitle ? title[..MaxTitle] : title;
        var tail = entry.Succeeded ? DisplayFormat.Duration(entry.Elapsed) : UiText.Get("palette.history.failed");
        return new PaletteItem(
            PaletteItemKind.History, shown, profile?.Name ?? "", $"{Ago(entry.At, now)} · {tail}", profile?.Environment, null, "history",
            [.. match.Ranges.Where(r => r.Start < shown.Length).Select(r => (r.Start, Math.Min(r.Length, shown.Length - r.Start)))],
            true, match.Score, (mode, _) => Insert(entry, mode));
    }

    private ValueTask Insert(HistoryEntry entry, PaletteRunMode mode)
    {
        var editor = mode == PaletteRunMode.NewTab ? null : host.ActiveEditor;
        if (editor is not null)
        {
            var existing = editor.Text;
            var start = existing.Length == 0 ? 0 : existing.Length + 2;
            // shortcut: ISqlEditor has no undoable edit, so Text is replaced; upgrade when it offers one.
            editor.Text = existing.Length == 0 ? entry.Sql : existing + new string(Lf, 2) + entry.Sql;
            editor.CaretOffset = start;
            return ValueTask.CompletedTask;
        }
        var session = host.Sessions.FirstOrDefault(s => s.Profile.Id == entry.ProfileId) ?? host.ActiveSession;
        if (session is not null)
            host.OpenScript(session, entry.Sql);
        return ValueTask.CompletedTask;
    }

    private static string Collapse(string sql)
    {
        StringBuilder sb = new(sql.Length);
        var space = false;
        foreach (var c in sql.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }
            if (space)
                sb.Append(' ');
            space = false;
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string Ago(DateTimeOffset at, DateTimeOffset now)
    {
        var age = now - at;
        if (age < TimeSpan.FromMinutes(1))
            return UiText.Get("palette.history.ago.now");
        if (age < TimeSpan.FromHours(1))
            return UiText.FormatPlural("palette.history.ago.minutes", (int)age.TotalMinutes);
        if (age < TimeSpan.FromDays(1))
            return UiText.FormatPlural("palette.history.ago.hours", (int)age.TotalHours);
        if (age < TimeSpan.FromDays(7))
            return UiText.FormatPlural("palette.history.ago.days", (int)age.TotalDays);
        return at.ToLocalTime().ToString("d", UiText.Culture);
    }
}
