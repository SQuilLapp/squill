using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Presentation.Localization;
using Squill.Sql.Analysis;

namespace Squill.Presentation.Query;

public sealed partial class QueryTabViewModel
{
    private TaskCompletionSource<bool>? _parametersPrompt;

    /// <summary>Gets the rows of the parameters panel, one per parameter of the statements about to run.</summary>
    public ObservableCollection<ParameterRowViewModel> ParameterRows { get; } = [];

    /// <summary>Gets whether the parameters panel is shown under the editor, waiting for values.</summary>
    [ObservableProperty]
    public partial bool IsParametersPanelOpen { get; private set; }

    /// <summary>Gets the muted count line of the panel header.</summary>
    public string ParametersHeader => UiText.Format("editor.parametersDetected", ParameterRows.Count);

    /// <summary>Runs with the typed values; disabled while a row has an error.</summary>
    [RelayCommand(CanExecute = nameof(CanRunParameters))]
    private void RunParameters() => _parametersPrompt?.TrySetResult(true);

    private bool CanRunParameters() => ParameterRows.All(r => r.Error is null);

    /// <summary>Closes the parameters panel and runs nothing.</summary>
    [RelayCommand]
    private void IgnoreParameters() => _parametersPrompt?.TrySetResult(false);

    private async Task<Dictionary<string, ParameterValue>?> PromptValuesAsync(IReadOnlyList<string> statements, CancellationToken ct)
    {
        var dialect = _session.Profile.Dialect;
        List<string> names = [];
        foreach (var name in statements.SelectMany(s => ParameterScanner.Scan(s, dialect)))
        {
            if (!names.Contains(name))
                names.Add(name);
        }

        if (names.Count == 0)
            return [];
        ParameterRows.Clear();
        foreach (var name in names)
        {
            _lastValues.TryGetValue(name, out var previous);
            var row = new ParameterRowViewModel(name, previous?.Text, previous?.IsNull ?? false);
            row.PropertyChanged += (_, _) => RunParametersCommand.NotifyCanExecuteChanged();
            ParameterRows.Add(row);
        }

        var prompt = _parametersPrompt = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        OnPropertyChanged(nameof(ParametersHeader));
        RunParametersCommand.NotifyCanExecuteChanged();
        IsParametersPanelOpen = true;
        try
        {
            using var registration = ct.Register(() => prompt.TrySetResult(false));
            if (!await prompt.Task)
                return null;
            var entered = ParameterRows.Select(r => r.ToValue()).ToList();
            foreach (var value in entered)
                _lastValues[value.Name] = value;
            return entered.ToDictionary(v => v.Name);
        }
        finally
        {
            IsParametersPanelOpen = false;
            _parametersPrompt = null;
        }
    }
}
