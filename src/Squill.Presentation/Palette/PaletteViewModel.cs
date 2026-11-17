using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Palette;

/// <summary>A captioned group of rows of the palette.</summary>
/// <param name="Caption">The upper-case caption of the group.</param>
/// <param name="Items">The rows of the group, in display order.</param>
public sealed record PaletteSection(string Caption, IReadOnlyList<PaletteItem> Items);

/// <summary>The state of the command palette overlay: typed text, mode, sections, selection and the rules that run a row; both heads bind it and hold no palette logic.</summary>
public sealed partial class PaletteViewModel : ViewModelBase, IDisposable
{
    private const int ObjectsInAll = 5;
    private const int ActionsInAll = 5;
    private const int HistoryInAll = 3;
    private const int HistoryInAllEmpty = 5;
    private const int ModeLimit = 50;

    private readonly Func<string, CancellationToken, ValueTask<IReadOnlyList<PaletteItem>>> _objects;
    private readonly Func<string, CancellationToken, ValueTask<IReadOnlyList<PaletteItem>>> _history;
    private readonly Func<CancellationToken, ValueTask> _reloadHistory;
    private readonly Func<string, IReadOnlyList<PaletteItem>> _line;
    private readonly Func<bool> _hasConnection;
    private readonly IUiDispatcher _dispatcher;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _search;
    private Task _historyReady = Task.CompletedTask;
    private int _version;
    private bool _opening;

    /// <summary>Creates the palette view-model.</summary>
    /// <param name="registry">The actions; searched synchronously on the UI thread.</param>
    /// <param name="objects">Searches the objects, usually <see cref="PaletteObjectProvider.Search"/>; runs off the UI thread.</param>
    /// <param name="history">Searches the query history, usually <see cref="PaletteHistoryProvider.Search"/>.</param>
    /// <param name="reloadHistory">Reads the history again, usually <see cref="PaletteHistoryProvider.ReloadAsync"/>; called by <see cref="Open"/>.</param>
    /// <param name="line">Builds the go-to-line row, usually <see cref="PaletteLineProvider.Search"/>.</param>
    /// <param name="hasConnection">Whether at least one connection is open, for the empty-state hint of the objects mode.</param>
    /// <param name="dispatcher">Publishes results on the UI thread.</param>
    /// <param name="dialogs">Shows the error of a row that threw.</param>
    public PaletteViewModel(
        PaletteActionRegistry registry,
        Func<string, CancellationToken, ValueTask<IReadOnlyList<PaletteItem>>> objects,
        Func<string, CancellationToken, ValueTask<IReadOnlyList<PaletteItem>>> history,
        Func<CancellationToken, ValueTask> reloadHistory,
        Func<string, IReadOnlyList<PaletteItem>> line,
        Func<bool> hasConnection,
        IUiDispatcher dispatcher,
        IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(reloadHistory);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(hasConnection);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(dialogs);
        Registry = registry;
        _objects = objects;
        _history = history;
        _reloadHistory = reloadHistory;
        _line = line;
        _hasConnection = hasConnection;
        _dispatcher = dispatcher;
        _dialogs = dialogs;
    }

    /// <summary>Gets the actions; the heads add their head-only actions at startup.</summary>
    public PaletteActionRegistry Registry { get; }

    /// <summary>Gets whether the overlay is shown.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>Gets or sets the whole typed text, prefix included; each change starts a new search while the palette is open.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = "";

    /// <summary>Gets the mode of the typed text.</summary>
    public PaletteMode Mode => PaletteQuery.Parse(Text).Mode;

    /// <summary>Gets the groups of rows, in display order.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PaletteSection> Sections { get; private set; } = [];

    /// <summary>Gets the index of the selected row over all sections, disabled rows included; -1 when there is no row.</summary>
    [ObservableProperty]
    public partial int SelectedIndex { get; private set; } = -1;

    /// <summary>Gets the selected row, or <see langword="null"/> when there is none.</summary>
    public PaletteItem? Selected => Flat() is { Count: > 0 } all && SelectedIndex >= 0 && SelectedIndex < all.Count ? all[SelectedIndex] : null;

    /// <summary>Gets the footer text with the result count and the time of the newest search.</summary>
    [ObservableProperty]
    public partial string FooterText { get; private set; } = "";

    /// <summary>Gets the empty-state text, or <see langword="null"/> when rows are shown.</summary>
    [ObservableProperty]
    public partial string? Hint { get; private set; }

    /// <summary>Completes when the newest search has published; lets tests and heads wait for the end of a search.</summary>
    internal Task Idle { get; private set; } = Task.CompletedTask;

    partial void OnTextChanged(string value)
    {
        OnPropertyChanged(nameof(Mode));
        if (IsOpen && !_opening)
            StartSearch();
    }

    /// <summary>Switches the mode as a mode pill does: the prefix is replaced and the term kept.</summary>
    /// <param name="mode">The mode to switch to.</param>
    public void SetMode(PaletteMode mode) => Text = PaletteQuery.WithMode(Text, mode);

    /// <summary>Opens the palette with empty text, reads the history again and selects the first row.</summary>
    [RelayCommand]
    public void Open()
    {
        _opening = true;
        try
        {
            IsOpen = true;
            Text = "";
        }
        finally
        {
            _opening = false;
        }
        _historyReady = Guard(_reloadHistory(CancellationToken.None));
        StartSearch();
    }

    /// <summary>Cancels the running search.</summary>
    public void Dispose() => Cancel();

    /// <summary>Closes the palette, cancels the running search and clears the rows.</summary>
    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
        Cancel();
        Publish([], null, "");
    }

    /// <summary>Moves the selection, wrapping at both ends.</summary>
    /// <param name="delta">The number of rows to move, negative to go up.</param>
    public void MoveSelection(int delta)
    {
        var count = Flat().Count;
        if (count == 0)
            return;
        SelectedIndex = (((SelectedIndex + delta) % count) + count) % count;
        OnPropertyChanged(nameof(Selected));
    }

    /// <summary>Runs the selected row: the palette closes first, then the row runs; a disabled row does nothing and the palette stays open.</summary>
    /// <param name="mode">Plain Enter or Ctrl+Enter.</param>
    /// <returns>A task that completes when the row ended.</returns>
    public async ValueTask RunSelectedAsync(PaletteRunMode mode)
    {
        if (Selected is not { IsEnabled: true } item)
            return;
        Close();
        try
        {
            await item.Run(mode, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _dialogs.ShowErrorAsync(UiText.Get("palette.error.title"), ex.Message);
        }
    }

    private static async Task Guard(ValueTask task)
    {
        try
        {
            await task;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The history search reads the store again when its cache is empty, so a failed reload only costs that search.
        }
    }

    private List<PaletteItem> Flat() => [.. Sections.SelectMany(s => s.Items)];

    private void Cancel()
    {
        _search?.Cancel();
        _search?.Dispose();
        _search = null;
        _version++;
    }

    private void StartSearch()
    {
        Cancel();
        var cts = _search = new CancellationTokenSource();
        var version = _version;
        var clock = Stopwatch.GetTimestamp();
        var (mode, term, _) = PaletteQuery.Parse(Text);
        Idle = SearchAsync(mode, term, version, clock, cts.Token);
    }

    private async Task SearchAsync(PaletteMode mode, string term, int version, long clock, CancellationToken ct)
    {
        try
        {
            switch (mode)
            {
                case PaletteMode.Line:
                    Show(version, clock, [Section("palette.section.line", _line(term))], null);
                    return;
                case PaletteMode.Actions:
                    Show(version, clock, [Section("palette.section.actions", Registry.Search(term))], null);
                    return;
                case PaletteMode.Objects when term.Length == 0:
                    Show(version, clock, [], UiText.Get(_hasConnection() ? "palette.hint.objects" : "palette.hint.noConnection"));
                    return;
            }

            var actions = mode == PaletteMode.All ? Registry.Search(term) : [];
            if (mode == PaletteMode.All)
            {
                // Actions are cheap: show them at once so a slow index never blanks the list.
                Show(version, clock, ActionsOnly(actions, term), null);
            }

            var objectsTask = mode is PaletteMode.Objects || (mode == PaletteMode.All && term.Length > 0)
                ? Task.Run(async () => await _objects(term, ct), ct)
                : Task.FromResult<IReadOnlyList<PaletteItem>>([]);
            var historyTask = mode is PaletteMode.History || mode == PaletteMode.All
                ? Task.Run(async () =>
                {
                    await _historyReady;
                    return await _history(term, ct);
                }, ct)
                : Task.FromResult<IReadOnlyList<PaletteItem>>([]);
            var objects = await Safe(objectsTask);
            var history = await Safe(historyTask);
            ct.ThrowIfCancellationRequested();

            var sections = mode switch
            {
                PaletteMode.Objects => [Section("palette.section.objects", Take(objects, ModeLimit))],
                PaletteMode.History => [Section("palette.section.history", Take(history, ModeLimit))],
                _ => AllSections(objects, actions, history, term),
            };
            var total = objects.Count + actions.Count + history.Count;
            Show(version, clock, sections, null, total);
        }
        catch (OperationCanceledException)
        {
            // A newer search or Close replaced this one.
        }
    }

    private static async Task<IReadOnlyList<PaletteItem>> Safe(Task<IReadOnlyList<PaletteItem>> task)
    {
        try
        {
            return await task;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static PaletteSection[] ActionsOnly(IReadOnlyList<PaletteItem> actions, string term) =>
        [Section("palette.section.actions", term.Length == 0 ? actions : Take(actions, ActionsInAll))];

    private static PaletteSection[] AllSections(
        IReadOnlyList<PaletteItem> objects, IReadOnlyList<PaletteItem> actions, IReadOnlyList<PaletteItem> history, string term)
    {
        var empty = term.Length == 0;
        return [.. new[]
        {
            Section("palette.section.objects", Take(objects, ObjectsInAll)),
            Section("palette.section.actions", empty ? actions : Take(actions, ActionsInAll)),
            Section("palette.section.history", Take(history, empty ? HistoryInAllEmpty : HistoryInAll)),
        }.Where(s => s.Items.Count > 0)];
    }

    private static PaletteSection Section(string key, IReadOnlyList<PaletteItem> items) => new(UiText.Get(key), items);

    private static IReadOnlyList<PaletteItem> Take(IReadOnlyList<PaletteItem> items, int max) => items.Count <= max ? items : [.. items.Take(max)];

    private void Show(int version, long clock, IReadOnlyList<PaletteSection> sections, string? hint, int? total = null) =>
        _dispatcher.Post(() =>
        {
            if (version != _version)
                return;
            var count = total ?? sections.Sum(s => s.Items.Count);
            var ms = (int)Stopwatch.GetElapsedTime(clock).TotalMilliseconds;
            Publish(sections, hint, UiText.FormatPlural("palette.footer.results", count, ms));
        });

    private void Publish(IReadOnlyList<PaletteSection> sections, string? hint, string footer)
    {
        var shown = sections.Where(s => s.Items.Count > 0).ToList();
        Sections = shown;
        SelectedIndex = shown.Count > 0 ? 0 : -1;
        OnPropertyChanged(nameof(Selected));
        FooterText = footer;
        Hint = hint ?? (shown.Count == 0 && IsOpen && PaletteQuery.Parse(Text).Term.Length > 0 ? UiText.Get("palette.empty") : null);
    }
}
