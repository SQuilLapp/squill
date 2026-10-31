using Microsoft.UI.Xaml;

namespace Squill.App.WinUI.Editors;

/// <summary>An <see cref="Squill.Presentation.Editor.ISqlEditor"/> that a view can place in its tree.</summary>
internal interface IEditorControl
{
    /// <summary>Gets the element to place in a view.</summary>
    FrameworkElement Control { get; }
}
