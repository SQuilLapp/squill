using Squill.Core;
using Squill.Presentation.Sessions;

namespace Squill.Presentation.Palette;

/// <summary>What the objects provider needs from the workspace: the open sessions and the ways to open an object.</summary>
public interface IPaletteObjectHost
{
    /// <summary>Gets every open session, including those opened on other databases of a server.</summary>
    IReadOnlyList<ConnectionSession> Sessions { get; }

    /// <summary>Gets the session the user is working on, or <see langword="null"/>.</summary>
    ConnectionSession? ActiveSession { get; }

    /// <summary>Makes <paramref name="session"/> the active one, then opens and runs the "first rows" query of a table or view, as an explorer double click does.</summary>
    /// <param name="session">The session that owns the table.</param>
    /// <param name="table">The table, view or materialized view.</param>
    /// <returns>A task that completes when the query ended.</returns>
    ValueTask OpenTableAsync(ConnectionSession session, TableInfo table);

    /// <summary>Makes <paramref name="session"/> the active one and opens a new query tab holding <paramref name="text"/>, not executed, with the caret at its end.</summary>
    /// <param name="session">The session of the new tab.</param>
    /// <param name="text">The script text.</param>
    void OpenScript(ConnectionSession session, string text);
}
