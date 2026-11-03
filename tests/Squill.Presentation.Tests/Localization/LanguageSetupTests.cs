using System.Globalization;
using Squill.Platform.Settings;
using Squill.Presentation.Localization;
using Squill.Presentation.Settings;
using Xunit;

namespace Squill.Presentation.Tests.Localization;

/// <summary>Collection of the tests that change the process-wide language and cultures, so they never run in parallel with others.</summary>
[CollectionDefinition("Language", DisableParallelization = true)]
public sealed class LanguageGroup;

[Collection("Language")]
public sealed class LanguageSetupTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
    private readonly CultureInfo? _defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? _defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
    private readonly UiLanguage _language = UiText.Language;

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
        CultureInfo.DefaultThreadCurrentCulture = _defaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _defaultUiCulture;
        UiText.Initialize(_language);
    }

    private sealed class MemoryStore(AppLanguage language) : ISettingsStore
    {
        public AppSettings Value { get; private set; } = AppSettings.Default with { Language = language };

        public ValueTask<AppSettings> LoadAsync(CancellationToken ct) => ValueTask.FromResult(Value);

        public ValueTask SaveAsync(AppSettings settings, CancellationToken ct)
        {
            Value = settings;
            return ValueTask.CompletedTask;
        }
    }

    [Theory]
    [InlineData(AppLanguage.French, "en-GB", UiLanguage.French)]
    [InlineData(AppLanguage.English, "fr-FR", UiLanguage.English)]
    [InlineData(AppLanguage.System, "fr-FR", UiLanguage.French)]
    [InlineData(AppLanguage.System, "fr-BE", UiLanguage.French)]
    [InlineData(AppLanguage.System, "en-GB", UiLanguage.English)]
    [InlineData(AppLanguage.System, "de-DE", UiLanguage.English)]
    [InlineData(AppLanguage.System, "", UiLanguage.English)]
    public void Resolve_Table(AppLanguage setting, string culture, UiLanguage expected) =>
        Assert.Equal(expected, LanguageResolver.Resolve(setting, CultureInfo.GetCultureInfo(culture)));

    [Theory]
    [InlineData(AppLanguage.French, null, "fr")]
    [InlineData(AppLanguage.English, null, "en")]
    [InlineData(AppLanguage.System, null, "en")]
    [InlineData(AppLanguage.French, "EN", "en")]
    [InlineData(AppLanguage.English, "fr", "fr")]
    [InlineData(AppLanguage.French, "System", "en")]
    [InlineData(AppLanguage.French, "klingon", "fr")]
    [InlineData(AppLanguage.English, "", "en")]
    public void Apply_ResolvesAndSetsCultures(AppLanguage saved, string? env, string expectedCulture)
    {
        var language = LanguageSetup.Apply(new MemoryStore(saved), env, CultureInfo.GetCultureInfo("de-DE"));

        Assert.Equal(expectedCulture == "fr" ? UiLanguage.French : UiLanguage.English, language);
        Assert.Equal(language, UiText.Language);
        Assert.Equal(expectedCulture, CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
        Assert.Equal(expectedCulture, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        Assert.Equal(UiText.Culture, CultureInfo.DefaultThreadCurrentCulture);
        Assert.Equal(UiText.Culture, CultureInfo.DefaultThreadCurrentUICulture);
    }

    [Fact]
    public void Apply_DoesNotDeadlockWhenTheCallerHasASynchronizationContext()
    {
        var finished = false;
        var thread = new Thread(() =>
        {
            // Like the UI thread at startup: a context whose queue is never pumped while Apply blocks.
            SynchronizationContext.SetSynchronizationContext(new NeverPumpedContext());
            LanguageSetup.Apply(new SlowStore(), null, CultureInfo.GetCultureInfo("de-DE"));
            finished = true;
        });
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Apply must not wait for the caller's synchronization context.");
        Assert.True(finished);
    }

    private sealed class NeverPumpedContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
        }
    }

    private sealed class SlowStore : ISettingsStore
    {
        public async ValueTask<AppSettings> LoadAsync(CancellationToken ct)
        {
            await Task.Delay(20, ct);
            return AppSettings.Default;
        }

        public ValueTask SaveAsync(AppSettings settings, CancellationToken ct) => ValueTask.CompletedTask;
    }

    [Fact]
    public void RestartRequired_FollowsTheSavedChoice()
    {
        UiText.Initialize(UiLanguage.English);
        var vm = new SettingsViewModel(new MemoryStore(AppLanguage.System));
        vm.Language = AppLanguage.English;
        Assert.False(vm.RestartRequired);

        var raised = false;
        vm.PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(SettingsViewModel.RestartRequired);
        vm.Language = AppLanguage.French;

        Assert.True(raised);
        Assert.True(vm.RestartRequired);
        Assert.Equal(UiLanguage.English, vm.ActiveLanguage);
    }

    [Fact]
    public async Task Language_IsLoadedAndSaved()
    {
        UiText.Initialize(UiLanguage.English);
        var store = new MemoryStore(AppLanguage.French);
        var vm = new SettingsViewModel(store);

        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(AppLanguage.French, vm.Language);
        Assert.True(vm.RestartRequired);

        vm.Language = AppLanguage.English;
        await vm.PendingSave;
        Assert.Equal(AppLanguage.English, store.Value.Language);
        Assert.False(vm.RestartRequired);
    }

    [Fact]
    public void LanguageOptions_AreTranslated()
    {
        UiText.Initialize(UiLanguage.French);
        var vm = new SettingsViewModel(new MemoryStore(AppLanguage.System));
        Assert.Equal(["Par défaut du système", "Français", "English"], vm.LanguageOptions.Select(o => o.Label));
    }
}
