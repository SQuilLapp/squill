using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Platform.Settings;

namespace Squill.Presentation.Settings;

/// <summary>Exposes the user preferences, clamps edits to their allowed range and saves them on every change once loaded.</summary>
/// <param name="store">Where the settings are read and saved.</param>
public sealed partial class SettingsViewModel(ISettingsStore store) : ViewModelBase
{
    private bool _loaded;

    /// <summary>Raised after <see cref="Theme"/> changed, so the head can switch the theme variant.</summary>
    public event EventHandler? ThemeChanged;

    /// <summary>Gets the task of the latest save; it never faults, failures are reported by <see cref="SaveError"/>.</summary>
    public Task PendingSave { get; private set; } = Task.CompletedTask;

    /// <summary>Gets the message of the last failed save, or <see langword="null"/> when the last save succeeded.</summary>
    [ObservableProperty]
    public partial string? SaveError { get; private set; }

    /// <summary>Gets or sets the requested color theme.</summary>
    [ObservableProperty]
    public partial ThemeMode Theme { get; set; } = AppSettings.Default.Theme;

    /// <summary>Gets or sets the editor font size in points, clamped to the allowed range.</summary>
    [ObservableProperty]
    public partial double EditorFontSize { get; set; } = AppSettings.Default.EditorFontSize;

    /// <summary>Gets or sets the maximum number of rows kept in a result grid, clamped to the allowed range.</summary>
    [ObservableProperty]
    public partial int MaxGridRows { get; set; } = AppSettings.Default.MaxGridRows;

    /// <summary>Gets or sets the keyword casing applied by the formatter.</summary>
    [ObservableProperty]
    public partial KeywordCase FormatterKeywordCase { get; set; } = AppSettings.Default.FormatterKeywordCase;

    /// <summary>Gets the current values as a settings record.</summary>
    public AppSettings Current => new(Theme, EditorFontSize, MaxGridRows, FormatterKeywordCase);

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        var settings = await store.LoadAsync(ct);
        _loaded = false;
        Theme = settings.Theme;
        EditorFontSize = settings.EditorFontSize;
        MaxGridRows = settings.MaxGridRows;
        FormatterKeywordCase = settings.FormatterKeywordCase;
        _loaded = true;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnThemeChanged(ThemeMode value)
    {
        if (_loaded)
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        Save();
    }

    partial void OnEditorFontSizeChanged(double value)
    {
        var clamped = Current.Clamp().EditorFontSize;
        if (!clamped.Equals(value))
            EditorFontSize = clamped;
        else
            Save();
    }

    partial void OnMaxGridRowsChanged(int value)
    {
        var clamped = Current.Clamp().MaxGridRows;
        if (!clamped.Equals(value))
            MaxGridRows = clamped;
        else
            Save();
    }

    partial void OnFormatterKeywordCaseChanged(KeywordCase value) => Save();

    private void Save()
    {
        if (_loaded)
            PendingSave = SaveAfterAsync(PendingSave, Current);
    }

    private async Task SaveAfterAsync(Task previous, AppSettings settings)
    {
        await previous;
        try
        {
            await store.SaveAsync(settings, CancellationToken.None);
            SaveError = null;
        }
        catch (Exception ex)
        {
            SaveError = ex.Message;
        }
    }
}
