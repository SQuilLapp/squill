using Squill.Core;
using Squill.Presentation.Designer;
using Squill.Presentation.Diagram;
using Squill.Presentation.Query;
using Squill.Presentation.Terminal;

namespace Squill.App.WinUI.Views;

/// <summary>What a tab of the strip shows, read from the tab's view-model.</summary>
internal static class TabInfo
{
    /// <summary>Reads the title, the environment of the connection and the unsaved flag of a tab view-model.</summary>
    /// <param name="source">A query, designer, diagram or terminal tab view-model.</param>
    /// <returns>The title, the environment (<see cref="ConnectionEnvironment.None"/> for terminals) and whether the tab holds unsaved work.</returns>
    public static (string Title, ConnectionEnvironment Environment, bool IsDirty) Describe(object source) => source switch
    {
        QueryTabViewModel q => (q.Title, q.Session.Profile.Environment, q.IsModified),
        TableDesignerViewModel d => (d.Title, d.Session.Profile.Environment, d.HasChanges),
        DiagramTabViewModel g => (g.Title, g.Session.Profile.Environment, false),
        TerminalTabViewModel t => (t.Title, ConnectionEnvironment.None, false),
        _ => ("", ConnectionEnvironment.None, false),
    };

    /// <summary>Gets the suffix of the tab style keys (<c>SquillTabItem{Suffix}Style</c>, <c>EnvSquare{Suffix}Style</c>) for an environment.</summary>
    /// <param name="environment">The environment of the tab's connection.</param>
    /// <returns>The suffix; empty for the neutral look of tabs without environment.</returns>
    public static string StyleSuffix(ConnectionEnvironment environment) => environment switch
    {
        ConnectionEnvironment.Production => "Prod",
        ConnectionEnvironment.Staging => "Staging",
        ConnectionEnvironment.Development => "Dev",
        ConnectionEnvironment.Local => "Local",
        _ => "",
    };
}
