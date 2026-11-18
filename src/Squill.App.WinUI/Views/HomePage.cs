using System.Collections.Specialized;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using ColumnDefinition = Microsoft.UI.Xaml.Controls.ColumnDefinition;
using Squill.Core;
using Squill.Presentation.Home;
using Squill.Presentation.Localization;

namespace Squill.App.WinUI.Views;

/// <summary>The home page (mockup M01): recent connection cards, quick actions, tip card and the startup checkbox, all built in code from <see cref="HomeViewModel"/>.</summary>
public sealed partial class HomePage : UserControl
{
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private HomeViewModel? _home;

    /// <summary>Creates the page; call <see cref="Attach"/> to give it a view-model.</summary>
    public HomePage()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Content = _scroll;
        ActualThemeChanged += (_, _) => Rebuild();
    }

    /// <summary>Connects the page to its view-model and draws it.</summary>
    /// <param name="home">The home view-model.</param>
    public void Attach(HomeViewModel home)
    {
        _home = home;
        home.RecentCards.CollectionChanged += OnCardsChanged;
        home.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(HomeViewModel.TipIndex) or nameof(HomeViewModel.TipsHidden))
                Rebuild();
        };
        Rebuild();
    }

    /// <summary>Opens the card that has the given 1-based position, as the <c>Ctrl+1</c> to <c>Ctrl+6</c> shortcuts do.</summary>
    /// <param name="position">Position of the card, from 1.</param>
    /// <returns><see langword="true"/> when a card exists at that position.</returns>
    public bool OpenCard(int position)
    {
        if (_home is not { } home || position < 1 || position > home.RecentCards.Count)
            return false;
        home.RecentCards[position - 1].OpenCommand.Execute(null);
        return true;
    }

    private void OnCardsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private Brush Brush(string key) => Tokens.Brush(this, key);

    private static Style StyleOf(string key) => (Style)Application.Current.Resources[key];

    private static double Size(string key) => (double)Application.Current.Resources[key];

    private void Rebuild()
    {
        if (_home is not { } home)
            return;
        var page = new Grid { MaxWidth = 1160, Margin = new Thickness(40, 40, 40, 32), HorizontalAlignment = HorizontalAlignment.Center, RowSpacing = 28 };
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        page.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        page.ColumnSpacing = 32;
        var header = Header(home);
        Grid.SetColumnSpan(header, 2);
        page.Children.Add(header);
        var left = Recent(home);
        Grid.SetRow(left, 1);
        var right = Right(home);
        Grid.SetRow(right, 1);
        Grid.SetColumn(right, 1);
        page.Children.Add(left);
        page.Children.Add(right);
        _scroll.Content = page;
    }

    private Grid Header(HomeViewModel home)
    {
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
        var logo = new Image
        {
            Width = 32,
            Height = 32,
            Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "squill-32.png"))),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock
        {
            Text = "SQuilL",
            FontFamily = (FontFamily)Application.Current.Resources["SquillFontBrand"],
            FontSize = Size("SquillFontSizeDisplay"),
            FontWeight = FontWeights.Bold,
            Foreground = Brush("SquillFgBrush"),
        });
        title.Children.Add(new TextBlock { Text = UiText.Format("about.version", AppVersion.Current), Foreground = Brush("SquillFg2Brush") });
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { logo, title } };
        grid.Children.Add(brand);
        var search = SearchBox(home);
        Grid.SetColumn(search, 2);
        grid.Children.Add(search);
        return grid;
    }

    private Button SearchBox(HomeViewModel home)
    {
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(Icons.Create("SquillIconSearch", 14, Brush("SquillFg3Brush")));
        var text = new TextBlock { Text = UiText.Get("search.placeholder"), Foreground = Brush("SquillFg3Brush"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        var chip = new ContentControl { Content = "Ctrl+K", Style = StyleOf("KeyboardChipStyle"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(chip, 2);
        row.Children.Add(chip);
        var button = new Button
        {
            Content = row,
            Height = 34,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(12, 0, 10, 0),
            CornerRadius = new CornerRadius(6),
            Background = Brush("SquillBgRaisedBrush"),
            BorderBrush = Brush("SquillBorderBrush"),
        };
        button.Click += (_, _) => home.SearchCommand.Execute(null);
        button.GotFocus += (_, _) => button.BorderBrush = Brush("SquillFocusBrush");
        button.LostFocus += (_, _) => button.BorderBrush = Brush("SquillBorderBrush");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, UiText.Get("search.placeholder"));
        return button;
    }

    private StackPanel Recent(HomeViewModel home)
    {
        var panel = new StackPanel { Spacing = 12 };
        var header = new Grid();
        header.Children.Add(Caps(UiText.Get("home.recent")));
        var manage = new HyperlinkButton { Content = UiText.Get("home.manageConnections"), Padding = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right };
        manage.Click += (_, _) => home.ManageConnectionsCommand.Execute(null);
        header.Children.Add(manage);
        panel.Children.Add(header);
        if (home.RecentCards.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = UiText.Get("home.noConnection"), Foreground = Brush("SquillFg2Brush") });
            return panel;
        }

        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < home.RecentCards.Count; i++)
        {
            if (i % 2 == 0)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var card = Card(home.RecentCards[i]);
            Grid.SetRow(card, i / 2);
            Grid.SetColumn(card, i % 2);
            grid.Children.Add(card);
        }

        panel.Children.Add(grid);
        return panel;
    }

    private Border Card(HomeRecentCardViewModel card)
    {
        var environment = card.Environment switch
        {
            ConnectionEnvironment.Production => "Prod",
            ConnectionEnvironment.Staging => "Staging",
            ConnectionEnvironment.Development => "Dev",
            ConnectionEnvironment.Local => "Local",
            _ => "",
        };
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new ContentControl { Style = ExplorerGlyphs.EngineStyle(card.Profile), Content = card.EngineLetters, VerticalAlignment = VerticalAlignment.Center });
        title.Children.Add(new TextBlock
        {
            Text = card.Name,
            FontWeight = FontWeights.SemiBold,
            FontSize = Size("SquillFontSizeSubtitle"),
            Foreground = Brush("SquillFgBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (environment.Length > 0)
            title.Children.Add(new ContentControl { Style = ExplorerGlyphs.EnvironmentStyle(card.Profile), Content = card.EnvironmentText, VerticalAlignment = VerticalAlignment.Center });
        var top = new Grid();
        top.Children.Add(title);
        top.Children.Add(new ContentControl { Content = card.Shortcut, Style = StyleOf("KeyboardChipStyle"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center });
        var body = new StackPanel { Spacing = 10, Margin = new Thickness(16, 14, 16, 14) };
        body.Children.Add(top);
        body.Children.Add(new TextBlock
        {
            Text = card.Subtitle,
            FontFamily = (FontFamily)Application.Current.Resources["SquillFontMono"],
            FontSize = Size("SquillFontSizeBodySm"),
            Foreground = Brush("SquillFg2Brush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        var details = new TextBlock { Text = card.Details, Foreground = Brush("SquillFg3Brush"), FontSize = Size("SquillFontSizeBodySm"), TextTrimming = TextTrimming.CharacterEllipsis };
        var tabs = new TextBlock { Text = card.OpenTabsText, Foreground = Brush("SquillFg3Brush"), FontSize = Size("SquillFontSizeBodySm"), HorizontalAlignment = HorizontalAlignment.Right };
        var bottom = new Grid { ColumnSpacing = 8 };
        bottom.ColumnDefinitions.Add(new ColumnDefinition());
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(tabs, 1);
        bottom.Children.Add(details);
        bottom.Children.Add(tabs);
        card.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HomeRecentCardViewModel.Details))
                details.Text = card.Details;
            else if (e.PropertyName == nameof(HomeRecentCardViewModel.OpenTabsText))
                tabs.Text = card.OpenTabsText;
        };
        body.Children.Add(bottom);
        var stack = new Grid();
        stack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        stack.RowDefinitions.Add(new RowDefinition());
        stack.Children.Add(new Border { Height = 3, Background = Brush(environment.Length > 0 ? $"SquillEnv{environment}Brush" : "SquillBorderBrush") });
        Grid.SetRow(body, 1);
        stack.Children.Add(body);
        var open = new Button
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            CornerRadius = new CornerRadius(8),
        };
        foreach (var key in new[] { "ButtonBackgroundPointerOver", "ButtonBackgroundPressed" })
            open.Resources[key] = Brush("SquillBgHoverBrush");
        Grid.SetRowSpan(open, 2);
        open.Click += (_, _) => card.OpenCommand.Execute(null);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(open, card.Name);
        stack.Children.Add(open);
        return new Border
        {
            Child = stack,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush("SquillBorderSubtleBrush"),
            Background = Brush("SquillBgRaisedBrush"),
        };
    }

    private StackPanel Right(HomeViewModel home)
    {
        var panel = new StackPanel { Spacing = 16 };
        panel.Children.Add(Caps(UiText.Get("home.start")));
        panel.Children.Add(NewConnectionTile(home));
        if (!home.TipsHidden)
            panel.Children.Add(TipCard(home));
        var check = new CheckBox { Content = UiText.Get("home.showAtStartup"), IsChecked = home.ShowAtStartup };
        check.Checked += (_, _) => home.ShowAtStartup = true;
        check.Unchecked += (_, _) => home.ShowAtStartup = false;
        panel.Children.Add(check);
        return panel;
    }

    private Button NewConnectionTile(HomeViewModel home)
    {
        var tile = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(6),
            Background = Brush("SquillAccentBrush"),
            Child = Icons.Create("SquillIconPlus", 16, Brush("SquillOnAccentBrush")),
            VerticalAlignment = VerticalAlignment.Top,
        };
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(tile);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = UiText.Get("home.newConnection"), FontWeight = FontWeights.SemiBold, Foreground = Brush("SquillFgBrush") });
        text.Children.Add(new TextBlock { Text = UiText.Get("home.newConnection.subtitle"), Foreground = Brush("SquillFg2Brush"), FontSize = Size("SquillFontSizeBodySm"), TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        var chip = new ContentControl { Content = "Ctrl+N", Style = StyleOf("KeyboardChipStyle"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(chip, 2);
        row.Children.Add(chip);
        var button = new Button
        {
            Content = row,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8),
        };
        button.Click += (_, _) => home.NewConnectionCommand.Execute(null);
        return button;
    }

    private Border TipCard(HomeViewModel home)
    {
        var head = new Grid();
        head.Children.Add(Caps(UiText.Get("home.tip")));
        head.Children.Add(new TextBlock { Text = home.TipPosition, Foreground = Brush("SquillFg3Brush"), HorizontalAlignment = HorizontalAlignment.Right });
        var text = new RichTextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFgBrush") };
        var paragraph = new Paragraph();
        foreach (var segment in home.TipSegments)
        {
            if (segment.IsKey)
                paragraph.Inlines.Add(new InlineUIContainer { Child = new ContentControl { Content = segment.Text, Style = StyleOf("KeyboardChipStyle"), Margin = new Thickness(2, 0, 2, -4) } });
            else
                paragraph.Inlines.Add(new Run { Text = segment.Text });
        }

        text.Blocks.Add(paragraph);
        var next = new HyperlinkButton { Content = UiText.Get("home.tip.next"), Padding = new Thickness(0) };
        next.Click += (_, _) => home.NextTipCommand.Execute(null);
        var hide = new HyperlinkButton { Content = UiText.Get("home.tip.hide"), Padding = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right };
        hide.Click += (_, _) => home.HideTipsCommand.Execute(null);
        var footer = new Grid();
        footer.Children.Add(next);
        footer.Children.Add(hide);
        return new Border
        {
            Child = new StackPanel { Spacing = 10, Children = { head, text, footer } },
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush("SquillBorderSubtleBrush"),
            Background = Brush("SquillBgRaisedBrush"),
        };
    }

    private static TextBlock Caps(string text) => new() { Text = text, Style = StyleOf("CapsTextBlockStyle"), VerticalAlignment = VerticalAlignment.Center };
}
