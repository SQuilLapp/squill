using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Squill.Core.Transactions;
using Squill.Presentation.Localization;
using Squill.Presentation.Safety;

namespace Squill.App.WinUI.Views;

/// <summary>The question asked when a transaction is still open: <c>Valider</c>, <c>Annuler les modifications</c> or <c>Retour</c>, which <c>Entrée</c> and <c>Échap</c> choose.</summary>
internal static class TransactionAskDialog
{
    /// <summary>Shows the question over a window.</summary>
    /// <param name="root">The content of the window that owns the dialog.</param>
    /// <param name="request">The open transaction.</param>
    /// <returns>The choice of the user; <see cref="TransactionChoice.Cancel"/> when the dialog was dismissed.</returns>
    public static async Task<TransactionChoice> ShowAsync(FrameworkElement root, TransactionCloseRequest request)
    {
        Microsoft.UI.Xaml.Media.Brush Brush(string key) => Tokens.Brush(root, key);
        var mono = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SquillFontMono"];
        var choice = TransactionChoice.Cancel;
        var dialog = new ContentDialog { XamlRoot = root.XamlRoot, RequestedTheme = root.ActualTheme, Padding = new Thickness(0), Background = Brush("SquillBgRaisedBrush") };
        dialog.Resources["ContentDialogMaxWidth"] = 520.0;
        dialog.Resources["ContentDialogMinWidth"] = 520.0;

        var body = new StackPanel { Spacing = 12, Padding = new Thickness(24, 20, 24, 8) };
        body.Children.Add(new TextBlock { Text = request.Title, FontSize = 20, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFgBrush") });
        body.Children.Add(new TextBlock { Text = request.Message, TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFg2Brush") });
        if (request.Statements.Count > 0)
        {
            var list = new StackPanel { Spacing = 6 };
            foreach (var statement in request.Statements.Take(5))
                list.Children.Add(new TextBlock { Text = statement, FontFamily = mono, FontSize = 12, MaxLines = 3, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFgBrush") });
            if (request.PendingCount > 5)
                list.Children.Add(new TextBlock { Text = UiText.Format("prod.more", request.PendingCount - 5), FontSize = 12, Foreground = Brush("SquillFg3Brush") });
            body.Children.Add(new Border { Background = Brush("SquillBgBaseBrush"), Padding = new Thickness(12, 10, 12, 10), Child = list });
        }

        Button Make(string textKey, string? style, TransactionChoice result)
        {
            var button = new Button { Content = UiText.Get(textKey), Height = 30, MinHeight = 30 };
            if (style is not null)
                button.Style = (Style)Application.Current.Resources[style];
            button.Click += (_, _) =>
            {
                choice = result;
                dialog.Hide();
            };
            return button;
        }

        var back = Make("tx.ask.back", null, TransactionChoice.Cancel);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(24, 12, 24, 20) };
        if (request.State != TransactionState.Failed)
            buttons.Children.Add(Make("tx.ask.commit", "OutlineSuccessButtonStyle", TransactionChoice.Commit));
        buttons.Children.Add(Make("tx.ask.rollback", "OutlineDangerButtonStyle", TransactionChoice.Rollback));
        buttons.Children.Add(back);

        // Retour is the default and the cancel answer: Entrée and Échap choose it whatever has the focus.
        dialog.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                dialog.Hide();
            }
        };
        dialog.Opened += (_, _) => root.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => back.Focus(FocusState.Keyboard));
        var stripe = new Border { Height = 3 };
        if (ChromeFormat.Environment(request.Environment) is { } look)
            stripe.Background = Brush($"SquillEnv{look.Suffix}Brush");
        dialog.Content = new StackPanel { Children = { stripe, body, buttons } };
        await dialog.ShowAsync();
        return choice;
    }
}
