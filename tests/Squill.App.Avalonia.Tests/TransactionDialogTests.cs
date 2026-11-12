using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Core.Transactions;
using Squill.Presentation.Safety;
using Squill.Sql.Transactions;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class TransactionDialogTests
{
    private static ProductionConfirmationRequest Request(string? word, bool option = false, bool locked = false) =>
        new(ConfirmationKind.Run, "pg-prod", "ventes", [new ConfirmedStatement("DELETE FROM t", "t", StatementKind.Delete)], null, word, option, locked, "Title", "Message", "Delete");

    private static async Task OnWindowAsync(Func<ProductionConfirmWindow> create, Func<ProductionConfirmWindow, Task> body) =>
        await Headless.Session.Dispatch(async () =>
        {
            var window = create();
            window.Show();
            try
            {
                await body(window);
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken);

    private static void Press(Control target, Key key) =>
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = target });

    [Fact]
    public Task TypedWord_GatesTheConfirmButton_WithoutCase() => OnWindowAsync(() => new ProductionConfirmWindow(Request("ventes")), window =>
    {
        Assert.False(window.Confirm.IsEnabled);
        window.TypedBox!.Text = "vent";
        Assert.False(window.Confirm.IsEnabled);
        window.TypedBox.Text = "VENTES";
        Assert.True(window.Confirm.IsEnabled);
        window.TypedBox.Text = "ventes2";
        Assert.False(window.Confirm.IsEnabled);
        Assert.False(window.Confirm.IsDefault);
        Assert.False(window.Confirm.IsCancel);
        Assert.True(window.Cancel.IsDefault);
        Assert.True(window.Cancel.IsCancel);
        return Task.CompletedTask;
    });

    [Fact]
    public Task NoRequiredWord_LeavesTheConfirmButtonEnabled() => Headless.Session.Dispatch(() =>
    {
        var window = new ProductionConfirmWindow(Request(null));
        Assert.Null(window.TypedBox);
        Assert.True(window.Confirm.IsEnabled);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Enter_Cancels_EvenWithAMatchingWord() => OnWindowAsync(() => new ProductionConfirmWindow(Request("ventes")), window =>
    {
        window.TypedBox!.Text = "ventes";
        Assert.True(window.Confirm.IsEnabled);
        Press(window.TypedBox, Key.Enter);
        Assert.False(window.Result.Confirmed);
        Assert.False(window.IsVisible);
        return Task.CompletedTask;
    });

    [Fact]
    public Task ConfirmClick_ReturnsTheTransactionChoice_OfALockedCheckedBox() => OnWindowAsync(() => new ProductionConfirmWindow(Request(null, option: true, locked: true)), window =>
    {
        Assert.True(window.OptionBox!.IsChecked);
        Assert.False(window.OptionBox.IsEnabled);
        window.Confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(new ProductionConfirmationResult(true, true), window.Result);
        return Task.CompletedTask;
    });

    [Fact]
    public Task ConfirmClick_WithTheBoxUnchecked_DoesNotUseATransaction() => OnWindowAsync(() => new ProductionConfirmWindow(Request(null, option: true)), window =>
    {
        window.OptionBox!.IsChecked = false;
        window.Confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(new ProductionConfirmationResult(true, false), window.Result);
        return Task.CompletedTask;
    });

    private static TransactionCloseRequest Ask(TransactionState state) =>
        new("c", ConnectionEnvironment.Production, "db", state, 2, ["INSERT INTO t VALUES (1)"], "Title", "Message");

    [Theory]
    [InlineData(TransactionState.Open, true)]
    [InlineData(TransactionState.Failed, false)]
    public Task TransactionQuestion_HidesValider_WhenTheTransactionFailed(TransactionState state, bool visible) => Headless.Session.Dispatch(() =>
    {
        var window = new TransactionAskWindow(Ask(state));
        Assert.Equal(visible, window.Commit.IsVisible);
        Assert.True(window.Back.IsDefault);
        Assert.True(window.Back.IsCancel);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Escape)]
    public Task TransactionQuestion_EnterAndEscapeChooseRetour(Key key) => Headless.Session.Dispatch(() =>
    {
        var window = new TransactionAskWindow(Ask(TransactionState.Open));
        window.Show();
        Press(window.Rollback, key);
        Assert.Equal(TransactionChoice.Cancel, window.Choice);
        Assert.False(window.IsVisible);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TransactionQuestion_ButtonsReturnTheirChoice() => Headless.Session.Dispatch(() =>
    {
        var commit = new TransactionAskWindow(Ask(TransactionState.Open));
        commit.Show();
        commit.Commit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(TransactionChoice.Commit, commit.Choice);

        var rollback = new TransactionAskWindow(Ask(TransactionState.Open));
        rollback.Show();
        rollback.Rollback.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(TransactionChoice.Rollback, rollback.Choice);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);
}
