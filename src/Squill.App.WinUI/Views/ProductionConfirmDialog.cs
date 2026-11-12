using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Squill.Presentation.Localization;
using Squill.Presentation.Safety;

namespace Squill.App.WinUI.Views;

/// <summary>The production confirmation of mockup M15: red header, statements, optional typed word, transaction option; <c>Entrée</c> and <c>Échap</c> cancel.</summary>
internal static class ProductionConfirmDialog
{
    /// <summary>Shows the dialog over a window.</summary>
    /// <param name="root">The content of the window that owns the dialog.</param>
    /// <param name="request">What is about to run.</param>
    /// <returns>The answer; not confirmed when the dialog was cancelled or dismissed.</returns>
    public static async Task<ProductionConfirmationResult> ShowAsync(FrameworkElement root, ProductionConfirmationRequest request)
    {
        Microsoft.UI.Xaml.Media.Brush Brush(string key) => Tokens.Brush(root, key);
        var mono = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SquillFontMono"];
        var confirmed = false;
        var dialog = new ContentDialog { XamlRoot = root.XamlRoot, RequestedTheme = root.ActualTheme, Padding = new Thickness(0), Background = Brush("SquillBgRaisedBrush") };
        dialog.Resources["ContentDialogMaxWidth"] = 560.0;
        dialog.Resources["ContentDialogMinWidth"] = 560.0;

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        header.Children.Add(Icons.Create("SquillIconWarning", 16, Brush("SquillOnEnvProdBrush")));
        header.Children.Add(new TextBlock { Text = UiText.Format("prod.header", request.ConnectionName, request.Database), Foreground = Brush("SquillOnEnvProdBrush"), FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
        var band = new Border { Background = Brush("SquillDangerBrush"), Padding = new Thickness(24, 12, 24, 12), Child = header };

        var body = new StackPanel { Spacing = 14, Padding = new Thickness(24, 20, 24, 8) };
        body.Children.Add(new TextBlock { Text = request.Title, FontSize = 20, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFgBrush") });
        body.Children.Add(Message(request, Brush, mono));
        body.Children.Add(Statements(request, Brush, mono));

        TextBox? typed = null;
        if (request.RequiredText is { } word)
        {
            var parts = UiText.Get("prod.typePrompt").Split("{0}");
            var label = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFg2Brush") };
            label.Inlines.Add(new Run { Text = parts[0] });
            label.Inlines.Add(new Run { Text = word, FontFamily = mono, FontWeight = FontWeights.Bold, Foreground = Brush("SquillFgBrush") });
            if (parts.Length > 1)
                label.Inlines.Add(new Run { Text = parts[1] });
            typed = new TextBox { PlaceholderText = word, FontFamily = mono };
            body.Children.Add(new StackPanel { Spacing = 6, Children = { label, typed } });
        }

        CheckBox? option = null;
        if (request.ShowTransactionOption)
        {
            option = new CheckBox { Content = UiText.Get("prod.option"), IsChecked = true, IsEnabled = !request.TransactionOptionLocked };
            body.Children.Add(option);
        }

        // The confirm button is never the default one: it only reacts to a click, and Entrée anywhere in the dialog is Annuler.
        var confirm = new Button { Style = (Style)Application.Current.Resources["VariantButtonStyle"], Content = request.ConfirmLabel, Background = Brush("SquillDangerBrush"), BorderBrush = Brush("SquillDangerBrush"), Foreground = Brush("SquillOnEnvProdBrush"), Height = 30, MinHeight = 30, IsEnabled = typed is null };
        var cancel = new Button { Content = UiText.Get("dialog.cancel"), Height = 30, MinHeight = 30 };
        confirm.Click += (_, _) =>
        {
            confirmed = true;
            dialog.Hide();
        };
        cancel.Click += (_, _) => dialog.Hide();
        if (typed is not null)
            typed.TextChanged += (_, _) => confirm.IsEnabled = ProductionGuard.Matches(typed.Text, request.RequiredText!);

        var footer = new Grid { Padding = new Thickness(24, 12, 24, 20), ColumnSpacing = 8, ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Auto } } };
        footer.Children.Add(new TextBlock { Text = UiText.Get("prod.hint"), Foreground = Brush("SquillFg3Brush"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(confirm, 1);
        Grid.SetColumn(cancel, 2);
        footer.Children.Add(confirm);
        footer.Children.Add(cancel);

        dialog.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                dialog.Hide();
            }
        };
        // The dialog moves the focus to its first button after it opened: take it back on the next turn.
        dialog.Opened += (_, _) => root.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => (typed is null ? (Control)cancel : typed).Focus(FocusState.Keyboard));
        dialog.Content = new StackPanel { Children = { band, body, footer } };
        await dialog.ShowAsync();
        return new ProductionConfirmationResult(confirmed, confirmed && option?.IsChecked == true);
    }

    // The figure keeps the danger color only when the formatted number is found exactly once in the message; otherwise the text stays plain.
    private static TextBlock Message(ProductionConfirmationRequest request, Func<string, Microsoft.UI.Xaml.Media.Brush> brush, Microsoft.UI.Xaml.Media.FontFamily mono)
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = brush("SquillFg2Brush") };
        var figure = request.EstimatedRows is { } rows ? rows.ToString("N0", UiText.Culture) : null;
        var at = figure is null ? -1 : request.Message.IndexOf(figure, StringComparison.Ordinal);
        if (figure is null || at < 0 || request.Message.IndexOf(figure, at + 1, StringComparison.Ordinal) >= 0)
        {
            text.Text = request.Message;
            return text;
        }

        text.Inlines.Add(new Run { Text = request.Message[..at] });
        text.Inlines.Add(new Run { Text = figure, FontFamily = mono, FontWeight = FontWeights.Bold, Foreground = brush("SquillDangerBrush") });
        text.Inlines.Add(new Run { Text = request.Message[(at + figure.Length)..] });
        return text;
    }

    private static Border Statements(ProductionConfirmationRequest request, Func<string, Microsoft.UI.Xaml.Media.Brush> brush, Microsoft.UI.Xaml.Media.FontFamily mono)
    {
        var list = new StackPanel { Spacing = 6 };
        // The request already holds at most five statements and the localized "+ N others" line.
        foreach (var statement in request.Statements)
            list.Children.Add(new TextBlock { Text = statement.Text, FontFamily = mono, FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxLines = 4, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = brush("SquillFgBrush") });
        return new Border { Background = brush("SquillBgBaseBrush"), BorderBrush = brush("SquillDangerBrush"), BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(12, 10, 12, 10), Child = list };
    }
}
