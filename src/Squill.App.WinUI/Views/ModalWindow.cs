using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace Squill.App.WinUI.Views;

/// <summary>Makes a window a modal child of the main window, centered over it.</summary>
internal static partial class ModalWindow
{
    private const int OwnerIndex = -8; // GWLP_HWNDPARENT

    /// <summary>Owns, sizes, centers and locks a window.</summary>
    /// <param name="window">The window to place.</param>
    /// <param name="owner">The main window; it stays disabled while <paramref name="window"/> is open.</param>
    /// <param name="logicalWidth">Width in logical pixels.</param>
    /// <param name="logicalHeight">Height in logical pixels.</param>
    /// <param name="resizable">Whether the user may resize and maximize the window.</param>
    public static void Place(Window window, Window owner, int logicalWidth, int logicalHeight, bool resizable)
    {
        var ownerHandle = WindowNative.GetWindowHandle(owner);
        var handle = WindowNative.GetWindowHandle(window);
        SetWindowLongPtr(handle, OwnerIndex, ownerHandle);
        var scale = GetDpiForWindow(handle) / 96.0;
        var size = new SizeInt32((int)(logicalWidth * scale), (int)(logicalHeight * scale));
        window.AppWindow.Resize(size);
        var at = owner.AppWindow.Position;
        var around = owner.AppWindow.Size;
        window.AppWindow.Move(new PointInt32(at.X + Math.Max(0, (around.Width - size.Width) / 2), at.Y + Math.Max(0, (around.Height - size.Height) / 2)));
        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
            if (!resizable)
            {
                presenter.IsMaximizable = false;
                presenter.IsResizable = false;
            }

            presenter.IsModal = true;
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr(nint window, int index, nint value);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(nint window);
}
