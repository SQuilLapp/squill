using Squill.Presentation.Editor;

namespace Squill.App.WinUI.Editors;

/// <summary>Chooses the editor control of the head: LoomEditor when the build references it, else the plain text box.</summary>
internal static class EditorFactory
{
    /// <summary>Creates an editor for a new query tab.</summary>
    /// <returns>A <see cref="LoomSqlEditor"/> when <c>SQUILL_LOOMEDITOR</c> is defined, otherwise a <see cref="PlainSqlEditor"/>.</returns>
    public static ISqlEditor Create() =>
#if SQUILL_LOOMEDITOR
        new LoomSqlEditor();
#else
        new PlainSqlEditor();
#endif
}
