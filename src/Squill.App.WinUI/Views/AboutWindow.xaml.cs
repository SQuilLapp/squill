using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Squill.Presentation.Localization;
using Squill.Presentation.Updates;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace Squill.App.WinUI.Views;

/// <summary>Modal window that shows what is installed and drives the update check; Esc closes it.</summary>
public sealed partial class AboutWindow : Window
{
    private const int LogicalWidth = 420;
    private const int LogicalHeight = 360;
    private const int OwnerIndex = -8; // GWLP_HWNDPARENT

    private readonly Window _owner;
    private readonly AboutViewModel _about;

    /// <summary>Creates the window bound to the About view-model.</summary>
    /// <param name="owner">The main window; it stays disabled while this window is open.</param>
    /// <param name="about">What to show.</param>
    public AboutWindow(Window owner, AboutViewModel about)
    {
        InitializeComponent();
        _owner = owner;
        _about = about;
        CaptionBar.Follow(this, Root, (FrameworkElement)owner.Content);
        Title = UiText.Get("about.title");
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "squill.ico"));
        Logo.Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "squill-32.png")));
        ProductName.Text = about.ProductName;
        VersionText.Text = about.VersionText;
        ChannelText.Text = about.ChannelText;
        InstallKindText.Text = about.InstallKindText;
        RuntimeText.Text = about.RuntimeText;
        LicenseText.Text = about.LicenseText;
        DataFolderPath.Text = about.DataFolder;
        RepositoryLink.NavigateUri = new Uri(about.RepositoryUrl);
        NameControls();
        if (about.ShowWhatsNew)
        {
            WhatsNewText.Text = about.WhatsNewText;
            NotesHost.Content = ReleaseNotesPresenter.Build(about.Notes, Root);
            WhatsNew.Visibility = Visibility.Visible;
        }

        var escape = new KeyboardAccelerator { Key = VirtualKey.Escape };
        escape.Invoked += (_, args) =>
        {
            args.Handled = true;
            Close();
        };
        Root.KeyboardAccelerators.Add(escape);
        about.Updates.PropertyChanged += OnUpdatesChanged;
        RefreshStatus();
        PlaceModal(owner);
        Closed += (_, _) =>
        {
            about.Updates.PropertyChanged -= OnUpdatesChanged;
            _owner.Activate();
        };
    }

    private void NameControls()
    {
        AutomationProperties.SetName(OpenFolderLink, UiText.Get("about.openDataFolder"));
        AutomationProperties.SetName(RepositoryLink, UiText.Get("about.repository"));
        AutomationProperties.SetName(CheckButton, UiText.Get("update.button.checkNow"));
        AutomationProperties.SetName(RestartButton, UiText.Get("update.button.restart"));
        AutomationProperties.SetName(CloseButton, UiText.Get("about.close"));
    }

    private void OnUpdatesChanged(object? sender, PropertyChangedEventArgs e) => RefreshStatus();

    private void RefreshStatus()
    {
        var updates = _about.Updates;
        StatusText.Text = updates.StatusText;
        CheckButton.IsEnabled = updates.CanCheck;
        RestartButton.Visibility = updates.CanRestart ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnCheckNow(object sender, RoutedEventArgs e) => _about.Updates.CheckNowCommand.Execute(null);

    private void OnRestart(object sender, RoutedEventArgs e) => _about.Updates.RestartToUpdateCommand.Execute(null);

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnOpenDataFolder(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(_about.DataFolder))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_about.DataFolder}\"") { UseShellExecute = true })?.Dispose();
    }

    private void PlaceModal(Window owner)
    {
        var ownerHandle = WindowNative.GetWindowHandle(owner);
        var handle = WindowNative.GetWindowHandle(this);
        Win32.SetWindowLongPtr(handle, OwnerIndex, ownerHandle);
        var scale = Win32.GetDpiForWindow(handle) / 96.0;
        var size = new SizeInt32((int)(LogicalWidth * scale), (int)(LogicalHeight * scale));
        AppWindow.Resize(size);
        var at = owner.AppWindow.Position;
        var around = owner.AppWindow.Size;
        AppWindow.Move(new PointInt32(at.X + Math.Max(0, (around.Width - size.Width) / 2), at.Y + Math.Max(0, (around.Height - size.Height) / 2)));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
            presenter.IsModal = true;
        }
    }

    private static partial class Win32
    {
        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        internal static partial nint SetWindowLongPtr(nint window, int index, nint value);

        [LibraryImport("user32.dll")]
        internal static partial uint GetDpiForWindow(nint window);
    }
}
