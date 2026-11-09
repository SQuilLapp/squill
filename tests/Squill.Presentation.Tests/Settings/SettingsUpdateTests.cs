using Squill.Core;
using Squill.Platform.Settings;
using Squill.Presentation.Localization;
using Squill.Presentation.Settings;
using Squill.Tests.Shared;
using Xunit;

namespace Squill.Presentation.Tests.Settings;

public sealed class SettingsUpdateTests
{
    private readonly FakeSettingsStore _store = new();

    [Theory]
    [InlineData(UiLanguage.French, "Jamais", "Chaque semaine", "Stable")]
    [InlineData(UiLanguage.English, "Never", "Every week", "Stable")]
    public void Options_AreLabelledInTheActiveLanguage(UiLanguage language, string never, string weekly, string stable)
    {
        using var scope = UiText.Override(language);

        var vm = new SettingsViewModel(_store);

        Assert.Equal(
            [UpdateCheckFrequency.Never, UpdateCheckFrequency.Startup, UpdateCheckFrequency.Daily, UpdateCheckFrequency.Weekly],
            vm.FrequencyOptions.Select(o => o.Value));
        Assert.Equal(never, vm.FrequencyOptions[0].Label);
        Assert.Equal(weekly, vm.FrequencyOptions[3].Label);
        Assert.Equal([UpdateChannel.Stable, UpdateChannel.Beta], vm.ChannelOptions.Select(o => o.Value));
        Assert.Equal(stable, vm.ChannelOptions[0].Label);
        Assert.Contains("Beta", vm.ChannelOptions[1].Label, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Load_NotAsked_ShowsNeverAndDoesNotSave()
    {
        var vm = new SettingsViewModel(_store);

        await vm.LoadCommand.ExecuteAsync(null);
        await vm.PendingSave;

        Assert.Equal(UpdateCheckFrequency.Never, vm.UpdateFrequency);
        Assert.Equal(UpdateCheckFrequency.NotAsked, vm.Current.UpdateFrequency);
        Assert.Equal(UpdateCheckFrequency.NotAsked, _store.Current.UpdateFrequency);
    }

    [Fact]
    public async Task Change_OfAnotherSetting_KeepsNotAsked()
    {
        var vm = new SettingsViewModel(_store);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.EditorFontSize = 20;
        await vm.PendingSave;

        Assert.Equal(20, _store.Current.EditorFontSize);
        Assert.Equal(UpdateCheckFrequency.NotAsked, _store.Current.UpdateFrequency);
    }

    [Fact]
    public async Task Change_FrequencyAndChannel_SavesAndReloads()
    {
        var vm = new SettingsViewModel(_store);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.UpdateFrequency = UpdateCheckFrequency.Daily;
        vm.UpdateChannel = UpdateChannel.Beta;
        await vm.PendingSave;

        Assert.Equal(UpdateCheckFrequency.Daily, _store.Current.UpdateFrequency);
        Assert.Equal(UpdateChannel.Beta, _store.Current.UpdateChannel);

        var reloaded = new SettingsViewModel(_store);
        await reloaded.LoadCommand.ExecuteAsync(null);
        Assert.Equal(UpdateCheckFrequency.Daily, reloaded.UpdateFrequency);
        Assert.Equal(UpdateChannel.Beta, reloaded.UpdateChannel);
        Assert.Equal(_store.Current, reloaded.Current);
    }

    [Fact]
    public async Task Choosing_NeverExplicitly_IsSavedAsNever()
    {
        var vm = new SettingsViewModel(_store);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.UpdateFrequency = UpdateCheckFrequency.Weekly;
        vm.UpdateFrequency = UpdateCheckFrequency.Never;
        await vm.PendingSave;

        Assert.Equal(UpdateCheckFrequency.Never, _store.Current.UpdateFrequency);
    }

    [Theory]
    [InlineData("1.0.0-beta.2", UpdateChannel.Stable, true)]
    [InlineData("1.0.0-beta.2", UpdateChannel.Beta, false)]
    [InlineData("1.0.0", UpdateChannel.Stable, false)]
    public void NoDowngradeNote_OnlyOnStableWhileRunningAPreRelease(string version, UpdateChannel channel, bool expected)
    {
        var vm = new SettingsViewModel(_store, version) { UpdateChannel = channel };

        Assert.Equal(expected, vm.ShowNoDowngradeNote);
    }

    [Fact]
    public void ChannelChange_RaisesTheNoteNotification()
    {
        var vm = new SettingsViewModel(_store, "1.0.0-beta.2") { UpdateChannel = UpdateChannel.Beta };
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.UpdateChannel = UpdateChannel.Stable;

        Assert.Contains(nameof(SettingsViewModel.ShowNoDowngradeNote), raised);
    }
}
