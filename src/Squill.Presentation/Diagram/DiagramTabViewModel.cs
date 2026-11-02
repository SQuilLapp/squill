using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Platform.Storage;
using Squill.Presentation.Sessions;

namespace Squill.Presentation.Diagram;

/// <summary>The size of the area the diagram is drawn in.</summary>
/// <param name="Width">Width in screen pixels.</param>
/// <param name="Height">Height in screen pixels.</param>
public readonly record struct ErSize(double Width, double Height);

/// <summary>Headless state of an ER diagram tab: scene, selection, pan and zoom, dragging and layout persistence.</summary>
public sealed partial class DiagramTabViewModel : ViewModelBase, IDisposable
{
    private const double MinZoom = 0.1;
    private const double MaxZoom = 4.0;
    private const double FitMargin = 24;
    private const int LargeSchema = 200;
    private const int CappedCount = 50;

    private readonly ConnectionSession _session;
    private readonly IDiagramLayoutStore _store;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _dispatcher;
    private readonly ErLayoutOptions _options;
    private readonly Dictionary<string, DiagramPosition> _positions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _z = [];
    private readonly CancellationTokenSource _cts = new();
    private bool _ready;
    private bool _showAll;
    private double _zoom = 1.0;
    private string? _dragKey;
    private ErPoint? _grab;

    /// <summary>Creates the view-model and lays out the session's current schema.</summary>
    /// <param name="session">The session whose schema is drawn.</param>
    /// <param name="store">Persists box positions, pan and zoom per profile.</param>
    /// <param name="dialogs">Reports errors.</param>
    /// <param name="measureText">Width of a text in the diagram font, supplied by the head.</param>
    /// <param name="dispatcher">Marshals error reports from background saves to the UI thread.</param>
    public DiagramTabViewModel(
        ConnectionSession session,
        IDiagramLayoutStore store,
        IDialogService dialogs,
        Func<string, double> measureText,
        IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(measureText);
        ArgumentNullException.ThrowIfNull(dispatcher);
        _session = session;
        _store = store;
        _dialogs = dialogs;
        _dispatcher = dispatcher;
        _options = new ErLayoutOptions(measureText);
        Title = "ER - " + session.Profile.Name;
        Model = new ErModel([], [], 0);
        Boxes = new Dictionary<string, ErBox>();
        Edges = [];
        Rebuild();
        _ready = true;
    }

    /// <summary>Raised when the user opens a table or view of the diagram.</summary>
    public event EventHandler<TableInfo>? OpenTableRequested;

    /// <summary>Raised when the user asks to design a table of the diagram.</summary>
    public event EventHandler<TableInfo>? DesignTableRequested;

    /// <summary>Gets the tab title.</summary>
    public string Title { get; }

    /// <summary>Gets the model of the entities currently shown.</summary>
    [ObservableProperty]
    public partial ErModel Model { get; private set; }

    /// <summary>Gets the box of every shown entity, keyed case-insensitively.</summary>
    [ObservableProperty]
    public partial IReadOnlyDictionary<string, ErBox> Boxes { get; private set; }

    /// <summary>Gets the routed relations.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ErEdge> Edges { get; private set; }

    /// <summary>Gets or sets the key of the selected entity.</summary>
    [ObservableProperty]
    public partial string? SelectedKey { get; set; }

    /// <summary>Gets or sets the zoom factor, clamped to 0.1 through 4.</summary>
    public double Zoom
    {
        get => _zoom;
        set => SetProperty(ref _zoom, Math.Clamp(value, MinZoom, MaxZoom));
    }

    /// <summary>Gets or sets the horizontal pan offset in screen pixels.</summary>
    [ObservableProperty]
    public partial double OffsetX { get; set; }

    /// <summary>Gets or sets the vertical pan offset in screen pixels.</summary>
    [ObservableProperty]
    public partial double OffsetY { get; set; }

    /// <summary>Gets or sets whether views are drawn.</summary>
    [ObservableProperty]
    public partial bool IncludeViews { get; set; }

    /// <summary>Gets or sets the text entity names must contain, ignoring case.</summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = "";

    /// <summary>Gets the number of foreign keys whose referenced table is not drawn.</summary>
    [ObservableProperty]
    public partial int UnresolvedRelations { get; private set; }

    /// <summary>Gets the entity the diagram is focused on, or null when everything is shown.</summary>
    [ObservableProperty]
    public partial string? FocusKey { get; private set; }

    /// <summary>Gets or sets how many relation hops around <see cref="FocusKey"/> are shown, 1 through 3.</summary>
    public int FocusDepth
    {
        get;
        set
        {
            if (SetProperty(ref field, Math.Clamp(value, 1, 3)) && _ready)
                Rebuild();
        }
    } = 1;

    /// <summary>Gets a message about a truncated drawing, or an empty string.</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = "";

    /// <summary>Gets or sets whether the SVG export uses the dark palette; set by the head from the current theme.</summary>
    [ObservableProperty]
    public partial bool IsDarkTheme { get; set; }

    partial void OnIncludeViewsChanged(bool value) => RebuildIfReady();

    partial void OnFilterTextChanged(string value) => RebuildIfReady();

    partial void OnFocusKeyChanged(string? value) => RebuildIfReady();

    /// <summary>Restores the saved layout, if any, and redraws.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the saved layout is applied.</returns>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var saved = await _store.LoadAsync(_session.Profile.Id, ct);
        if (saved is null)
            return;
        foreach (var (key, p) in saved.Positions)
            _positions[key] = p;
        Zoom = saved.Zoom;
        OffsetX = saved.OffsetX;
        OffsetY = saved.OffsetY;
        Rebuild();
    }

    /// <summary>Converts a screen point to diagram coordinates.</summary>
    /// <param name="screen">Point in screen pixels.</param>
    /// <returns>The diagram point under it.</returns>
    public ErPoint ToWorld(ErPoint screen) => new((screen.X - OffsetX) / Zoom, (screen.Y - OffsetY) / Zoom);

    /// <summary>Converts a diagram point to screen coordinates.</summary>
    /// <param name="world">Point in diagram coordinates.</param>
    /// <returns>Its position in screen pixels.</returns>
    public ErPoint ToScreen(ErPoint world) => new((world.X * Zoom) + OffsetX, (world.Y * Zoom) + OffsetY);

    /// <summary>Zooms while keeping the diagram point under an anchor fixed.</summary>
    /// <param name="screenAnchor">Screen point that stays put, usually the pointer.</param>
    /// <param name="factor">Multiplier of the current zoom; the result is clamped.</param>
    public void ZoomAt(ErPoint screenAnchor, double factor)
    {
        var world = ToWorld(screenAnchor);
        Zoom *= factor;
        OffsetX = screenAnchor.X - (world.X * Zoom);
        OffsetY = screenAnchor.Y - (world.Y * Zoom);
    }

    /// <summary>Moves the view.</summary>
    /// <param name="dx">Horizontal shift in screen pixels.</param>
    /// <param name="dy">Vertical shift in screen pixels.</param>
    public void Pan(double dx, double dy)
    {
        OffsetX += dx;
        OffsetY += dy;
    }

    /// <summary>Finds the entity drawn on top at a diagram point.</summary>
    /// <param name="worldPoint">Point in diagram coordinates.</param>
    /// <returns>The entity, or null when the point is on empty space.</returns>
    public ErEntity? HitTestEntity(ErPoint worldPoint)
    {
        for (var i = _z.Count - 1; i >= 0; i--)
        {
            if (Boxes.TryGetValue(_z[i], out var box) && box.Contains(worldPoint))
                return Model.Find(_z[i]);
        }

        return null;
    }

    /// <summary>Starts moving an entity box, selects it and brings it to the front.</summary>
    /// <param name="key">Entity key.</param>
    public void BeginDrag(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var entity = Model.Find(key);
        if (entity is null)
            return;
        _dragKey = entity.Key;
        _grab = null;
        SelectedKey = entity.Key;
        _z.RemoveAll(k => string.Equals(k, entity.Key, StringComparison.OrdinalIgnoreCase));
        _z.Add(entity.Key);
    }

    /// <summary>Moves the dragged box so it follows the pointer and reroutes the relations.</summary>
    /// <param name="world">Pointer position in diagram coordinates.</param>
    public void DragTo(ErPoint world)
    {
        if (_dragKey is null || !Boxes.TryGetValue(_dragKey, out var box))
            return;
        var grab = _grab ??= new ErPoint(world.X - box.X, world.Y - box.Y);
        var boxes = new Dictionary<string, ErBox>(Boxes, StringComparer.OrdinalIgnoreCase)
        {
            [_dragKey] = box with { X = world.X - grab.X, Y = world.Y - grab.Y },
        };
        Boxes = boxes;
        Edges = ErRouter.Route(Model, boxes, _options);
    }

    /// <summary>Ends the drag and saves the layout.</summary>
    public void EndDrag()
    {
        if (_dragKey is null)
            return;
        _dragKey = null;
        _grab = null;
        _ = SaveSafeAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            await _session.RefreshSchemaAsync(ct);
            Rebuild();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _dialogs.ShowErrorAsync("Refresh failed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ExportSvgAsync()
    {
        var path = await _dialogs.PickSaveFileAsync(_session.Profile.Name, "svg");
        if (path is null)
            return;
        try
        {
            await File.WriteAllTextAsync(path, ErSvgExporter.Render(Model, Boxes, Edges, _options, IsDarkTheme), _cts.Token);
            StatusText = "Saved " + path;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _dialogs.ShowErrorAsync("Export failed", ex.Message);
        }
    }

    [RelayCommand]
    private void FitToScreen(ErSize viewport)
    {
        if (Boxes.Count == 0)
        {
            Zoom = 1;
            OffsetX = 0;
            OffsetY = 0;
            return;
        }

        var minX = Boxes.Values.Min(b => b.X);
        var minY = Boxes.Values.Min(b => b.Y);
        var width = Boxes.Values.Max(b => b.Right) - minX;
        var height = Boxes.Values.Max(b => b.Bottom) - minY;
        Zoom = Math.Min(
            Math.Max(viewport.Width - (2 * FitMargin), 1) / Math.Max(width, 1),
            Math.Max(viewport.Height - (2 * FitMargin), 1) / Math.Max(height, 1));
        OffsetX = ((viewport.Width - (width * Zoom)) / 2) - (minX * Zoom);
        OffsetY = ((viewport.Height - (height * Zoom)) / 2) - (minY * Zoom);
    }

    [RelayCommand]
    private async Task ResetLayoutAsync()
    {
        _positions.Clear();
        Rebuild();
        try
        {
            await _store.DeleteAsync(_session.Profile.Id, _cts.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _dialogs.ShowErrorAsync("Reset failed", ex.Message);
        }
    }

    [RelayCommand]
    private void FocusSelected()
    {
        if (SelectedKey is not null)
            FocusKey = SelectedKey;
    }

    [RelayCommand]
    private void ShowAll()
    {
        _showAll = true;
        if (FocusKey is not null)
            FocusKey = null;
        else
            Rebuild();
    }

    [RelayCommand]
    private void Activate(string? key)
    {
        if (key is not null && FindTable(key) is { } table)
            OpenTableRequested?.Invoke(this, table);
    }

    [RelayCommand]
    private void Design(string? key)
    {
        if (key is not null && FindTable(key) is { } table)
            DesignTableRequested?.Invoke(this, table);
    }

    /// <summary>Stops pending saves from reporting after the tab is closed.</summary>
    public void Dispose() => _cts.Cancel();

    private void RebuildIfReady()
    {
        if (_ready)
            Rebuild();
    }

    private TableInfo? FindTable(string key)
    {
        var entity = Model.Find(key);
        return entity is null ? null : _session.Schema.FindTable(entity.Schema, entity.Name);
    }

    private void Rebuild()
    {
        var all = ErModel.Build(_session.Schema, new ErFilter(null, IncludeViews));
        IEnumerable<ErEntity> visible = all.Entities;
        if (FilterText.Length > 0)
            visible = visible.Where(e => e.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        if (FocusKey is not null)
        {
            var near = ErModel.Neighborhood(all, FocusKey, FocusDepth);
            visible = visible.Where(e => near.Contains(e.Key));
        }

        var keys = visible.Select(e => e.Key).ToList();
        var status = "";
        if (!_showAll && keys.Count > LargeSchema)
        {
            var weight = all.Relations
                .SelectMany(r => new[] { r.FromKey, r.ToKey })
                .GroupBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
            status = $"Showing {CappedCount} of {keys.Count} tables, use the filter or Focus";
            keys = [.. keys.OrderByDescending(k => weight.GetValueOrDefault(k)).ThenBy(k => k, StringComparer.OrdinalIgnoreCase).Take(CappedCount)];
        }

        var model = keys.Count == all.Entities.Count
            ? all
            : ErModel.Build(_session.Schema, new ErFilter(keys.ToHashSet(StringComparer.OrdinalIgnoreCase), IncludeViews));
        var boxes = new Dictionary<string, ErBox>(ErLayout.Compute(model, _options), StringComparer.OrdinalIgnoreCase);
        foreach (var key in boxes.Keys.ToList())
        {
            if (_positions.TryGetValue(key, out var p))
                boxes[key] = boxes[key] with { X = p.X, Y = p.Y };
        }

        _z.Clear();
        _z.AddRange(model.Entities.Select(e => e.Key));
        Model = model;
        Boxes = boxes;
        Edges = ErRouter.Route(model, boxes, _options);
        UnresolvedRelations = all.UnresolvedRelations;
        StatusText = status;
        if (SelectedKey is not null && model.Find(SelectedKey) is null)
            SelectedKey = null;
    }

    private async Task SaveSafeAsync()
    {
        try
        {
            foreach (var (key, box) in Boxes)
                _positions[key] = new DiagramPosition(box.X, box.Y);
            await _store.SaveAsync(
                _session.Profile.Id,
                new DiagramLayout(new Dictionary<string, DiagramPosition>(_positions, StringComparer.OrdinalIgnoreCase), Zoom, OffsetX, OffsetY),
                _cts.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _dispatcher.Post(() => _ = _dialogs.ShowErrorAsync("Saving the diagram layout failed", ex.Message).AsTask());
        }
    }
}
