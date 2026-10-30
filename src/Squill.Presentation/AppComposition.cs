using Squill.Data;
using Squill.Platform;
using Squill.Platform.Profiles;
using Squill.Platform.Secrets;
using Squill.Platform.Settings;
using Squill.Platform.Ssh;
using Squill.Platform.Storage;
using Squill.Presentation.Connections;
using Squill.Presentation.Editor;
using Squill.Presentation.Sessions;
using Squill.Presentation.Workspace;

namespace Squill.Presentation;

/// <summary>What each head provides so the shared object graph can be built.</summary>
public interface IPlatformServices
{
    /// <summary>Gets the dispatcher of the UI thread.</summary>
    IUiDispatcher Dispatcher { get; }

    /// <summary>Gets the modal dialogs of the head.</summary>
    IDialogService Dialogs { get; }

    /// <summary>Gets the factory creating the head's SQL editor for a new tab.</summary>
    Func<ISqlEditor> EditorFactory { get; }

    /// <summary>Gets the registry the head filled with the database providers it ships.</summary>
    ProviderRegistry Providers { get; }
}

/// <summary>The root view-models built by <see cref="AppComposition"/>.</summary>
/// <param name="Workspace">The shell view-model.</param>
public sealed record AppServices(WorkspaceViewModel Workspace);

/// <summary>Builds the object graph shared by all heads.</summary>
public static class AppComposition
{
    /// <summary>Creates the stores and view-models.</summary>
    /// <param name="platform">The services supplied by the head.</param>
    /// <param name="paths">Application paths; <see langword="null"/> uses the per-user default (tests pass a temporary directory).</param>
    /// <returns>The root view-models.</returns>
    public static AppServices Create(IPlatformServices platform, AppPaths? paths = null)
    {
        ArgumentNullException.ThrowIfNull(platform);
        paths ??= new AppPaths();
        var profiles = new JsonProfileStore(paths, SecretStoreFactory.Create(paths.DataDirectory));
        var history = new JsonQueryHistoryStore(paths);
        var settings = new JsonSettingsStore(paths);
        var sessions = new SessionManager(platform.Providers, profiles, new SshNetTunnelFactory());
        var list = new ConnectionListViewModel(profiles, platform.Providers, platform.Dialogs);
        return new AppServices(new WorkspaceViewModel(
            sessions, list, history, settings, platform.Dispatcher, platform.Dialogs, platform.EditorFactory));
    }
}
