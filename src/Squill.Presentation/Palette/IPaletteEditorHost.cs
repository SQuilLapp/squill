using Squill.Presentation.Editor;

namespace Squill.Presentation.Palette;

/// <summary>What the history and line providers need from the workspace: the objects host plus the editor of the active query tab.</summary>
public interface IPaletteEditorHost : IPaletteObjectHost
{
    /// <summary>Gets the editor of the active query tab, or <see langword="null"/> when no query tab is active.</summary>
    ISqlEditor? ActiveEditor { get; }
}
