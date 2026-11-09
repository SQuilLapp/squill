using Squill.Core;

namespace Squill.Platform.Updates;

/// <summary>A newer version an engine found.</summary>
/// <param name="Version">The offered version.</param>
/// <param name="ReleaseNotesMarkdown">Release notes in Markdown, when the feed carries them.</param>
/// <param name="DownloadBytes">Size of the download, 0 when unknown.</param>
/// <param name="IsDelta">Whether only a delta package is downloaded.</param>
/// <param name="Handle">Engine-private data the engine casts back; the one allowed <see cref="object"/>, never inspected elsewhere.</param>
public sealed record UpdateOffer(SemanticVersion Version, string? ReleaseNotesMarkdown, long DownloadBytes, bool IsDelta, object Handle);
