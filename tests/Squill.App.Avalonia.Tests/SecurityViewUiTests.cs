using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Squill.App.Avalonia.Controls;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Core.Security;
using Squill.Data;
using Squill.Presentation;
using Squill.Presentation.Security;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class SecurityViewUiTests
{
    private static readonly RoleInfo Analysts = new("analysts", false, false, true, [], ["alice"]);
    private static readonly RoleInfo Alice = new("alice", true, false, true, ["analysts"], []);

    private sealed class Reader(int extraRows = 0) : ISecurityReader
    {
        public ValueTask<IReadOnlyList<RoleInfo>> ReadRolesAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<RoleInfo>>([Analysts, Alice]);

        public ValueTask<IReadOnlyList<string>> ListSchemasAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<string>>(["public"]);

        public ValueTask<IReadOnlyList<ObjectPrivileges>> ReadPrivilegesAsync(string role, IReadOnlyList<string> schemas, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ObjectPrivileges>>(
            [
                new("public", "orders", ObjectKind.Table, new Dictionary<PrivilegeKind, PrivilegeState> { [PrivilegeKind.Select] = PrivilegeState.Granted, [PrivilegeKind.Insert] = PrivilegeState.Inherited("analysts") }),
                new("public", "items", ObjectKind.Table, new Dictionary<PrivilegeKind, PrivilegeState>()),
                .. Enumerable.Range(0, extraRows).Select(i => new ObjectPrivileges("public", "t" + i, ObjectKind.Table, new Dictionary<PrivilegeKind, PrivilegeState>())),
            ]);
    }

    private sealed class Connection : IDatabaseConnection
    {
        public string ServerVersion => "16.1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static async Task WithViewAsync(Func<Window, SecurityView, PrivilegeMatrixCanvas, SecurityViewModel, Task> body, int extraRows = 0)
    {
        await Headless.Session.Dispatch(async () =>
        {
            var profile = new ConnectionProfile(Guid.NewGuid(), "main", DatabaseDialect.PostgreSql, "h", 1, "appdb", "u", null, null, ConnectionEnvironment.Staging);
            using var vm = new SecurityViewModel(profile, new Reader(extraRows), new Connection(), null, new Dialogs());
            await vm.LoadAsync();
            await vm.SelectRoleAsync(Alice);
            var view = new SecurityView { DataContext = vm };
            var window = new Window { Content = view, Width = 1400, Height = 800 };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                var matrix = view.GetLogicalDescendants().OfType<PrivilegeMatrixCanvas>().Single();
                await body(window, view, matrix, vm);
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(PrivilegeCellDisplay.Granted, "SquillAccentBrush", "✓")]
    [InlineData(PrivilegeCellDisplay.Inherited, "SquillFg3Brush", "✓")]
    [InlineData(PrivilegeCellDisplay.PendingGrant, null, "")]
    [InlineData(PrivilegeCellDisplay.PendingRevoke, null, "−")]
    [InlineData(PrivilegeCellDisplay.Empty, null, "")]
    public void Cells_are_filled_only_when_held_and_pending_revoke_shows_a_minus(PrivilegeCellDisplay display, string? fill, string glyph)
    {
        Assert.Equal(fill, SecurityFormat.FillBrush(display));
        Assert.Equal(glyph, SecurityFormat.Glyph(display));
    }

    [Fact]
    public void Hit_test_finds_the_cell_and_ignores_the_name_column_and_the_outside()
    {
        Assert.Equal((1, 2), SecurityFormat.HitTest(280 + 92 * 2 + 5, 28 + 3, 280, 92, 28, 3, 7));
        Assert.Null(SecurityFormat.HitTest(100, 10, 280, 92, 28, 3, 7));
        Assert.Null(SecurityFormat.HitTest(300, 28 * 3, 280, 92, 28, 3, 7));
    }

    [Fact]
    public async Task The_view_paints_an_opaque_background_so_the_query_view_underneath_cannot_show_through()
    {
        await WithViewAsync((_, view, _, _) =>
        {
            Assert.NotNull(view.Background);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Clicking_a_cell_toggles_it_through_the_view_model()
    {
        await WithViewAsync((_, _, matrix, vm) =>
        {
            Assert.Equal(2, vm.Rows.Count);
            var x = PrivilegeMatrixCanvas.NameWidth + (PrivilegeMatrixCanvas.CellWidth * (int)PrivilegeKind.Insert) + 10;
            var y = PrivilegeMatrixCanvas.HeaderHeight + PrivilegeMatrixCanvas.RowHeight + 5; // second row ("items"), Insert column
            var before = vm.Rows[1].Cells[(int)PrivilegeKind.Insert].Display;

            matrix.RaiseEvent(new PointerPressedEventArgs(matrix, new Pointer(0, PointerType.Mouse, true), matrix, new Point(x, y), 0, new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
            Dispatcher.UIThread.RunJobs();

            Assert.NotEqual(before, vm.Rows[1].Cells[(int)PrivilegeKind.Insert].Display);
            Assert.Equal(1, vm.ChangeCount);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Arrow_keys_move_the_focus_and_space_toggles_the_focused_cell()
    {
        await WithViewAsync((window, _, matrix, vm) =>
        {
            matrix.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Assert.Equal((1, 1), (matrix.FocusRow, matrix.FocusColumn));

            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, vm.ChangeCount);
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(398, false)]
    [InlineData(399, true)]
    public async Task The_rows_cut_notice_shows_only_beyond_the_limit_and_row_401_cannot_be_hit(int extraRows, bool cut)
    {
        await WithViewAsync((_, view, matrix, vm) =>
        {
            var notice = view.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Name == "RowsCutNotice");
            Assert.Equal(cut, notice.IsVisible);
            Assert.Equal(cut ? vm.RowsCutText : "", cut ? notice.Text : "");
            Assert.Equal(Math.Min(vm.Rows.Count, SecurityViewModel.MaxShownRows), matrix.RowCount);
            var y = PrivilegeMatrixCanvas.HeaderHeight + (PrivilegeMatrixCanvas.RowHeight * SecurityViewModel.MaxShownRows) + 5; // row 401
            Assert.Null(SecurityFormat.HitTest(PrivilegeMatrixCanvas.NameWidth + 10, y - PrivilegeMatrixCanvas.HeaderHeight, PrivilegeMatrixCanvas.NameWidth, PrivilegeMatrixCanvas.CellWidth, PrivilegeMatrixCanvas.RowHeight, matrix.RowCount, 7));
            return Task.CompletedTask;
        }, extraRows);
    }

    [Fact]
    public async Task Security_renders_different_pixels_in_light_and_dark()
    {
        await WithViewAsync((window, _, _, _) =>
        {
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
            return Task.CompletedTask;
        });
    }
}
