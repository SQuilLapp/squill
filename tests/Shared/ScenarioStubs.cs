using Squill.Core;
using Squill.Platform.Profiles;
using Squill.Platform.Settings;
using Squill.Platform.Ssh;

namespace Squill.Data.Tests.Scenarios;

/// <summary>Settings store that always returns the defaults and saves nothing.</summary>
internal sealed class ScenarioSettings : ISettingsStore
{
    public ValueTask<AppSettings> LoadAsync(CancellationToken ct) => ValueTask.FromResult(AppSettings.Default);

    public ValueTask SaveAsync(AppSettings settings, CancellationToken ct) => ValueTask.CompletedTask;
}

/// <summary>Profile store with no profiles that hands back one fixed password.</summary>
internal sealed class ScenarioProfileStore(string? password) : IProfileStore
{
    public ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<ConnectionProfile>>([]);

    public ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct) => ValueTask.CompletedTask;

    public ValueTask DeleteAsync(Guid id, CancellationToken ct) => ValueTask.CompletedTask;

    public ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken ct) => ValueTask.FromResult(password);

    public ValueTask<string?> GetSshSecretAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);
}

/// <summary>SSH tunnel factory for scenarios that never tunnel.</summary>
internal sealed class ScenarioTunnelFactory : ISshTunnelFactory
{
    public ValueTask<ISshTunnel> OpenAsync(SshTunnelSettings settings, string? secret, string remoteHost, int remotePort, CancellationToken ct) =>
        throw new NotSupportedException();
}
