using Xunit;
using Squill.Core;

namespace Squill.Data.Tests;

public sealed class ProviderRegistryTests
{
    private static FakeDatabaseProvider Fake(DatabaseDialect d) => new(d, [], []);

    [Fact]
    public void Get_RegisteredDialect_ReturnsProvider()
    {
        var registry = new ProviderRegistry();
        var provider = Fake(DatabaseDialect.Sqlite);
        registry.Register(provider);
        Assert.Same(provider, registry.Get(DatabaseDialect.Sqlite));
    }

    [Fact]
    public void Get_MissingDialect_ThrowsNotSupportedWithDialectName()
    {
        var ex = Assert.Throws<NotSupportedException>(() => new ProviderRegistry().Get(DatabaseDialect.MySql));
        Assert.Contains("MySql", ex.Message);
    }

    [Fact]
    public void Register_SameDialectTwice_Throws()
    {
        var registry = new ProviderRegistry();
        registry.Register(Fake(DatabaseDialect.Sqlite));
        Assert.Throws<InvalidOperationException>(() => registry.Register(Fake(DatabaseDialect.Sqlite)));
    }
}
