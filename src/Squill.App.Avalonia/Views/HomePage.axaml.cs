using Avalonia.Controls;
using Squill.Core;
using Squill.Presentation.Home;
using Squill.Presentation.Localization;

namespace Squill.App.Avalonia.Views;

/// <summary>The home page (mockup M01): recent connection cards, quick actions, tip card and the startup checkbox, all bound to <see cref="HomeViewModel"/>.</summary>
public sealed partial class HomePage : UserControl
{
    /// <summary>Creates the page; call <see cref="Attach"/> to give it a view-model.</summary>
    public HomePage()
    {
        InitializeComponent();
        VersionText.Text = UiText.Format("about.version", AppVersion.Current);
    }

    /// <summary>Connects the page to its view-model.</summary>
    /// <param name="home">The home view-model.</param>
    public void Attach(HomeViewModel home)
    {
        DataContext = home;
        home.RecentCards.CollectionChanged += (_, _) => SyncEmpty(home);
        SyncEmpty(home);
    }

    /// <summary>Opens the card that has the given 1-based position, as the <c>Ctrl+1</c> to <c>Ctrl+6</c> shortcuts do.</summary>
    /// <param name="position">Position of the card, from 1.</param>
    /// <returns><see langword="true"/> when a card exists at that position.</returns>
    public bool OpenCard(int position)
    {
        if (DataContext is not HomeViewModel home || position < 1 || position > home.RecentCards.Count)
            return false;
        home.RecentCards[position - 1].OpenCommand.Execute(null);
        return true;
    }

    // Empty state: one line and the new-connection tile, no empty grid.
    private void SyncEmpty(HomeViewModel home)
    {
        EmptyText.IsVisible = home.RecentCards.Count == 0;
        Cards.IsVisible = home.RecentCards.Count > 0;
    }
}
