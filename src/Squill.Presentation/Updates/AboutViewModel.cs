using System.Globalization;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Platform.Lifecycle;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Updates;

/// <summary>What the About dialog shows: version, channel, install kind, runtime, data folder, notes of the installed version and the update status.</summary>
public sealed partial class AboutViewModel : ViewModelBase
{
    private const string ChangelogResource = "Squill.Presentation.CHANGELOG.md";
    private readonly string _currentVersion;
    private readonly string? _previousVersion;
    private readonly string? _changelog;

    /// <summary>Initializes the view-model.</summary>
    /// <param name="updates">The update flow shown under the version.</param>
    /// <param name="dataFolder">The folder holding the user's data.</param>
    /// <param name="runKind">How this start relates to the previous one.</param>
    /// <param name="previousVersion">Version that ran before, or <see langword="null"/> when unknown.</param>
    /// <param name="currentVersion">Version of the running build.</param>
    /// <param name="changelog">Text of <c>CHANGELOG.md</c>, or <see langword="null"/> when not available (see <see cref="ReadEmbeddedChangelog"/>).</param>
    public AboutViewModel(UpdateViewModel updates, string dataFolder, RunKind runKind, string? previousVersion, string currentVersion, string? changelog)
    {
        Updates = updates;
        DataFolder = dataFolder;
        RunKind = runKind;
        _previousVersion = previousVersion;
        _currentVersion = currentVersion;
        _changelog = changelog;
    }

    /// <summary>Raised when the user asks to open <see cref="DataFolder"/>; the head opens it in the file manager.</summary>
    public event EventHandler? OpenDataFolderRequested;

    /// <summary>Gets the update flow, for the status line and the buttons.</summary>
    public UpdateViewModel Updates { get; }

    /// <summary>Gets the folder holding the user's data.</summary>
    public string DataFolder { get; }

    /// <summary>Gets how this start relates to the previous one.</summary>
    public RunKind RunKind { get; }

    /// <summary>Gets the product name, never translated.</summary>
    public string ProductName { get; } = "SQuilL";

    /// <summary>Gets the address of the project repository.</summary>
    public string RepositoryUrl { get; } = "https://github.com/squillapp/squill";

    /// <summary>Gets the version line.</summary>
    public string VersionText => UiText.Format("about.version", _currentVersion);

    /// <summary>Gets the channel line, from the pre-release flag of the running version.</summary>
    public string ChannelText => UiText.Format(
        "about.channel",
        UiText.Get(SemanticVersion.Parse(_currentVersion, CultureInfo.InvariantCulture).ChannelOf() == UpdateChannel.Beta ? "about.channel.beta" : "about.channel.stable"));

    /// <summary>Gets the line saying whether the installer put SQuilL here.</summary>
    public string InstallKindText => UiText.Get(Updates.IsInstalled ? "about.install.installed" : "about.install.portable");

    /// <summary>Gets the runtime line.</summary>
    public string RuntimeText { get; } = UiText.Format(
        RuntimeFeature.IsDynamicCodeSupported ? "about.runtime.jit" : "about.runtime",
        Environment.Version.ToString(3));

    /// <summary>Gets the license line.</summary>
    public string LicenseText { get; } = UiText.Get("about.license");

    /// <summary>Gets a value indicating whether the "updated from" line and the notes are shown.</summary>
    public bool ShowWhatsNew => RunKind == RunKind.Updated;

    /// <summary>Gets the "updated from" line.</summary>
    public string WhatsNewText => UiText.Format("about.whatsnew", _previousVersion ?? string.Empty);

    /// <summary>Gets the release notes of the running version, empty when the changelog has none.</summary>
    public IReadOnlyList<NotesBlock> Notes => ReleaseNotes.Parse(ChangelogSection.Extract(_changelog, _currentVersion));

    /// <summary>Reads <c>CHANGELOG.md</c> embedded in this assembly.</summary>
    /// <returns>The changelog text, or <see langword="null"/> when the resource is missing.</returns>
    public static string? ReadEmbeddedChangelog()
    {
        using var stream = typeof(AboutViewModel).Assembly.GetManifestResourceStream(ChangelogResource);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [RelayCommand]
    private void OpenDataFolder() => OpenDataFolderRequested?.Invoke(this, EventArgs.Empty);
}
