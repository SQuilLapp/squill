#if SQUILL_LOOMTERMINAL
using Avalonia.Controls;
using LoomTerminal.Avalonia.Controls;
using Squill.Presentation.Terminal;

namespace Squill.App.Avalonia.Views;

/// <summary>A LoomTerminal control showing the session of a terminal tab.</summary>
internal sealed class TerminalTabView : UserControl
{
    private readonly TerminalView _terminal = new();
    private readonly LoomTerminalHost _host;

    /// <summary>Creates the view and attaches it to the host's current and future sessions.</summary>
    /// <param name="tab">The terminal tab to show.</param>
    public TerminalTabView(TerminalTabViewModel tab)
    {
        _host = (LoomTerminalHost)tab.Host;
        Content = _terminal;
        _terminal.Session = _host.Session;
        _host.SessionChanged += OnSessionChanged;
    }

    private void OnSessionChanged(object? sender, EventArgs e) => _terminal.Session = _host.Session;

    /// <summary>Detaches the control from the host before its tab closes.</summary>
    public void Detach()
    {
        _host.SessionChanged -= OnSessionChanged;
        _terminal.Session = null;
    }

    /// <summary>Gives the keyboard focus to the terminal.</summary>
    public void FocusTerminal() => _terminal.Focus();
}
#endif
