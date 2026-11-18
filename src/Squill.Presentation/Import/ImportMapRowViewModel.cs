using CommunityToolkit.Mvvm.ComponentModel;

namespace Squill.Presentation.Import;

/// <summary>One row of the mapping step: a CSV column, its sample values, the table column it feeds and what the check found.</summary>
public sealed partial class ImportMapRowViewModel : ViewModelBase
{
    private readonly Action _changed;

    internal ImportMapRowViewModel(int sourceIndex, string sourceName, IReadOnlyList<string> preview, Action changed)
    {
        SourceIndex = sourceIndex;
        SourceName = sourceName;
        Preview = preview;
        _changed = changed;
    }

    /// <summary>Gets the zero-based index of the CSV field.</summary>
    public int SourceIndex { get; }

    /// <summary>Gets the CSV header text.</summary>
    public string SourceName { get; }

    /// <summary>Gets the first three values of the column.</summary>
    public IReadOnlyList<string> Preview { get; }

    /// <summary>Gets or sets whether the column is imported; a column without target never is.</summary>
    [ObservableProperty]
    public partial bool Enabled { get; set; }

    /// <summary>Gets or sets the chosen target as <c>name . type</c>, or <see langword="null"/> for <c>(ignorer)</c>.</summary>
    [ObservableProperty]
    public partial string? Target { get; set; }

    /// <summary>Gets the conversion text, for example <c>Texte -&gt; bigint</c>; empty when ignored.</summary>
    [ObservableProperty]
    public partial string Conversion { get; internal set; } = "";

    /// <summary>Gets the status text: ok, the first failing example, or ignored.</summary>
    [ObservableProperty]
    public partial string Status { get; internal set; } = "";

    /// <summary>Gets whether the sample check found rows that do not convert.</summary>
    [ObservableProperty]
    public partial bool IsWarning { get; internal set; }

    partial void OnEnabledChanged(bool value) => _changed();

    partial void OnTargetChanged(string? value)
    {
        Enabled = value is not null; // choosing a target turns the column on, (ignorer) turns it off
        _changed();
    }
}
