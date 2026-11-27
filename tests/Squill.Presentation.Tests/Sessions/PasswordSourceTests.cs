using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;
using Squill.Platform.Secrets;
using Squill.Presentation.Query;
using Squill.Presentation.Safety;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Sessions;

public sealed class PasswordSourceTests
{
    private readonly List<string> _log = [];
    private readonly SessionManagerTests.FakeProvider _provider;
    private readonly TrustingStore _store = new();
    private readonly Dialogs _dialogs = new();
    private readonly Runner _runner = new();
    private readonly SessionManager _manager;

    public PasswordSourceTests()
    {
        _provider = new SessionManagerTests.FakeProvider(_log);
        var registry = new ProviderRegistry();
        registry.Register(_provider);
        _manager = new SessionManager(registry, _store, new SessionManagerTests.FakeTunnels(_log), null, null, _dialogs, _runner);
    }

    private static ConnectionProfile Profile(PasswordSource source, string? command = null) =>
        new(Guid.NewGuid(), "Prod", DatabaseDialect.PostgreSql, "db", 5432, "app", "me", null, null, PasswordSource: source, PasswordCommand: command);

    [Fact]
    public async Task Ask_UsesTheTypedPassword_AndNeverTouchesTheStore()
    {
        _dialogs.Typed = "typed";
        _store.Password = "stored";

        await _manager.OpenAsync(Profile(PasswordSource.Ask), CancellationToken.None);

        Assert.Equal(("typed", "Prod"), (_provider.Password, _dialogs.AskedFor));
        Assert.Empty(_store.Writes);
    }

    [Fact]
    public async Task Ask_Cancelled_AbortsTheConnect()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await _manager.OpenAsync(Profile(PasswordSource.Ask), CancellationToken.None));

        Assert.Null(_provider.OpenedWith);
        Assert.Empty(_manager.Sessions);
    }

    [Fact]
    public async Task Command_FirstRunAsksConfirmationShowingTheCommand_ThenRemembersIt()
    {
        _dialogs.Confirm = true;
        _runner.Output = "from-cmd";
        var profile = Profile(PasswordSource.Command, "op read op://v/i/p");

        await _manager.OpenAsync(profile, CancellationToken.None);
        await _manager.OpenAsync(profile, CancellationToken.None);

        Assert.Equal("from-cmd", _provider.Password);
        Assert.Equal(1, _dialogs.Confirmations);
        Assert.Contains("op read op://v/i/p", _dialogs.ConfirmMessage);
        Assert.Equal(["op read op://v/i/p", "op read op://v/i/p"], _runner.Ran);
        Assert.Empty(_store.Writes);
    }

    [Fact]
    public async Task Command_Changed_AsksAgain()
    {
        _dialogs.Confirm = true;
        var profile = Profile(PasswordSource.Command, "op read a");

        await _manager.OpenAsync(profile, CancellationToken.None);
        await _manager.OpenAsync(profile with { PasswordCommand = "op read b" }, CancellationToken.None);

        Assert.Equal(2, _dialogs.Confirmations);
    }

    [Fact]
    public async Task Command_Declined_RunsNothing()
    {
        _dialogs.Confirm = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await _manager.OpenAsync(Profile(PasswordSource.Command, "op read a"), CancellationToken.None));

        Assert.Empty(_runner.Ran);
        Assert.Null(_provider.OpenedWith);
    }

    [Theory]
    [InlineData(PasswordCommandFailure.TimedOut)]
    [InlineData(PasswordCommandFailure.ExitCode)]
    [InlineData(PasswordCommandFailure.EmptyOutput)]
    public async Task Command_Failure_ShowsStandardError(PasswordCommandFailure failure)
    {
        _dialogs.Confirm = true;
        _runner.Failure = new PasswordCommandException(failure, "2", "vault sealed");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await _manager.OpenAsync(Profile(PasswordSource.Command, "vault kv get"), CancellationToken.None));

        Assert.Contains("vault sealed", ex.Message);
        Assert.Null(_provider.OpenedWith);
        Assert.Empty(_manager.Sessions);
    }

    [Fact]
    public async Task Stored_IsTheDefault()
    {
        _store.Password = "stored";

        await _manager.OpenAsync(Profile(PasswordSource.Stored), CancellationToken.None);

        Assert.Equal("stored", _provider.Password);
        Assert.Empty(_runner.Ran);
    }

    private sealed class Runner : IPasswordCommandRunner
    {
        public string Output { get; set; } = "";
        public Exception? Failure { get; set; }
        public List<string> Ran { get; } = [];

        public ValueTask<string> RunAsync(string commandLine, CancellationToken ct)
        {
            Ran.Add(commandLine);
            return Failure is null ? ValueTask.FromResult(Output) : throw Failure;
        }
    }

    private sealed class TrustingStore : IProfileStore
    {
        private readonly HashSet<(Guid, string)> _trusted = [];
        public string? Password { get; set; }
        public List<string> Writes { get; } = [];

        public ValueTask<bool> IsCommandTrustedAsync(Guid id, string commandLine, CancellationToken ct) => ValueTask.FromResult(_trusted.Contains((id, commandLine)));

        public ValueTask TrustCommandAsync(Guid id, string commandLine, CancellationToken ct)
        {
            _trusted.Add((id, commandLine));
            return ValueTask.CompletedTask;
        }

        public ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken ct) => ValueTask.FromResult(Password);
        public ValueTask<string?> GetSshSecretAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);
        public ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct) => throw new NotSupportedException();

        public ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct)
        {
            Writes.Add("save");
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Dialogs : IDialogService
    {
        public string? Typed { get; set; }
        public string? AskedFor { get; private set; }
        public bool Confirm { get; set; }
        public int Confirmations { get; private set; }
        public string? ConfirmMessage { get; private set; }

        public ValueTask<string?> PromptPasswordAsync(string profileName)
        {
            AskedFor = profileName;
            return ValueTask.FromResult(Typed);
        }

        public ValueTask<bool> ConfirmAsync(string title, string message)
        {
            Confirmations++;
            ConfirmMessage = message;
            return ValueTask.FromResult(Confirm);
        }

        public ValueTask<string?> PromptAsync(string title, string message, string? initial) => throw new NotSupportedException();
        public ValueTask<string?> PickSaveFileAsync(string suggestedName, string extension) => throw new NotSupportedException();
        public ValueTask ShowErrorAsync(string title, string message) => throw new NotSupportedException();
        public ValueTask<bool> ConfirmSshHostKeyAsync(Squill.Platform.Ssh.SshHostKeyRequest request) => throw new NotSupportedException();
        public ValueTask<ProductionConfirmationResult> ConfirmProductionAsync(ProductionConfirmationRequest request) => throw new NotSupportedException();
        public ValueTask<TransactionChoice> AskTransactionAsync(TransactionCloseRequest request) => throw new NotSupportedException();
    }
}
