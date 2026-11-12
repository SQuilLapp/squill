using Squill.Core.Transactions;
using Xunit;

namespace Squill.Core.Tests;

public sealed class TransactionProbeResultTests
{
    [Fact]
    public void Unknown_HasNoTransactionInfoAndIsNotFailed()
    {
        Assert.Null(TransactionProbeResult.Unknown.InTransaction);
        Assert.False(TransactionProbeResult.Unknown.Failed);
    }
}
