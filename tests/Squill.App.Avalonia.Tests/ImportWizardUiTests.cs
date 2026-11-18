using System.Text;
using Avalonia;
using Avalonia.Controls;
using ColumnDefinition = Squill.Core.ColumnDefinition;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Data;
using Squill.Presentation.Import;
using Squill.Presentation.Localization;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class ImportWizardUiTests : IDisposable
{
    private static readonly ColumnDefinition[] Columns = [new("id", "bigint", false, true, null), new("name", "text", true, false, null), new("amount", "numeric", true, false, null)];
    private static readonly TableInfo Items = new("public", "items", TableKind.Table, Columns, [], []);
    private readonly string _dir = Directory.CreateTempSubdirectory("squill-wizard-ui-").FullName;

    // The wizard may still hold items.csv for a moment on Windows; the temp folder is not worth a failed test.
    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }

    private sealed class Reader : IResultReader
    {
        public IReadOnlyList<ColumnInfo> Columns => [];
        public long RecordsAffected => 1;
        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken) => ValueTask.FromResult<object?[][]?>(null);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Conn : IDatabaseConnection
    {
        public string ServerVersion => "1";
        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken) => ValueTask.FromResult<IResultReader>(new Reader());
        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);
        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static ImportWizardViewModel Wizard()
    {
        var session = new ConnectionSession(new ConnectionProfile(Guid.NewGuid(), "dev", DatabaseDialect.PostgreSql, "h", 1, "db", "u", null, null, ConnectionEnvironment.Development), new Conn(), new SchemaSnapshot("public", [Items]), null);
        return new ImportWizardViewModel([session], new Dialogs());
    }

    private string Csv()
    {
        var path = Path.Combine(_dir, "items.csv");
        File.WriteAllLines(path, ["id,name,amount", "1,a,1.5", "2,b,2.5"], new UTF8Encoding(false));
        return path;
    }

    private static async Task OnWindowAsync(Func<ImportWizardWindow, ImportWizardViewModel, Task> body) =>
        await Headless.Session.Dispatch(async () =>
        {
            using var _ = UiText.Override(UiLanguage.English);
            using var vm = Wizard();
            var window = new ImportWizardWindow(vm);
            window.Show();
            try
            {
                await body(window, vm);
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken);

    private async Task ToMappingAsync(ImportWizardViewModel vm)
    {
        await vm.SetFileAsync(Csv());
        await vm.NextAsync();
        vm.SelectedTable = "public.items";
        await vm.NextAsync();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task Navigation_follows_the_steps_and_the_footer_buttons() => OnWindowAsync(async (window, vm) =>
    {
        Assert.NotEmpty(window.PageTitle);
        Assert.False(window.NextButton.IsEnabled);
        Assert.False(window.BackButton.IsEnabled);

        await vm.SetFileAsync(Csv());
        Dispatcher.UIThread.RunJobs();
        Assert.True(window.NextButton.IsEnabled);
        var source = window.PageTitle;

        await vm.NextAsync();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ImportStep.Target, vm.Step);
        Assert.NotEqual(source, window.PageTitle);
        Assert.True(window.BackButton.IsEnabled);
        Assert.False(window.NextButton.IsEnabled);

        window.BackButton.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ImportStep.Source, vm.Step);
    });

    [Fact]
    public Task Mapping_page_has_one_string_combo_per_file_column() => OnWindowAsync(async (window, vm) =>
    {
        await ToMappingAsync(vm);

        Assert.Equal(ImportStep.Mapping, vm.Step);
        Assert.Equal(3, window.TargetBoxes.Count);
        Assert.All(window.TargetBoxes, box =>
        {
            Assert.All(box.Items, item => Assert.IsType<string>(item));
            Assert.Equal(vm.TargetChoices.Count + 1, box.ItemCount);
        });

        window.TargetBoxes[1].SelectedIndex = 0;
        Assert.Null(vm.Rows[1].Target);
    });

    [Fact]
    public Task Enter_never_starts_the_import_and_the_start_button_is_not_the_default() => OnWindowAsync(async (window, vm) =>
    {
        await ToMappingAsync(vm);
        await vm.NextAsync();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ImportStep.Options, vm.Step);
        Assert.False(window.NextButton.IsDefault);

        window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = window });
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ImportStep.Options, vm.Step);
        Assert.False(vm.IsRunning);
    });

    [Fact]
    public Task Wizard_renders_different_pixels_in_light_and_dark() => OnWindowAsync(async (window, vm) =>
    {
        await ToMappingAsync(vm);
        var frames = new List<byte[]>();
        foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            window.RequestedThemeVariant = variant;
            Dispatcher.UIThread.RunJobs();
            var size = new PixelSize((int)window.Width, (int)window.Height);
            var bitmap = new RenderTargetBitmap(size);
            bitmap.Render(window);
            var bgra = new byte[size.Width * size.Height * 4];
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(bgra, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                bitmap.CopyPixels(new PixelRect(size), handle.AddrOfPinnedObject(), bgra.Length, size.Width * 4);
            }
            finally
            {
                handle.Free();
            }

            frames.Add(bgra);
        }

        Assert.NotEqual(frames[0], frames[1]);
    });
}
