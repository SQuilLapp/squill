using System.Diagnostics;
using Squill.Data;
using Xunit;

namespace Squill.Data.RoundTrip;

/// <summary>Cancel and command-timeout checks shared by every driver test project.</summary>
internal static class CancelScenario
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

    /// <summary>Cancels a slow statement after 500 ms; it must throw <see cref="OperationCanceledException"/> within 5 s and leave the connection usable.</summary>
    public static async Task CancelSlowStatementAsync(IDatabaseConnection connection, string slowSql)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var clock = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await using var reader = await connection.ExecuteAsync(slowSql, [], cts.Token);
            await reader.ReadAllAsync(10, cts.Token);
        });
        Assert.True(clock.Elapsed < Limit, $"Cancellation took {clock.Elapsed}.");
        await AssertUsableAsync(connection);
    }

    /// <summary>Runs a slow statement on a connection opened with a 1 s command timeout; the driver must fail within 5 s and leave the connection usable.</summary>
    public static async Task TimeoutSlowStatementAsync(IDatabaseConnection connection, string slowSql)
    {
        var clock = Stopwatch.StartNew();
        var error = await Record.ExceptionAsync(async () =>
        {
            await using var reader = await connection.ExecuteAsync(slowSql, [], CancellationToken.None);
            await reader.ReadAllAsync(10, CancellationToken.None);
        });
        Assert.NotNull(error);
        Assert.IsNotType<OperationCanceledException>(error);
        Assert.True(clock.Elapsed < Limit, $"The timeout took {clock.Elapsed}.");
        await AssertUsableAsync(connection);
    }

    /// <summary>Cancels after the first batch of a huge result; the cancelled read and the reader disposal must both finish within 5 s and the connection must stay usable.</summary>
    public static async Task CancelWhileReadingAsync(IDatabaseConnection connection, string hugeSql)
    {
        using var cts = new CancellationTokenSource();
        var reader = await connection.ExecuteAsync(hugeSql, [], cts.Token);
        Assert.NotNull(await reader.ReadBatchAsync(100, cts.Token));
        await cts.CancelAsync();
        var clock = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await reader.ReadBatchAsync(100, cts.Token));
        await reader.DisposeAsync();
        Assert.True(clock.Elapsed < Limit, $"Disposing the cancelled reader took {clock.Elapsed}.");
        await AssertUsableAsync(connection);
    }

    private static async Task AssertUsableAsync(IDatabaseConnection connection)
    {
        await using var reader = await connection.ExecuteAsync("select 1", [], CancellationToken.None);
        var rows = await reader.ReadAllAsync(10, CancellationToken.None);
        Assert.Equal(1, Convert.ToInt32(Assert.Single(rows)[0], System.Globalization.CultureInfo.InvariantCulture));
    }
}
