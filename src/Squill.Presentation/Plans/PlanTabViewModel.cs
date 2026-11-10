using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Core.Plans;
using Squill.Data;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;

namespace Squill.Presentation.Plans;

/// <summary>A workspace tab showing the plan of one statement of a query tab; running again replaces its content.</summary>
public sealed partial class PlanTabViewModel : ViewModelBase
{
    private IReadOnlyList<QueryParameter> _parameters = [];

    internal PlanTabViewModel(QueryTabViewModel source, PlanViewModel plan, string statement, IReadOnlyList<QueryParameter> parameters, PlanMode mode, string message)
    {
        Source = source;
        Plan = plan;
        Statement = statement;
        _parameters = parameters;
        Mode = mode;
        MessageLine = message;
        source.PropertyChanged += OnSourceChanged;
    }

    /// <summary>Raised when the user closes the tab.</summary>
    public event Action<PlanTabViewModel>? CloseRequested;

    /// <summary>Gets the query tab this plan belongs to.</summary>
    public QueryTabViewModel Source { get; }

    /// <summary>Gets the tab title, <c>&lt;query tab&gt; · Plan</c>, kept in sync with the query tab.</summary>
    public string Title => UiText.Format("tab.plan", Source.Title);

    /// <summary>Gets the environment of the connection, which the head turns into the tab color.</summary>
    public ConnectionEnvironment Environment => Source.Session.Profile.Environment;

    /// <summary>Gets the statement the plan was made for, exactly as sent to the engine.</summary>
    public string Statement { get; private set; } = "";

    /// <summary>Gets whether the plan is estimated or actual.</summary>
    public PlanMode Mode { get; private set; }

    /// <summary>Gets the plan shown, replaced in place by <see cref="RerunCommand"/>.</summary>
    [ObservableProperty]
    public partial PlanViewModel Plan { get; private set; }

    /// <summary>Gets whether a plan is being fetched.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    /// <summary>Gets a line shown above the plan when something went wrong (parse failure, failed re-run), empty otherwise.</summary>
    [ObservableProperty]
    public partial string MessageLine { get; private set; } = "";

    /// <summary>Gets the server version for the status bar.</summary>
    public string ServerVersion => Source.Session.Connection.ServerVersion;

    /// <summary>Gets the right side of the status bar: node count and execution time when the engine gave one.</summary>
    public string SummaryText => Plan.Plan.ExecutionTimeMs is { } ms ? $"{Plan.NodeCountText} · {PlanText.Milliseconds(ms)}" : Plan.NodeCountText;

    internal void Update(PlanViewModel plan, string statement, IReadOnlyList<QueryParameter> parameters, PlanMode mode, string message)
    {
        Plan = plan;
        Statement = statement;
        _parameters = parameters;
        Mode = mode;
        MessageLine = message;
        OnPropertyChanged(nameof(SummaryText));
    }

    [RelayCommand]
    private async Task RerunAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var (plan, message) = await Source.LoadPlanAsync(Statement, _parameters, Mode, CancellationToken.None);
            Update(plan, Statement, _parameters, Mode, message);
        }
        catch (Exception ex)
        {
            MessageLine = ex is PlanUnavailableException u ? QueryTabViewModel.UnavailableText(u) : ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Opens a suggested statement in a new query tab on the session of the source tab.</summary>
    /// <param name="script">The statement text.</param>
    [RelayCommand]
    private void OpenInEditor(string? script)
    {
        if (!string.IsNullOrEmpty(script))
        {
            Source.RequestScript(script);
        }
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this);

    private void OnSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueryTabViewModel.Title))
        {
            OnPropertyChanged(nameof(Title));
        }
    }
}
