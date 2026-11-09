using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using System.Globalization;
using Squill.Platform.Settings;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Settings;

/// <summary>One entry of the language picker.</summary>
/// <param name="Value">The setting it selects.</param>
/// <param name="Label">Its text in the active language.</param>
public sealed record LanguageOption(AppLanguage Value, string Label);

/// <summary>One entry of the update frequency picker.</summary>
/// <param name="Value">The setting it selects.</param>
/// <param name="Label">Its text in the active language.</param>
public sealed record FrequencyOption(UpdateCheckFrequency Value, string Label);

/// <summary>One entry of the update channel picker.</summary>
/// <param name="Value">The channel it selects.</param>
/// <param name="Label">Its text in the active language.</param>
public sealed record ChannelOption(UpdateChannel Value, string Label);

/// <summary>Exposes the user preferences, clamps edits to their allowed range and saves them on every change once loaded.</summary>
/// <param name="store">Where the settings are read and saved.</param>
/// <param name="currentVersion">Version of the running build, used for the downgrade note; <see langword="null"/> uses <see cref="AppVersion.Current"/>.</param>
public sealed partial class SettingsViewModel(ISettingsStore store, string? currentVersion = null) : ViewModelBase
{
    private bool _loaded;
    private bool _frequencyUnchosen = true;

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

    /// <summary>Gets or sets the requested display language; it applies after a restart.</summary>
    [ObservableProperty]
    public partial AppLanguage Language { get; set; } = AppSettings.Default.Language;

    /// <summary>Gets or sets how often SQuilL checks for updates; a stored "not asked" shows as <see cref="UpdateCheckFrequency.Never"/> and stays unsaved until the user picks a value.</summary>
    [ObservableProperty]
    public partial UpdateCheckFrequency UpdateFrequency { get; set; } = UpdateCheckFrequency.Never;

    /// <summary>Gets or sets the release channel followed for updates.</summary>
    [ObservableProperty]
    public partial UpdateChannel UpdateChannel { get; set; } = AppSettings.Default.UpdateChannel;

    /// <summary>Gets the frequencies the picker offers.</summary>
    public IReadOnlyList<FrequencyOption> FrequencyOptions { get; } =
    [
        new(UpdateCheckFrequency.Never, UiText.Get("settings.update.frequency.never")),
        new(UpdateCheckFrequency.Startup, UiText.Get("settings.update.frequency.startup")),
        new(UpdateCheckFrequency.Daily, UiText.Get("settings.update.frequency.daily")),
        new(UpdateCheckFrequency.Weekly, UiText.Get("settings.update.frequency.weekly")),
    ];

    /// <summary>Gets the channels the picker offers.</summary>
    public IReadOnlyList<ChannelOption> ChannelOptions { get; } =
    [
        new(UpdateChannel.Stable, UiText.Get("settings.update.channel.stable")),
        new(UpdateChannel.Beta, UiText.Get("settings.update.channel.beta")),
    ];

    /// <summary>Gets a value indicating whether the "no downgrade" note applies: the stable channel is chosen while a pre-release runs.</summary>
    public bool ShowNoDowngradeNote =>
        UpdateChannel == UpdateChannel.Stable
        && SemanticVersion.Parse(currentVersion ?? AppVersion.Current, CultureInfo.InvariantCulture).IsPreRelease;

    /// <summary>Gets the language in effect since startup.</summary>
    public UiLanguage ActiveLanguage { get; } = UiText.Language;

    /// <summary>Gets the language choices, labelled in the active language.</summary>
    public IReadOnlyList<LanguageOption> LanguageOptions { get; } =
    [
        new(AppLanguage.System, UiText.Get("settings.language.system")),
        new(AppLanguage.French, UiText.Get("settings.language.french")),
        new(AppLanguage.English, UiText.Get("settings.language.english")),
    ];

    /// <summary>Gets a value indicating whether the chosen language differs from the active one; compares the saved choice only, so the <c>SQUILL_LANGUAGE</c> test override is not taken into account.</summary>
    public bool RestartRequired => LanguageResolver.Resolve(Language, CultureInfo.CurrentUICulture) != ActiveLanguage;

    /// <summary>Gets the current values as a settings record.</summary>
    public AppSettings Current => new(
        Theme,
        EditorFontSize,
        MaxGridRows,
        FormatterKeywordCase,
        Language,
        _frequencyUnchosen ? UpdateCheckFrequency.NotAsked : UpdateFrequency,
        UpdateChannel);

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        var settings = await store.LoadAsync(ct);
        _loaded = false;
        Theme = settings.Theme;
        EditorFontSize = settings.EditorFontSize;
        MaxGridRows = settings.MaxGridRows;
        FormatterKeywordCase = settings.FormatterKeywordCase;
        Language = settings.Language;
        _frequencyUnchosen = settings.UpdateFrequency == UpdateCheckFrequency.NotAsked;
        UpdateFrequency = _frequencyUnchosen ? UpdateCheckFrequency.Never : settings.UpdateFrequency;
        UpdateChannel = settings.UpdateChannel;
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
        if (Math.Abs(clamped - value) > 0.001)
            EditorFontSize = clamped;
        else
            Save();
    }

    partial void OnMaxGridRowsChanged(int value)
    {
        var clamped = Current.Clamp().MaxGridRows;
        if (Math.Abs(clamped - value) > 0.001)
            MaxGridRows = clamped;
        else
            Save();
    }

    partial void OnFormatterKeywordCaseChanged(KeywordCase value) => Save();

    partial void OnLanguageChanged(AppLanguage value)
    {
        OnPropertyChanged(nameof(RestartRequired));
        Save();
    }

    partial void OnUpdateFrequencyChanged(UpdateCheckFrequency value)
    {
        if (_loaded)
        {
            _frequencyUnchosen = false;
        }

        Save();
    }

    partial void OnUpdateChannelChanged(UpdateChannel value)
    {
        OnPropertyChanged(nameof(ShowNoDowngradeNote));
        Save();
    }

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
            SaveError = UiText.Format("settings.saveError", ex.Message);
        }
    }
}
