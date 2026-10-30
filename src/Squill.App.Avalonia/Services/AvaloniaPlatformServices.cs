using Squill.Data;
using Squill.Presentation;
using Squill.Presentation.Editor;

namespace Squill.App.Avalonia.Services;

/// <summary>The Avalonia implementation of what the shared composition needs from a head.</summary>
internal sealed class AvaloniaPlatformServices(ProviderRegistry providers, Func<ISqlEditor> editorFactory) : IPlatformServices
{
    /// <inheritdoc />
    public IUiDispatcher Dispatcher { get; } = new AvaloniaDispatcher();

    /// <inheritdoc />
    public IDialogService Dialogs { get; } = new AvaloniaDialogService();

    /// <inheritdoc />
    public Func<ISqlEditor> EditorFactory { get; } = editorFactory;

    /// <inheritdoc />
    public ProviderRegistry Providers { get; } = providers;
}
