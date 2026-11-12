using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Squill.Core;
using Squill.Core.Transactions;
using Squill.Presentation.Localization;
using Squill.Presentation.Safety;

namespace Squill.App.Avalonia.Views;

/// <summary>The question asked when a transaction is still open: Valider, Annuler les modifications or Retour, which Enter and Escape choose.</summary>
internal sealed class TransactionAskWindow : Window
{
    private TransactionChoice _choice = TransactionChoice.Cancel;

    /// <summary>Creates the window for a request.</summary>
    /// <param name="request">The open transaction.</param>
    public TransactionAskWindow(TransactionCloseRequest request)
    {
        Title = request.Title;
        Width = 520;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var mono = this.TryFindResource("SquillFontMono", ActualThemeVariant, out var font) && font is FontFamily family ? family : FontFamily.Default;
        IBrush? Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;
        Background = Brush("SquillBgRaisedBrush");

        var body = new StackPanel { Spacing = 12, Margin = new Thickness(24, 20, 24, 8) };
        body.Children.Add(new TextBlock { Text = request.Title, FontSize = 20, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFgBrush") });
        body.Children.Add(new TextBlock { Text = request.Message, TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFg2Brush") });
        if (request.Statements.Count > 0)
        {
            var list = new StackPanel { Spacing = 6 };
            foreach (var statement in request.Statements.Take(5))
                list.Children.Add(new TextBlock { Text = statement, FontFamily = mono, FontSize = 12, MaxLines = 3, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFgBrush") });
            if (request.PendingCount > 5)
                list.Children.Add(new TextBlock { Text = UiText.Format("prod.more", request.PendingCount - 5), FontSize = 12, Foreground = Brush("SquillFg3Brush") });
            body.Children.Add(new Border { Background = Brush("SquillBgBaseBrush"), Padding = new Thickness(12, 10), Child = list });
        }

        Button Make(string textKey, string? style, TransactionChoice result)
        {
            var button = new Button { Content = UiText.Get(textKey), Height = 30, MinHeight = 30 };
            if (style is not null)
                button.Classes.Add(style);
            button.Click += (_, _) =>
            {
                _choice = result;
                Close();
            };
            return button;
        }

        Commit = Make("tx.ask.commit", "outline-success", TransactionChoice.Commit);
        Commit.IsVisible = request.State != TransactionState.Failed;
        Rollback = Make("tx.ask.rollback", "outline-danger", TransactionChoice.Rollback);
        Back = Make("tx.ask.back", null, TransactionChoice.Cancel);
        Back.IsDefault = true;
        Back.IsCancel = true;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(24, 12, 24, 20), Children = { Commit, Rollback, Back } };

        var key = request.Environment switch
        {
            ConnectionEnvironment.Production => "SquillEnvProdBrush",
            ConnectionEnvironment.Staging => "SquillEnvStagingBrush",
            ConnectionEnvironment.Development => "SquillEnvDevBrush",
            ConnectionEnvironment.Local => "SquillEnvLocalBrush",
            _ => null,
        };
        var stripe = new Border { Height = 3, Background = key is null ? null : Brush(key) };

        // Retour is the default and the cancel answer: Enter and Escape choose it whatever has the focus.
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key is not (Key.Enter or Key.Escape))
                return;
            e.Handled = true;
            Close();
        }, RoutingStrategies.Tunnel);
        Opened += (_, _) => Back.Focus();
        Content = new StackPanel { Children = { stripe, body, buttons } };
    }

    /// <summary>Gets the Valider button, hidden when the transaction failed.</summary>
    public Button Commit { get; }

    /// <summary>Gets the Annuler les modifications button.</summary>
    public Button Rollback { get; }

    /// <summary>Gets the Retour button, the default and cancel one.</summary>
    public Button Back { get; }

    /// <summary>Gets the choice once the window is closed; <see cref="TransactionChoice.Cancel"/> when it was dismissed.</summary>
    public TransactionChoice Choice => _choice;

    /// <summary>Shows the window over another one.</summary>
    /// <param name="owner">The window that owns the dialog.</param>
    /// <param name="request">The open transaction.</param>
    /// <returns>The choice of the user.</returns>
    public static async Task<TransactionChoice> ShowAsync(Window owner, TransactionCloseRequest request)
    {
        var window = new TransactionAskWindow(request);
        await window.ShowDialog(owner);
        return window.Choice;
    }
}
