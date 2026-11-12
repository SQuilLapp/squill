using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Squill.Presentation.Localization;
using Squill.Presentation.Safety;

namespace Squill.App.Avalonia.Views;

/// <summary>The production confirmation of mockup M15: red header, statements, optional typed word, transaction option; Enter and Escape cancel.</summary>
internal sealed class ProductionConfirmWindow : Window
{
    private readonly TextBox? _typed;
    private readonly CheckBox? _option;
    private bool _confirmed;

    /// <summary>Creates the window for a request.</summary>
    /// <param name="request">What is about to run.</param>
    public ProductionConfirmWindow(ProductionConfirmationRequest request)
    {
        Title = request.Title;
        Width = 560;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var mono = this.TryFindResource("SquillFontMono", ActualThemeVariant, out var font) && font is FontFamily family ? family : FontFamily.Default;
        IBrush? Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;
        Background = Brush("SquillBgRaisedBrush");

        var icon = new global::Avalonia.Controls.Shapes.Path { Classes = { "icon" }, Width = 16, Height = 16, Stroke = Brush("SquillOnEnvProdBrush") };
        icon.Bind(global::Avalonia.Controls.Shapes.Path.DataProperty, icon.GetResourceObservable("SquillIconWarning"));
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                icon,
                new TextBlock { Text = UiText.Format("prod.header", request.ConnectionName, request.Database), Foreground = Brush("SquillOnEnvProdBrush"), FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center },
            },
        };
        var band = new Border { Background = Brush("SquillDangerBrush"), Padding = new Thickness(24, 12), Child = header };

        var body = new StackPanel { Spacing = 14, Margin = new Thickness(24, 20, 24, 8) };
        body.Children.Add(new TextBlock { Text = request.Title, FontSize = 20, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFgBrush") });
        body.Children.Add(Message(request, mono, Brush));
        body.Children.Add(Statements(request, mono, Brush));

        if (request.RequiredText is { } word)
        {
            var parts = UiText.Get("prod.typePrompt").Split("{0}");
            var label = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFg2Brush") };
            label.Inlines!.Add(new Run(parts[0]));
            label.Inlines.Add(new Run(word) { FontFamily = mono, FontWeight = FontWeight.Bold, Foreground = Brush("SquillFgBrush") });
            if (parts.Length > 1)
                label.Inlines.Add(new Run(parts[1]));
            _typed = new TextBox { PlaceholderText = word, FontFamily = mono };
            body.Children.Add(new StackPanel { Spacing = 6, Children = { label, _typed } });
        }

        if (request.ShowTransactionOption)
        {
            _option = new CheckBox { Content = UiText.Get("prod.option"), IsChecked = true, IsEnabled = !request.TransactionOptionLocked };
            body.Children.Add(_option);
        }

        // The confirm button is neither the default nor the cancel one: it only reacts to a click, and Enter anywhere in the window is Annuler.
        Confirm = new Button { Content = request.ConfirmLabel, Background = Brush("SquillDangerBrush"), Foreground = Brush("SquillOnEnvProdBrush"), Height = 30, MinHeight = 30, IsEnabled = _typed is null };
        Cancel = new Button { Content = UiText.Get("dialog.cancel"), Height = 30, MinHeight = 30, IsDefault = true, IsCancel = true };
        Confirm.Click += (_, _) =>
        {
            _confirmed = true;
            Close();
        };
        Cancel.Click += (_, _) => Close();
        if (_typed is not null)
            _typed.TextChanged += (_, _) => Confirm.IsEnabled = ProductionGuard.Matches(_typed.Text, request.RequiredText!);

        var footer = new Grid { Margin = new Thickness(24, 12, 24, 20), ColumnSpacing = 8, ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        footer.Children.Add(new TextBlock { Text = UiText.Get("prod.hint"), Foreground = Brush("SquillFg3Brush"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(Confirm, 1);
        Grid.SetColumn(Cancel, 2);
        footer.Children.Add(Confirm);
        footer.Children.Add(Cancel);

        // Tunnel: the typed field and every button see the key after this, so a matching word never turns Enter into a confirmation.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        Opened += (_, _) => (_typed ?? (Control)Cancel).Focus();
        Content = new StackPanel { Children = { band, body, footer } };
    }

    /// <summary>Gets the button that confirms; disabled until the typed word matches.</summary>
    public Button Confirm { get; }

    /// <summary>Gets the Annuler button, the default and cancel one.</summary>
    public Button Cancel { get; }

    /// <summary>Gets the typed-word field, or <see langword="null"/> when the request has no required text.</summary>
    public TextBox? TypedBox => _typed;

    /// <summary>Gets the transaction checkbox, or <see langword="null"/> when the request has no option.</summary>
    public CheckBox? OptionBox => _option;

    /// <summary>Gets the answer once the window is closed.</summary>
    public ProductionConfirmationResult Result => new(_confirmed, _confirmed && _option?.IsChecked == true);

    /// <summary>Shows the window over another one.</summary>
    /// <param name="owner">The window that owns the dialog.</param>
    /// <param name="request">What is about to run.</param>
    /// <returns>The answer; not confirmed when the window was cancelled or closed.</returns>
    public static async Task<ProductionConfirmationResult> ShowAsync(Window owner, ProductionConfirmationRequest request)
    {
        var window = new ProductionConfirmWindow(request);
        await window.ShowDialog(owner);
        return window.Result;
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Escape))
            return;
        e.Handled = true;
        Close();
    }

    // The figure keeps the danger color only when the formatted number is found exactly once in the message; otherwise the text stays plain.
    private static TextBlock Message(ProductionConfirmationRequest request, FontFamily mono, Func<string, IBrush?> brush)
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = brush("SquillFg2Brush") };
        var figure = request.EstimatedRows is { } rows ? rows.ToString("N0", UiText.Culture) : null;
        var at = figure is null ? -1 : request.Message.IndexOf(figure, StringComparison.Ordinal);
        if (figure is null || at < 0 || request.Message.IndexOf(figure, at + 1, StringComparison.Ordinal) >= 0)
        {
            text.Text = request.Message;
            return text;
        }

        text.Inlines!.Add(new Run(request.Message[..at]));
        text.Inlines.Add(new Run(figure) { FontFamily = mono, FontWeight = FontWeight.Bold, Foreground = brush("SquillDangerBrush") });
        text.Inlines.Add(new Run(request.Message[(at + figure.Length)..]));
        return text;
    }

    private static Border Statements(ProductionConfirmationRequest request, FontFamily mono, Func<string, IBrush?> brush)
    {
        var list = new StackPanel { Spacing = 6 };
        // The builder lists five statements and then one already localized "+ N others" line.
        for (var i = 0; i < request.Statements.Count; i++)
        {
            var text = request.Statements[i].Text;
            list.Children.Add(i < 5
                ? new TextBlock { Text = text, FontFamily = mono, FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxLines = 4, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = brush("SquillFgBrush") }
                : new TextBlock { Text = text, FontSize = 12, Foreground = brush("SquillFg3Brush") });
        }

        return new Border { Background = brush("SquillBgBaseBrush"), BorderBrush = brush("SquillDangerBrush"), BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(12, 10), Child = list };
    }
}
