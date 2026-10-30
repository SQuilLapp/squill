using Squill.Core;
using Squill.Platform.Settings;
using Squill.Presentation.Settings;
using Xunit;

namespace Squill.Presentation.Tests.Settings;

public sealed class SettingsViewModelTests
{
    private sealed class MemoryStore(AppSettings initial) : ISettingsStore
    {
        public AppSettings Value { get; private set; } = initial;
        public int Saves { get; private set; }

        public ValueTask<AppSettings> LoadAsync(CancellationToken ct) => ValueTask.FromResult(Value);

        public ValueTask SaveAsync(AppSettings settings, CancellationToken ct)
        {
            Value = settings;
            Saves++;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task LoadAsync_StoredSettings_ExposesThemWithoutSaving()
    {
        var store = new MemoryStore(new(ThemeMode.Dark, 20, 5_000, KeywordCase.Lower));
        var vm = new SettingsViewModel(store);
        var themeEvents = 0;
        vm.ThemeChanged += (_, _) => themeEvents++;

        await vm.LoadCommand.ExecuteAsync(null);
        await vm.PendingSave;

        Assert.Equal(new(ThemeMode.Dark, 20, 5_000, KeywordCase.Lower), vm.Current);
        Assert.Equal(0, store.Saves);
        Assert.Equal(1, themeEvents);
    }

    [Fact]
    public async Task Change_AfterLoad_SavesAndRaisesThemeChanged()
    {
        var store = new MemoryStore(AppSettings.Default);
        var vm = new SettingsViewModel(store);
        await vm.LoadCommand.ExecuteAsync(null);
        var themeEvents = 0;
        vm.ThemeChanged += (_, _) => themeEvents++;

        vm.Theme = ThemeMode.Light;
        vm.FormatterKeywordCase = KeywordCase.Lower;
        await vm.PendingSave;

        Assert.Equal(ThemeMode.Light, store.Value.Theme);
        Assert.Equal(KeywordCase.Lower, store.Value.FormatterKeywordCase);
        Assert.Equal(1, themeEvents);
    }

    [Fact]
    public async Task Change_OutOfRange_IsClampedInViewModelAndStore()
    {
        var store = new MemoryStore(AppSettings.Default);
        var vm = new SettingsViewModel(store);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.EditorFontSize = 100;
        vm.MaxGridRows = 5;
        await vm.PendingSave;

        Assert.Equal(32, vm.EditorFontSize);
        Assert.Equal(1_000, vm.MaxGridRows);
        Assert.Equal(32, store.Value.EditorFontSize);
        Assert.Equal(1_000, store.Value.MaxGridRows);
    }

    [Fact]
    public void Change_BeforeLoad_DoesNotSave()
    {
        var store = new MemoryStore(AppSettings.Default);
        var vm = new SettingsViewModel(store) { Theme = ThemeMode.Dark };

        Assert.Equal(0, store.Saves);
    }
}
