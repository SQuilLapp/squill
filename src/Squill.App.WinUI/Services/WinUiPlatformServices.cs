using Microsoft.UI.Dispatching;
using Squill.Data;
using Squill.Presentation;
using Squill.Presentation.Editor;

namespace Squill.App.WinUI.Services;

/// <summary>The WinUI implementation of what the shared composition needs from a head.</summary>
internal sealed class WinUiPlatformServices(ProviderRegistry providers, Func<ISqlEditor> editorFactory) : IPlatformServices
{
    private readonly WinUiDialogService _dialogs = new();

    /// <inheritdoc />
    public IUiDispatcher Dispatcher { get; } = new WinUiDispatcher(DispatcherQueue.GetForCurrentThread());

    /// <inheritdoc />
    public IDialogService Dialogs => _dialogs;

    /// <inheritdoc />
    public Func<ISqlEditor> EditorFactory { get; } = editorFactory;

    /// <inheritdoc />
    public ProviderRegistry Providers { get; } = providers;

    /// <summary>Gives the dialogs the window they are shown over.</summary>
    /// <param name="window">The main window.</param>
    public void AttachWindow(MainWindow window) => _dialogs.Attach(window);
}
