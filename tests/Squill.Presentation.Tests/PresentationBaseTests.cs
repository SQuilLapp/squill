using Xunit;

namespace Squill.Presentation.Tests;

public sealed class PresentationBaseTests
{
    [Fact]
    public void Property_Changed_RaisesPropertyChanged()
    {
        var vm = new TestViewModel();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.Name = "x";

        Assert.Equal([nameof(TestViewModel.Name)], raised);
    }

    [Fact]
    public async Task RelayCommand_Executed_RunsAsync()
    {
        var vm = new TestViewModel();

        await vm.RunCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.Runs);
    }

    [Fact]
    public void ImmediateDispatcher_Post_RunsInline()
    {
        var dispatcher = new ImmediateDispatcher();
        var ran = false;

        dispatcher.Post(() => ran = true);

        Assert.True(dispatcher.CheckAccess());
        Assert.True(ran);
    }

    [Fact]
    public async Task FakeDialogService_Prompt_ReturnsInitialByDefault()
    {
        var dialogs = new FakeDialogService();

        Assert.Equal("a", await dialogs.PromptAsync("t", "m", "a"));
        await dialogs.ShowErrorAsync("t", "boom");
        Assert.Single(dialogs.Errors);
    }
}
