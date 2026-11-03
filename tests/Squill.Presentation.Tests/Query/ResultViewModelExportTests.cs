using Squill.Core;
using Squill.Data;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.Presentation.Tests.Query;

public sealed class ResultViewModelExportTests
{
    private sealed class EmptyReader : IResultReader
    {
        public IReadOnlyList<ColumnInfo> Columns { get; } = [new("c", "int", typeof(int), true, 0)];
        public long RecordsAffected => -1;

        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken) => ValueTask.FromResult<object?[][]?>(null);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ThrowingExporter : IResultExporter
    {
        public string FileExtension => "csv";

        public ValueTask ExportAsync(IGridDataSource source, Stream destination, CancellationToken ct) =>
            throw new InvalidOperationException("developer guard");
    }

    [Theory]
    [InlineData(UiLanguage.French, "Export impossible", "Le résultat est encore en cours de chargement et ne peut pas être exporté.")]
    [InlineData(UiLanguage.English, "Export failed", "The result is still loading and cannot be exported.")]
    public async Task Export_WhileLoading_ShowsTheCatalogMessageNotTheGuard(UiLanguage language, string title, string message)
    {
        using var _ = UiText.Override(language);
        var dialogs = new FakeDialogService { SavePathResult = Path.GetTempFileName() };
        var result = ResultViewModel.ForGrid("r", new StreamingGridDataSource(new EmptyReader(), 10, new ImmediateDispatcher()), dialogs);

        await result.ExportCommand.ExecuteAsync(new ThrowingExporter());

        Assert.Equal((title, message), Assert.Single(dialogs.Errors));
    }
}
