using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Squill.Presentation.Tests;

/// <summary>Minimal view-model proving the toolkit source generators work under warnings-as-errors.</summary>
internal sealed partial class TestViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Name { get; set; } = "";

    public int Runs { get; private set; }

    [RelayCommand]
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        Runs++;
    }
}
