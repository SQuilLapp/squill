using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Platform.Profiles;
using Squill.Platform.Settings;
using Squill.Presentation.Connections;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Home;

/// <summary>What the home page shows and does: recent connections, quick actions, the tip of the day and the startup setting.</summary>
public sealed partial class HomeViewModel : ViewModelBase
{
    /// <summary>Most recent connections shown as cards.</summary>
    public const int MaxCards = 6;

    private readonly ConnectionListViewModel _connections;
    private readonly IRecentConnectionsStore _recent;
    private readonly ISettingsStore _settings;
    private readonly Func<ConnectionProfile, Task> _connect;
    private readonly Action _openPalette;
    private readonly TimeProvider _time;
    private readonly Func<Guid, (int Tabs, string? Version)> _live;
    private bool _loading;

    internal HomeViewModel(
        ConnectionListViewModel connections, IRecentConnectionsStore recent, ISettingsStore settings,
        Func<ConnectionProfile, Task> connect, Action openPalette, TimeProvider time, Func<Guid, (int Tabs, string? Version)> live)
    {
        _connections = connections;
        _recent = recent;
        _settings = settings;
        _connect = connect;
        _openPalette = openPalette;
        _time = time;
        _live = live;
    }

    /// <summary>Raised when the user asks to manage the connections (the "Gérer les connexions" link); the head shows its connection list.</summary>
    public event EventHandler? ManageConnectionsRequested;

    /// <summary>Gets the recent connections, newest first, at most <see cref="MaxCards"/>.</summary>
    public ObservableCollection<HomeRecentCardViewModel> RecentCards { get; } = [];

    /// <summary>Gets the command creating a connection (<c>Ctrl+N</c>).</summary>
    public IRelayCommand NewConnectionCommand => _connections.NewCommand;

    /// <summary>Gets the "manage connections" link command.</summary>
    public IRelayCommand ManageConnectionsCommand => field ??= new RelayCommand(() => ManageConnectionsRequested?.Invoke(this, EventArgs.Empty));

    /// <summary>Gets the command opening the command palette.</summary>
    public IRelayCommand SearchCommand => field ??= new RelayCommand(_openPalette);

    /// <summary>Gets or sets whether the home page opens at startup; saved when changed.</summary>
    [ObservableProperty]
    public partial bool ShowAtStartup { get; set; } = true;

    /// <summary>Gets or sets the index of the tip on screen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TipText))]
    [NotifyPropertyChangedFor(nameof(TipSegments))]
    [NotifyPropertyChangedFor(nameof(TipPosition))]
    public partial int TipIndex { get; private set; }

    /// <summary>Gets whether the tips card is hidden.</summary>
    [ObservableProperty]
    public partial bool TipsHidden { get; private set; }

    /// <summary>Gets the text of the tip on screen, with its <c>{kbd:...}</c> markers.</summary>
    public string TipText => UiText.Get(HomeTips.Keys[TipIndex]);

    /// <summary>Gets <see cref="TipText"/> split into text and key-chip segments.</summary>
    public IReadOnlyList<TipSegment> TipSegments => HomeTips.Parse(TipText);

    /// <summary>Gets the position of the tip, as <c>3 / 10</c>.</summary>
    public string TipPosition => $"{TipIndex + 1} / {HomeTips.Keys.Count}";

    /// <summary>Loads the settings and the recent connections; the connection list must already be loaded.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the page is ready.</returns>
    public async Task InitializeAsync(CancellationToken ct)
    {
        var settings = await _settings.LoadAsync(ct);
        _loading = true;
        ShowAtStartup = settings.ShowHomeAtStartup;
        TipIndex = Math.Clamp(settings.HomeTipIndex, 0, HomeTips.Keys.Count - 1);
        TipsHidden = settings.HomeTipsHidden;
        _loading = false;
        await RefreshAsync(ct);
    }

    /// <summary>Rebuilds the cards from the recent store; a connection that no longer exists is skipped and forgotten.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the cards are rebuilt.</returns>
    public async Task RefreshAsync(CancellationToken ct)
    {
        var now = _time.GetLocalNow();
        List<HomeRecentCardViewModel> cards = [];
        foreach (var entry in await _recent.ListAsync(ct))
        {
            if (_connections.Profiles.FirstOrDefault(p => p.Id == entry.ProfileId) is not { } profile)
            {
                await _recent.ForgetAsync(entry.ProfileId, ct);
                continue;
            }

            if (cards.Count < MaxCards)
                cards.Add(new HomeRecentCardViewModel(profile, RelativeTimeFormatter.Format(entry.LastOpened, now), $"Ctrl+{cards.Count + 1}", () => _connect(profile)));
        }

        RecentCards.Clear();
        foreach (var card in cards)
            RecentCards.Add(card);
        RefreshLive();
    }

    /// <summary>Updates the open-tab count and server version of every card from the live sessions.</summary>
    public void RefreshLive()
    {
        foreach (var card in RecentCards)
        {
            var (tabs, version) = _live(card.Profile.Id);
            card.ApplyLive(tabs, version);
        }
    }

    /// <summary>Records that a connection was opened now and refreshes the cards.</summary>
    /// <param name="profileId">Identifier of the opened profile.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the store is updated.</returns>
    public async Task TouchAsync(Guid profileId, CancellationToken ct)
    {
        await _recent.TouchAsync(profileId, _time.GetUtcNow(), ct);
        await RefreshAsync(ct);
    }

    [RelayCommand]
    private async Task NextTipAsync(CancellationToken ct)
    {
        TipIndex = (TipIndex + 1) % HomeTips.Keys.Count;
        await SaveAsync(s => s with { HomeTipIndex = TipIndex }, ct);
    }

    [RelayCommand]
    private async Task HideTipsAsync(CancellationToken ct)
    {
        TipsHidden = true;
        await SaveAsync(s => s with { HomeTipsHidden = true }, ct);
    }

    partial void OnShowAtStartupChanged(bool value)
    {
        if (!_loading)
            _ = SaveAsync(s => s with { ShowHomeAtStartup = value }, CancellationToken.None);
    }

    private async Task SaveAsync(Func<AppSettings, AppSettings> change, CancellationToken ct) =>
        await _settings.SaveAsync(change(await _settings.LoadAsync(ct)), ct);
}
