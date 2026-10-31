using Avalonia;
using Avalonia.Headless;
using Squill.App.Avalonia.Editors;
using Squill.Core;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.App.Avalonia.Tests;

public sealed class AvaloniaSqlEditorTests
{
    private sealed class TestApp : Application;

    private static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(TestApp));

    private sealed class FakeProvider : ISqlCompletionProvider
    {
        public SqlRequest? Request { get; private set; }

        public IReadOnlyList<CompletionItem> GetCompletions(SqlRequest request)
        {
            Request = request;
            return [new CompletionItem("users", CompletionKind.Class, "users", null, null, new TextRange(request.Offset, 0))];
        }
    }

    [Fact]
    public async Task Text_SetAndRead_RoundTripsAndRaisesChanged()
    {
        await Session.Dispatch(() =>
        {
            var editor = new AvaloniaSqlEditor();
            var raised = 0;
            editor.TextChanged += (_, _) => raised++;

            editor.Text = "select 1";

            Assert.Equal("select 1", editor.Text);
            Assert.True(raised > 0);
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CaretAndSelection_Set_AreMapped()
    {
        await Session.Dispatch(() =>
        {
            var editor = new AvaloniaSqlEditor { Text = "select 1" };

            editor.CaretOffset = 3;
            Assert.Equal(3, editor.CaretOffset);

            editor.Selection = new TextRange(2, 4);
            Assert.Equal(new TextRange(2, 4), editor.Selection);
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RequestCompletions_AfterFrom_AsksProviderAtCaret()
    {
        await Session.Dispatch(() =>
        {
            var editor = new AvaloniaSqlEditor { Text = "select * from " };
            var provider = new FakeProvider();
            editor.SetCompletionProvider(provider);
            editor.CaretOffset = editor.Text.Length;

            var items = editor.RequestCompletions();

            Assert.Equal("select * from ", provider.Request!.Text);
            Assert.Equal(14, provider.Request.Offset);
            Assert.Single(items);
        }, TestContext.Current.CancellationToken);
    }
}
