using Squill.Core;

namespace Squill.Data;

/// <summary>Maps each dialect to the provider that serves it.</summary>
public sealed class ProviderRegistry
{
    private readonly Dictionary<DatabaseDialect, IDatabaseProvider> _providers = [];

    /// <summary>Adds a provider; a second provider for the same dialect throws <see cref="InvalidOperationException"/>.</summary>
    public void Register(IDatabaseProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (!_providers.TryAdd(provider.Dialect, provider))
            throw new InvalidOperationException($"A provider is already registered for {provider.Dialect}.");
    }

    /// <summary>Returns the provider for the dialect, or throws <see cref="NotSupportedException"/>.</summary>
    public IDatabaseProvider Get(DatabaseDialect dialect) =>
        _providers.TryGetValue(dialect, out var provider)
            ? provider
            : throw new NotSupportedException($"No provider is registered for {dialect}.");
}
