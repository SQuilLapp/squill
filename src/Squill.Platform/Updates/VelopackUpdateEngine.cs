using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using Squill.Core;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;
using CoreVersion = Squill.Core.SemanticVersion;

namespace Squill.Platform.Updates;

/// <summary>The <see cref="IUpdateEngine"/> backed by Velopack: GitHub Releases, or a directory or URL given as the feed override.</summary>
public sealed class VelopackUpdateEngine : IUpdateEngine
{
    private readonly string _repositoryUrl;
    private readonly string? _feedOverride;
    private readonly Architecture _architecture;
    private readonly IVelopackLocator? _locator;

    /// <summary>Initializes the engine.</summary>
    /// <param name="repositoryUrl">The GitHub repository that hosts the releases.</param>
    /// <param name="feedOverride">A directory path or an http(s) URL that replaces the feed; null or empty means GitHub.</param>
    /// <param name="architecture">The architecture used in channel names; defaults to the running process architecture.</param>
    public VelopackUpdateEngine(string repositoryUrl, string? feedOverride, Architecture? architecture = null)
        : this(repositoryUrl, feedOverride, architecture, locator: null)
    {
    }

    internal VelopackUpdateEngine(string repositoryUrl, string? feedOverride, Architecture? architecture, IVelopackLocator? locator)
    {
        _repositoryUrl = repositoryUrl;
        _feedOverride = feedOverride;
        _architecture = architecture ?? RuntimeInformation.ProcessArchitecture;
        _locator = locator;
    }

    internal enum FeedKind
    {
        GitHub,
        Directory,
        Web,
        Invalid,
    }

    /// <inheritdoc/>
    public bool IsInstalled => Manager()?.IsInstalled ?? false;

    /// <inheritdoc/>
    public CoreVersion? InstalledVersion =>
        CoreVersion.TryParse(Manager()?.CurrentVersion?.ToString(), out var version) ? version : null;

    /// <inheritdoc/>
    public async ValueTask<UpdateOffer?> CheckAsync(UpdateChannel channel, CancellationToken ct)
    {
        if (!IsInstalled)
        {
            throw new UpdateEngineException(UpdateFailure.NotInstalled, "The application is not installed.");
        }

        var manager = new UpdateManager(CreateSource(channel), new UpdateOptions { ExplicitChannel = ChannelName(channel, _architecture, CurrentOs()) }, _locator);
        UpdateInfo? info;
        try
        {
            info = await manager.CheckForUpdatesAsync().WaitAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            throw Map(ex);
        }

        if (info is null)
        {
            return null;
        }

        var target = info.TargetFullRelease;
        if (!CoreVersion.TryParse(target.Version.ToString(), out var version))
        {
            throw new UpdateEngineException(UpdateFailure.FeedInvalid, $"The feed offers a version that is not valid: {target.Version}.");
        }

        var isDelta = info.DeltasToTarget.Length > 0;
        var bytes = isDelta ? info.DeltasToTarget.Sum(d => d.Size) : target.Size;
        var notes = string.IsNullOrWhiteSpace(target.NotesMarkdown) ? null : target.NotesMarkdown;
        return new UpdateOffer(version, notes, bytes, isDelta, new Pending(manager, info));
    }

    /// <inheritdoc/>
    public async ValueTask DownloadAsync(UpdateOffer offer, IProgress<int> percent, CancellationToken ct)
    {
        var pending = PendingOf(offer);
        try
        {
            await pending.Manager.DownloadUpdatesAsync(pending.Info, percent.Report, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            throw Map(ex);
        }
    }

    /// <inheritdoc/>
    public void ApplyAndRestart(UpdateOffer offer)
    {
        var pending = PendingOf(offer);
        pending.Manager.ApplyUpdatesAndRestart(pending.Info);
    }

    /// <summary>Builds the Velopack channel name, which carries the architecture (and the OS off Windows) because Velopack keeps one feed per channel and file names have no OS.</summary>
    /// <param name="channel">The user's channel.</param>
    /// <param name="architecture">The process architecture.</param>
    /// <param name="os">The OS infix: <c>linux</c> or <c>osx</c>; null or empty for Windows, whose channel names stay unchanged.</param>
    /// <returns>For example <c>stable-x64</c> (Windows) or <c>stable-linux-x64</c>.</returns>
    internal static string ChannelName(UpdateChannel channel, Architecture architecture, string? os = null) =>
        string.IsNullOrEmpty(os)
            ? $"{channel.ChannelId()}-{architecture.ToString().ToLowerInvariant()}"
            : $"{channel.ChannelId()}-{os}-{architecture.ToString().ToLowerInvariant()}";

    private static string? CurrentOs() =>
        OperatingSystem.IsLinux() ? "linux" : OperatingSystem.IsMacOS() ? "osx" : null;

    /// <summary>Classifies a feed override.</summary>
    /// <param name="feedOverride">The raw value of <c>SQUILL_UPDATE_FEED</c>.</param>
    /// <returns>The kind of source it designates.</returns>
    internal static FeedKind Classify(string? feedOverride)
    {
        if (string.IsNullOrWhiteSpace(feedOverride))
        {
            return FeedKind.GitHub;
        }

        if (Directory.Exists(feedOverride))
        {
            return FeedKind.Directory;
        }

        return Uri.TryCreate(feedOverride, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? FeedKind.Web
            : FeedKind.Invalid;
    }

    // Without a locator Velopack needs VelopackApp.Run() to have run; a dev run (or a test) that skipped it is simply not installed.
    private UpdateManager? Manager() =>
        _locator is null && !VelopackLocator.IsCurrentSet ? null : new UpdateManager(options: null, locator: _locator);

    private static Pending PendingOf(UpdateOffer offer) =>
        offer.Handle as Pending ?? throw new ArgumentException("The offer was not produced by this engine.", nameof(offer));

    private static UpdateEngineException Map(Exception ex) => ex switch
    {
        UpdateEngineException known => known,
        HttpRequestException or OperationCanceledException => new(UpdateFailure.Network, ex.Message, ex),
        JsonException => new(UpdateFailure.FeedInvalid, ex.Message, ex),
        InvalidDataException => new(UpdateFailure.VerificationFailed, ex.Message, ex),
        IOException or UnauthorizedAccessException => new(UpdateFailure.Disk, ex.Message, ex),
        _ => new(UpdateFailure.Unknown, ex.Message, ex),
    };

    private IUpdateSource CreateSource(UpdateChannel channel) => Classify(_feedOverride) switch
    {
        FeedKind.Directory => new SimpleFileSource(new DirectoryInfo(_feedOverride!)),
        FeedKind.Web => new SimpleWebSource(_feedOverride!),
        FeedKind.GitHub => new GithubSource(_repositoryUrl, accessToken: null, prerelease: channel == UpdateChannel.Beta),
        _ => throw new UpdateEngineException(UpdateFailure.FeedInvalid, "The update feed must be an existing directory or an http(s) URL."),
    };

    private sealed record Pending(UpdateManager Manager, UpdateInfo Info);
}
