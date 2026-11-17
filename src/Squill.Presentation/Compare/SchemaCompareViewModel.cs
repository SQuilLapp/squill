using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Core.Design;
using Squill.Core.Schema.Compare;
using Squill.Core.Transactions;
using Squill.Presentation.Localization;
using Squill.Presentation.Safety;
using Squill.Presentation.Sessions;
using Squill.Sql.Ddl;
using Squill.Sql.Migration;
using Squill.Sql.Statements;
using Squill.Sql.Transactions;

namespace Squill.Presentation.Compare;

/// <summary>Where a schema comparison stands.</summary>
public enum CompareState
{
    /// <summary>Nothing compared yet, or the result was discarded.</summary>
    Idle,

    /// <summary>Both schemas are being read and compared.</summary>
    Comparing,

    /// <summary>The differences are listed.</summary>
    Ready,

    /// <summary>The comparison failed; <see cref="SchemaCompareViewModel.FailureText"/> says why.</summary>
    Failed,
}

/// <summary>
/// The schema compare tab: picks a source and a target of the same engine, lists the differences, shows both DDLs side by side, builds the
/// migration script of the included differences and runs it on the target. Running a script on a production target always goes through
/// <see cref="ProductionRequestBuilder.ConfirmWriteAsync"/>.
/// </summary>
public sealed partial class SchemaCompareViewModel : ViewModelBase, IDisposable
{
    /// <summary>The signature of <see cref="MigrationScriptBuilder.Build"/>, so that tests can slow a build down.</summary>
    internal delegate MigrationScript ScriptBuilder(
        DatabaseDialect dialect, SchemaSnapshot source, SchemaSnapshot target, SchemaComparison comparison, IReadOnlySet<ObjectKey> included, MigrationOptions options);

    private readonly Func<IEnumerable<ConnectionSession>> _sessions;
    private readonly IDialogService _dialogs;
    private readonly TimeProvider _time;
    private readonly TimeSpan _debounce;
    private readonly ScriptBuilder _build;
    private readonly List<CompareItemViewModel> _items = [];
    private CancellationTokenSource? _compareCts;
    private CancellationTokenSource? _buildCts;
    private CancellationTokenSource? _runCts;
    private ConnectionSession? _sourceSession;
    private ConnectionSession? _targetSession;
    private CompareEndpoint? _comparedSource;
    private CompareEndpoint? _comparedTarget;
    private SchemaSnapshot? _sourceSnapshot;
    private SchemaSnapshot? _targetSnapshot;
    private SchemaComparison? _comparison;
    private MigrationScript? _generated;
    private int _buildVersion;
    private bool _bulk;
    private bool _settingScript;
    private bool _swapping;

    /// <summary>Creates the tab.</summary>
    /// <param name="sessions">Gives every open session (including those opened on other databases) each time the pickers refresh.</param>
    /// <param name="dialogs">Used for confirmations, errors and the save dialog.</param>
    /// <param name="time">Clock of the compared-at text and the header of the script; defaults to <see cref="TimeProvider.System"/>.</param>
    public SchemaCompareViewModel(Func<IEnumerable<ConnectionSession>> sessions, IDialogService dialogs, TimeProvider? time = null)
        : this(sessions, dialogs, time, TimeSpan.FromMilliseconds(200), MigrationScriptBuilder.Build)
    {
    }

    internal SchemaCompareViewModel(Func<IEnumerable<ConnectionSession>> sessions, IDialogService dialogs, TimeProvider? time, TimeSpan debounce, ScriptBuilder build)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(dialogs);
        _sessions = sessions;
        _dialogs = dialogs;
        _time = time ?? TimeProvider.System;
        _debounce = debounce;
        _build = build;
        RefreshEndpoints();
    }

    /// <summary>Raised when the user closes the tab.</summary>
    public event Action<SchemaCompareViewModel>? CloseRequested;

    /// <summary>Raised by <see cref="CopyCommand"/>; the head puts the text on the clipboard.</summary>
    public event Action<string>? CopyRequested;

    /// <summary>Raised by <see cref="OpenInEditorCommand"/> with the target session and the script, for a new query tab.</summary>
    public event Action<ConnectionSession, string>? ScriptRequested;

    /// <summary>Gets the tab title.</summary>
    public string Title { get; } = UiText.Get("compare.title");

    /// <summary>Gets the session the tab belongs to for the status bar and the colour stripe: the target.</summary>
    public ConnectionSession? Session => _targetSession ?? Target?.Session;

    /// <summary>Gets the environment of the target, which the head turns into the tab colour.</summary>
    public ConnectionEnvironment Environment => Session?.Profile.Environment ?? ConnectionEnvironment.None;

    /// <summary>Tells whether the tab reads or would change a database of the given server.</summary>
    /// <param name="root">A session opened from a profile.</param>
    /// <returns><see langword="true"/> when the source, the target or the compared target belongs to that server.</returns>
    internal bool UsesServer(ConnectionSession root) =>
        Source?.Session.Root == root || Target?.Session.Root == root || _sourceSession?.Root == root || _targetSession?.Root == root;

    // ---- pickers ----

    /// <summary>Gets everything that can be compared among the open sessions.</summary>
    public IReadOnlyList<CompareEndpoint> Sources { get; private set; } = [];

    /// <summary>Gets what the target can be: the endpoints of the source's engine.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<CompareEndpoint> Targets { get; private set; } = [];

    /// <summary>Gets or sets the source: the desired state.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompareCommand))]
    public partial CompareEndpoint? Source { get; set; }

    /// <summary>Gets or sets the target: the database a script would change.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompareCommand))]
    public partial CompareEndpoint? Target { get; set; }

    /// <summary>Re-reads the open sessions into the pickers, keeping the current choice when it still exists.</summary>
    public void RefreshEndpoints()
    {
        Sources = CompareEndpoint.List(_sessions());
        OnPropertyChanged(nameof(Sources));
        Source = Sources.FirstOrDefault(e => Same(e, Source)) ?? (Sources.Count > 0 ? Sources[0] : null);
        RefreshTargets();
    }

    /// <summary>Chooses the source (and the first other endpoint of its engine as target) for a tab opened from a given connection.</summary>
    /// <param name="session">The session to compare from.</param>
    public void SelectSource(ConnectionSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (Sources.FirstOrDefault(e => e.Session == session && e.Database is null) is { } found)
        {
            Target = null;
            Source = found;
            RefreshTargets(); // the setter does nothing when the source did not change
        }
    }

    partial void OnSourceChanged(CompareEndpoint? value)
    {
        if (!_swapping)
            RefreshTargets();
        OnPropertyChanged(nameof(Session));
    }

    partial void OnTargetChanged(CompareEndpoint? value)
    {
        OnPropertyChanged(nameof(Session));
        OnPropertyChanged(nameof(Environment));
    }

    private void RefreshTargets()
    {
        Targets = Source is null ? [] : [.. Sources.Where(e => e.Dialect == Source.Dialect && !Same(e, Source))];
        if (Target is null || !Targets.Any(e => Same(e, Target)))
            Target = Targets.Count > 0 ? Targets[0] : null;
    }

    private static bool Same(CompareEndpoint a, CompareEndpoint? b) =>
        b is not null && a.Session == b.Session && string.Equals(a.Database, b.Database, StringComparison.OrdinalIgnoreCase);

    /// <summary>Exchanges source and target and discards the result, which would read backwards.</summary>
    [RelayCommand]
    private void Swap()
    {
        if (Source is not { } s || Target is not { } t || IsExecuting)
            return;
        _swapping = true;
        Source = t;
        Target = s;
        _swapping = false;
        RefreshTargets();
        _compareCts?.Cancel();
        Reset();
    }

    // ---- comparing ----

    /// <summary>Gets where the comparison stands.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompareCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExecuteScriptCommand))]
    public partial CompareState State { get; private set; }

    /// <summary>Gets the localized reason of the last failed comparison, empty otherwise.</summary>
    [ObservableProperty]
    public partial string FailureText { get; private set; } = "";

    /// <summary>Gets when the comparison finished.</summary>
    public DateTimeOffset? ComparedAt { get; private set; }

    /// <summary>Gets how long reading and comparing took.</summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>Gets <c>Comparé il y a 12 s · 1,4 s</c>; call <see cref="RefreshComparedText"/> to move the age on.</summary>
    public string ComparedText => ComparedAt is { } at
        ? UiText.Format("compare.compared", Age(_time.GetUtcNow() - at), UiText.Format("compare.duration", Duration.TotalSeconds.ToString("0.0", UiText.Culture)))
        : "";

    /// <summary>Raises <see cref="ComparedText"/> again so that the age shown keeps up with the clock.</summary>
    public void RefreshComparedText() => OnPropertyChanged(nameof(ComparedText));

    private static string Age(TimeSpan age) => age.TotalSeconds < 5 ? UiText.Get("compare.age.now")
        : age.TotalMinutes < 1 ? UiText.Format("compare.age.seconds", (int)age.TotalSeconds)
        : age.TotalHours < 1 ? UiText.Format("compare.age.minutes", (int)age.TotalMinutes)
        : UiText.Format("compare.age.hours", (int)age.TotalHours);

    private bool CanCompare() => Source is not null && Target is not null && State != CompareState.Comparing && !IsExecuting;

    /// <summary>Reads both schemas in parallel and lists the differences (<c>Comparer à nouveau</c> when a result is already shown).</summary>
    [RelayCommand(CanExecute = nameof(CanCompare))]
    private async Task CompareAsync()
    {
        if (Source is not { } source || Target is not { } target)
            return;
        _compareCts?.Cancel();
        using var cts = new CancellationTokenSource();
        _compareCts = cts;
        var ct = cts.Token;
        var previous = State == CompareState.Ready ? CompareState.Ready : CompareState.Idle;
        State = CompareState.Comparing;
        FailureText = "";
        var started = _time.GetTimestamp();
        try
        {
            var s = await Resolve(source, ct);
            var t = await Resolve(target, ct);
            var reading = new[]
            {
                Task.Run(() => s.Connection.ReadSchemaAsync(ct).AsTask(), ct),
                Task.Run(() => t.Connection.ReadSchemaAsync(ct).AsTask(), ct),
            };
            var snapshots = await Task.WhenAll(reading);
            var comparison = await Task.Run(() => SchemaComparer.Compare(snapshots[0], snapshots[1]), ct);
            ct.ThrowIfCancellationRequested();
            _sourceSession = s;
            _targetSession = t;
            _comparedSource = source;
            _comparedTarget = target;
            Show(snapshots[0], snapshots[1], comparison, _time.GetElapsedTime(started));
        }
        catch (OperationCanceledException)
        {
            if (State == CompareState.Comparing)
                State = previous;
        }
        catch (Exception ex)
        {
            FailureText = UiText.Format("compare.failed", ex.Message);
            State = CompareState.Failed;
        }
        finally
        {
            if (ReferenceEquals(_compareCts, cts))
                _compareCts = null;
        }
    }

    /// <summary>Stops a comparison in progress; the previous result stays.</summary>
    [RelayCommand]
    private void CancelCompare() => _compareCts?.Cancel();

    private static async Task<ConnectionSession> Resolve(CompareEndpoint endpoint, CancellationToken ct) =>
        endpoint.Database is { } db ? await endpoint.Session.OpenDatabaseAsync(db, ct) : endpoint.Session;

    private void Reset()
    {
        _comparison = null;
        _sourceSnapshot = _targetSnapshot = null;
        _items.Clear();
        State = CompareState.Idle;
        ComparedAt = null;
        FailureText = "";
        Groups.Clear();
        SelectedItem = null;
        CancelBuild();
        _generated = null;
        SetScript("");
        ScriptNotes = [];
        ExecuteSummary = "";
        ExecuteError = "";
        RaiseSummary();
    }

    private void Show(SchemaSnapshot source, SchemaSnapshot target, SchemaComparison comparison, TimeSpan duration)
    {
        _sourceSnapshot = source;
        _targetSnapshot = target;
        _comparison = comparison;
        _items.Clear();
        _items.AddRange(comparison.Differences.Select(d => new CompareItemViewModel(d, d.Kind != DifferenceKind.Removed, OnItemChanged)));
        ComparedAt = _time.GetUtcNow();
        Duration = duration;
        ExecuteError = "";
        State = CompareState.Ready;
        RebuildGroups();
        SelectedItem = Groups.Count > 0 ? Groups[0].Items[0] : null;
        BuildScript(immediate: true, replaceEdits: true);
        OnPropertyChanged(nameof(ComparedText));
        RaiseSummary();
    }

    // ---- difference list ----

    /// <summary>Gets the groups of the difference list that pass the filters; empty groups are left out.</summary>
    public ObservableCollection<CompareGroupViewModel> Groups { get; } = [];

    /// <summary>Gets the number of added objects.</summary>
    public int AddedCount => _items.Count(i => i.Difference.Kind == DifferenceKind.Added);

    /// <summary>Gets the number of modified objects.</summary>
    public int ModifiedCount => _items.Count(i => i.Difference.Kind == DifferenceKind.Modified);

    /// <summary>Gets the number of removed objects.</summary>
    public int RemovedCount => _items.Count(i => i.Difference.Kind == DifferenceKind.Removed);

    /// <summary>Gets or sets whether added objects are listed.</summary>
    [ObservableProperty]
    public partial bool ShowAdded { get; set; } = true;

    /// <summary>Gets or sets whether modified objects are listed.</summary>
    [ObservableProperty]
    public partial bool ShowModified { get; set; } = true;

    /// <summary>Gets or sets whether removed objects are listed.</summary>
    [ObservableProperty]
    public partial bool ShowRemoved { get; set; } = true;

    /// <summary>Gets or sets the text the object names are filtered by.</summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = "";

    partial void OnShowAddedChanged(bool value) => RebuildGroups();

    partial void OnShowModifiedChanged(bool value) => RebuildGroups();

    partial void OnShowRemovedChanged(bool value) => RebuildGroups();

    partial void OnFilterTextChanged(string value) => RebuildGroups();

    private void RebuildGroups()
    {
        var filter = FilterText.Trim();
        var visible = _items.Where(i =>
            (i.Difference.Kind switch { DifferenceKind.Added => ShowAdded, DifferenceKind.Removed => ShowRemoved, _ => ShowModified })
            && (filter.Length == 0 || i.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))).ToList();
        Groups.Clear();
        foreach (var category in Enum.GetValues<ObjectCategory>())
        {
            var rows = visible.Where(i => i.Difference.Category == category).ToList();
            if (rows.Count > 0)
                Groups.Add(new CompareGroupViewModel(category, UiText.Get(category switch
                {
                    ObjectCategory.Table => "compare.group.table",
                    ObjectCategory.View => "compare.group.view",
                    ObjectCategory.Index => "compare.group.index",
                    _ => "compare.group.routine",
                }), rows));
        }

        if (SelectedItem is { } selected && !visible.Contains(selected))
            SelectedItem = null;
    }

    /// <summary>Gets the number of differences included in the script.</summary>
    public int IncludedCount => _items.Count(i => i.IsIncluded);

    /// <summary>Gets the number of differences found.</summary>
    public int TotalCount => _items.Count;

    /// <summary>Gets <c>Tout inclure · 7 sur 8 sélectionnés</c>.</summary>
    public string SelectionText => UiText.Format("compare.selection", IncludedCount, TotalCount);

    /// <summary>Gets <c>8 différences · 7 incluses</c> for the status bar.</summary>
    public string DifferencesText => UiText.FormatPlural("compare.differences", TotalCount, IncludedCount);

    /// <summary>Gets <c>STAGING -&gt; PRODUCTION</c> for the status bar, empty when neither side has an environment.</summary>
    public string EnvironmentFlow =>
        _comparedSource is { } s && _comparedTarget is { } t && (s.Environment != ConnectionEnvironment.None || t.Environment != ConnectionEnvironment.None)
            ? $"{EnvironmentName(s.Environment)} -> {EnvironmentName(t.Environment)}"
            : "";

    /// <summary>Gets the server version of the source for the status bar.</summary>
    public string SourceServerVersion => _sourceSession?.Connection.ServerVersion ?? "";

    /// <summary>Gets the server version of the target for the status bar.</summary>
    public string TargetServerVersion => _targetSession?.Connection.ServerVersion ?? "";

    /// <summary>Gets <c>Source · &lt;connection&gt;</c>.</summary>
    public string SourceCaption => _comparedSource is { } s ? UiText.Format("compare.sourceCaption", s.Label) : "";

    /// <summary>Gets <c>Cible · &lt;connection&gt;</c>.</summary>
    public string TargetCaption => _comparedTarget is { } t ? UiText.Format("compare.targetCaption", t.Label) : "";

    private static string EnvironmentName(ConnectionEnvironment e) =>
        e switch
        {
            ConnectionEnvironment.Production => UiText.Get("compare.env.production"),
            ConnectionEnvironment.Staging => UiText.Get("compare.env.staging"),
            ConnectionEnvironment.Development => UiText.Get("compare.env.development"),
            ConnectionEnvironment.Local => UiText.Get("compare.env.local"),
            _ => "",
        };

    /// <summary>Gets whether every difference is included, <see langword="false"/> when none is, <see langword="null"/> when some are.</summary>
    public bool? AllIncluded => _items.Count == 0 || IncludedCount == 0 ? false : IncludedCount == _items.Count ? true : null;

    /// <summary>Includes or excludes every difference that passes the filters (<c>Tout inclure</c>).</summary>
    /// <param name="included">The new state of every listed checkbox.</param>
    [RelayCommand]
    private void SetAllIncluded(bool included)
    {
        _bulk = true;
        foreach (var item in Groups.SelectMany(g => g.Items))
            item.IsIncluded = included;
        _bulk = false;
        OnInclusionChanged();
    }

    private void OnItemChanged(CompareItemViewModel item)
    {
        if (!_bulk)
            OnInclusionChanged();
    }

    private void OnInclusionChanged()
    {
        RaiseSummary();
        BuildScript(immediate: false, replaceEdits: false);
    }

    private void RaiseSummary()
    {
        foreach (var name in new[]
        {
            nameof(AddedCount), nameof(ModifiedCount), nameof(RemovedCount), nameof(IncludedCount), nameof(TotalCount), nameof(SelectionText),
            nameof(DifferencesText), nameof(EnvironmentFlow), nameof(SourceServerVersion), nameof(TargetServerVersion), nameof(SourceCaption),
            nameof(TargetCaption), nameof(AllIncluded), nameof(Session), nameof(Environment),
        })
            OnPropertyChanged(name);
        ExecuteScriptCommand.NotifyCanExecuteChanged();
    }

    // ---- detail of the selected object ----

    /// <summary>Gets or sets the difference whose DDL is shown.</summary>
    [ObservableProperty]
    public partial CompareItemViewModel? SelectedItem { get; set; }

    /// <summary>Gets the DDL of the selected object on the source side.</summary>
    [ObservableProperty]
    public partial string SourceText { get; private set; } = "";

    /// <summary>Gets the DDL of the selected object on the target side.</summary>
    [ObservableProperty]
    public partial string TargetText { get; private set; } = "";

    /// <summary>Gets the two DDLs aligned line by line.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<AlignedLine> Lines { get; private set; } = [];

    /// <summary>Gets the index in <see cref="Lines"/> of the difference the last jump went to, <c>-1</c> before any jump.</summary>
    [ObservableProperty]
    public partial int CurrentDifferenceLine { get; private set; } = -1;

    partial void OnSelectedItemChanged(CompareItemViewModel? value)
    {
        CurrentDifferenceLine = -1;
        if (value is null || _sourceSnapshot is null || _targetSnapshot is null)
        {
            SourceText = TargetText = "";
            Lines = [];
            return;
        }

        var dialect = _comparedSource?.Dialect ?? DatabaseDialect.PostgreSql;
        SourceText = DdlOf(_sourceSnapshot, value.Difference, dialect);
        TargetText = DdlOf(_targetSnapshot, value.Difference, dialect);
        Lines = LineDiff.Align(SourceText, TargetText);
    }

    /// <summary>Moves <see cref="CurrentDifferenceLine"/> to the next line that differs, wrapping around (<c>F7</c>).</summary>
    [RelayCommand]
    private void NextDifference() => Jump(1);

    /// <summary>Moves <see cref="CurrentDifferenceLine"/> to the previous line that differs, wrapping around (<c>Maj+F7</c>).</summary>
    [RelayCommand]
    private void PreviousDifference() => Jump(-1);

    private void Jump(int step)
    {
        var n = Lines.Count;
        var from = CurrentDifferenceLine < 0 ? (step > 0 ? -1 : n) : CurrentDifferenceLine;
        for (var k = 1; k <= n; k++)
        {
            var index = ((from + step * k) % n + n) % n;
            if (Lines[index].Mark != LineMark.Same)
            {
                CurrentDifferenceLine = index;
                return;
            }
        }
    }

    private static bool Eq(string? a, string? b) => string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);

    private static string DdlOf(SchemaSnapshot snapshot, ObjectDifference x, DatabaseDialect dialect)
    {
        switch (x.Category)
        {
            case ObjectCategory.Table:
            case ObjectCategory.View:
                var relation = snapshot.Tables.FirstOrDefault(t => Eq(t.Schema, x.Schema) && Eq(t.Name, x.Name) && t.Kind.IsViewLike() == (x.Category == ObjectCategory.View));
                if (relation is null)
                    return "";
                return x.Category == ObjectCategory.View
                    ? relation.Definition ?? UiText.Get("compare.noDefinition")
                    : DdlGenerators.ToScript(DdlGenerators.For(dialect).CreateTable(TableDesign.FromTable(relation, snapshot.Triggers)));
            case ObjectCategory.Routine:
                var routine = snapshot.Routines.FirstOrDefault(r => Eq(r.Schema, x.Schema) && Eq(r.Signature, x.Name));
                return routine is null ? "" : routine.Definition ?? UiText.Get("compare.noDefinition");
            default:
                foreach (var t in snapshot.Tables.Where(t => t.Kind == TableKind.Table && Eq(t.Schema, x.Schema)))
                    foreach (var i in t.Indexes.Where(i => Eq($"{t.Name}.{i.Name}", x.Name)))
                        return $"CREATE {(i.IsUnique ? "UNIQUE " : "")}INDEX {i.Name} ON {t.Name} ({string.Join(", ", i.Columns)})";
                return "";
        }
    }

    // ---- migration script ----

    /// <summary>Gets or sets the script text; typing in it sets <see cref="IsScriptEdited"/> and stops it being replaced by the next build.</summary>
    [ObservableProperty]
    public partial string Script { get; set; } = "";

    /// <summary>Gets whether the user changed the script, so that a new selection no longer overwrites it.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegenerateCommand))]
    public partial bool IsScriptEdited { get; private set; }

    /// <summary>Gets whether a script build is waiting or running.</summary>
    [ObservableProperty]
    public partial bool IsBuilding { get; private set; }

    /// <summary>Gets the localized notes about the generated script (data loss, implicit commit...), shown before it runs.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> ScriptNotes { get; private set; } = [];

    /// <summary>Gets the task of the latest script build, for callers that need the finished script.</summary>
    internal Task ScriptTask { get; private set; } = Task.CompletedTask;

    partial void OnScriptChanged(string value)
    {
        if (!_settingScript)
            IsScriptEdited = true;
        ExecuteScriptCommand.NotifyCanExecuteChanged();
    }

    private void SetScript(string text)
    {
        _settingScript = true;
        Script = text;
        _settingScript = false;
        IsScriptEdited = false;
    }

    private void CancelBuild()
    {
        _buildVersion++;
        _buildCts?.Cancel();
        IsBuilding = false;
    }

    private void BuildScript(bool immediate, bool replaceEdits)
    {
        if (_comparison is not { } comparison || _sourceSnapshot is not { } source || _targetSnapshot is not { } target
            || _comparedSource is not { } src || _comparedTarget is not { } tgt)
            return;
        var version = ++_buildVersion;
        _buildCts?.Cancel();
        var cts = _buildCts = new CancellationTokenSource();
        var included = _items.Where(i => i.IsIncluded).Select(i => i.Key).ToHashSet();
        var options = new MigrationOptions(src.Label, tgt.Label, _time.GetLocalNow().DateTime);
        IsBuilding = true;
        ScriptTask = BuildAsync(version, immediate, replaceEdits, src.Dialect, source, target, comparison, included, options, cts.Token);
    }

    private async Task BuildAsync(
        int version, bool immediate, bool replaceEdits, DatabaseDialect dialect,
        SchemaSnapshot source, SchemaSnapshot target, SchemaComparison comparison, IReadOnlySet<ObjectKey> included, MigrationOptions options, CancellationToken ct)
    {
        try
        {
            if (!immediate && _debounce > TimeSpan.Zero)
                await Task.Delay(_debounce, _time, ct);
            var script = await Task.Run(() => _build(dialect, source, target, comparison, included, options), ct);
            if (version != _buildVersion)
                return; // a newer build owns the result
            _generated = script;
            ScriptNotes = [.. script.Notes.Select(NoteText)];
            if (replaceEdits || !IsScriptEdited)
                SetScript(script.Text);
            else
                RegenerateCommand.NotifyCanExecuteChanged();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (version == _buildVersion)
        {
            ExecuteError = ex.Message;
        }
        finally
        {
            if (version == _buildVersion)
                IsBuilding = false;
        }
    }

    private static string NoteText(MigrationNote note) =>
        UiText.Format(note.Kind switch
        {
            MigrationNoteKind.DropsData => "compare.note.dropsData",
            MigrationNoteKind.NarrowsColumn => "compare.note.narrowsColumn",
            MigrationNoteKind.MySqlImplicitCommit => "compare.note.mySqlImplicitCommit",
            _ => "compare.note.sqliteRebuild",
        }, note.Subject);

    /// <summary>Replaces the edited script with the one generated for the current inclusion (<c>Régénérer</c>).</summary>
    [RelayCommand(CanExecute = nameof(IsScriptEdited))]
    private void Regenerate()
    {
        if (_generated is { } g)
            SetScript(g.Text);
    }

    /// <summary>Asks the head to copy the script.</summary>
    [RelayCommand]
    private void Copy() => CopyRequested?.Invoke(Script);

    /// <summary>Saves the script in a <c>.sql</c> file chosen by the user.</summary>
    [RelayCommand]
    private async Task SaveAsAsync()
    {
        var path = await _dialogs.PickSaveFileAsync(_comparedTarget is { } t ? $"{t.DatabaseName}-migration" : "migration", "sql");
        if (path is null)
            return;
        try
        {
            await File.WriteAllTextAsync(path, Script);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _dialogs.ShowErrorAsync(UiText.Get("compare.execute.title"), ex.Message);
        }
    }

    /// <summary>Opens the script in a new query tab on the target connection.</summary>
    [RelayCommand]
    private void OpenInEditor()
    {
        if (_targetSession is { } session)
            ScriptRequested?.Invoke(session, Script);
    }

    // ---- executing ----

    /// <summary>Gets whether the script is running on the target.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteScriptCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompareCommand))]
    public partial bool IsExecuting { get; private set; }

    /// <summary>Gets the sentence shown after a successful run.</summary>
    [ObservableProperty]
    public partial string ExecuteSummary { get; private set; } = "";

    /// <summary>Gets the engine message and the failing statement after a failed run; the script is kept.</summary>
    [ObservableProperty]
    public partial string ExecuteError { get; private set; } = "";

    private bool CanExecuteScript() =>
        State == CompareState.Ready && _targetSession is not null && !IsExecuting && Statements().Count > 0;

    private List<string> Statements()
    {
        if (_comparedSource is not { } src)
            return [];
        var dialect = src.Dialect;
        var list = new List<string>();
        foreach (var span in SqlStatementSplitter.Split(Script, dialect))
        {
            var text = Script.Substring(span.Start, span.Length);
            var traits = StatementClassifier.Classify(text, dialect);
            // The script wraps itself in BEGIN / COMMIT; the run brackets the statements in a scope that respects the session transaction.
            if (traits.Kind == StatementKind.Empty || traits.Control is TransactionControl.Begin or TransactionControl.Commit or TransactionControl.Rollback)
                continue;
            list.Add(text);
        }

        return list;
    }

    private static bool IsPragma(string sql) => sql.AsSpan().TrimStart().StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Runs the script on the target (<c>Exécuter sur la cible</c>). A production target always asks the typed confirmation of phase 15 first;
    /// an open transaction of the target is respected through a savepoint; success reads the target again and compares anew.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteScript))]
    private async Task ExecuteScriptAsync()
    {
        await ScriptTask;
        if (_targetSession is not { } session || _comparedTarget is not { } endpoint)
            return;
        var statements = Statements();
        if (statements.Count == 0)
            return;
        var profile = session.Profile;
        var dialect = profile.Dialect;
        var tx = session.Transaction;
        if (!DdlGenerators.UsesTransactionalDdl(dialect) && tx.State == TransactionState.Open
            && !await _dialogs.ConfirmAsync(UiText.Get("tx.implicitCommit.title"), UiText.Get("tx.implicitCommit.message")))
            return;
        if (profile.Environment == ConnectionEnvironment.Production)
        {
            if (!await ProductionRequestBuilder.ConfirmWriteAsync(_dialogs, profile, ConfirmationKind.SchemaChange, statements, statements.Count, endpoint.Label, tx.Mode))
                return;
        }
        else if (ScriptNotes.Count > 0
            && !await _dialogs.ConfirmAsync(UiText.Get("compare.execute.title"), UiText.Format("compare.execute.confirm", string.Join("\n", ScriptNotes), endpoint.Label)))
        {
            return;
        }

        IsExecuting = true;
        ExecuteSummary = "";
        ExecuteError = "";
        var ok = false;
        using (tx.BeginOperation())
        {
            using var cts = new CancellationTokenSource();
            _runCts = cts;
            var transactional = DdlGenerators.UsesTransactionalDdl(dialect);
            var lead = 0;
            var tail = statements.Count;
            if (transactional)
            {
                while (lead < tail && IsPragma(statements[lead]))
                    lead++;
                while (tail > lead && IsPragma(statements[tail - 1]))
                    tail--;
            }

            StatementScope? scope = null;
            var current = "";
            try
            {
                for (var i = 0; i < statements.Count; i++)
                {
                    if (transactional && i == lead && lead < tail)
                        scope = await SqlRunner.BeginScopeAsync(session.Connection, dialect, tx, Title, cts.Token);
                    if (transactional && i == tail && scope is not null)
                    {
                        await scope.CompleteAsync(cts.Token);
                        scope = null;
                    }

                    current = statements[i];
                    var rows = await SqlRunner.ExecuteAsync(session.Connection, current, [], cts.Token);
                    if (scope is not null)
                        scope.Observe(current, rows);
                    else if (!IsPragma(current))
                        tx.ObserveSuccess(current, StatementClassifier.Classify(current, dialect), rows, Title);
                }

                if (scope is not null)
                {
                    await scope.CompleteAsync(cts.Token);
                    scope = null;
                }

                ok = true;
            }
            catch (Exception ex)
            {
                if (scope is not null)
                    await scope.FailAsync();
                if (transactional)
                {
                    // Restores what a SQLite rebuild switched off; the violation check is skipped on purpose.
                    foreach (var pragma in statements.Skip(tail).Where(s => !s.Contains("foreign_key_check", StringComparison.OrdinalIgnoreCase)))
                    {
                        try
                        {
                            await SqlRunner.ExecuteAsync(session.Connection, pragma, [], CancellationToken.None);
                        }
                        catch (Exception)
                        {
                            // Best effort: the original failure is reported instead.
                        }
                    }
                }

                var message = ex is OperationCanceledException ? UiText.Get("query.cancelled") : ex.Message;
                var first = current.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(l => !l.StartsWith("--", StringComparison.Ordinal)) ?? current;
                ExecuteError = UiText.Format("compare.failedAt", message, first);
                await _dialogs.ShowErrorAsync(UiText.Get("compare.executeFailed"), ExecuteError);
            }
            finally
            {
                _runCts = null;
                IsExecuting = false;
            }
        }

        if (!ok)
            return;
        ExecuteSummary = UiText.FormatPlural("compare.executed", statements.Count);
        try
        {
            await session.RefreshSchemaAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            // The comparison below reads the schema itself and reports the failure.
        }

        await CompareAsync();
    }

    /// <summary>Asks the workspace to close the tab.</summary>
    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this);

    /// <summary>Cancels a comparison, a script build or a run in progress.</summary>
    public void Dispose()
    {
        _compareCts?.Cancel();
        _buildCts?.Cancel();
        _runCts?.Cancel();
    }
}
