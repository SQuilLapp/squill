using Squill.Core;
using Squill.Platform.Profiles;
using Squill.Platform.Secrets;
using Xunit;

namespace Squill.Platform.Tests;

public sealed class PasswordCommandTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "squill-tests-" + Guid.NewGuid().ToString("N"));
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    // The scripts are run by an explicit shell chosen by the test: the runner itself never uses one.
    private static string Script(string script) => OperatingSystem.IsWindows()
        ? $"powershell.exe -NoProfile -NonInteractive -Command \"{script}\""
        : $"sh -c \"{script}\"";

    [Fact]
    public void Split_GroupsQuotesAndKeepsBackslashesAndMetacharactersLiteral()
    {
        Assert.Equal(["op", "read", "op://v/i;rm -rf x", @"C:\a b\c"], PasswordCommandRunner.Split(@"op  read ""op://v/i;rm -rf x"" ""C:\a b\c"""));
        Assert.Equal(["a", ""], PasswordCommandRunner.Split("a \"\""));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("op \"unclosed")]
    public void Split_InvalidLine_Throws(string line) =>
        Assert.Equal(PasswordCommandFailure.Invalid, Assert.Throws<PasswordCommandException>(() => PasswordCommandRunner.Split(line)).Failure);

    [Fact]
    public async Task RunAsync_ReturnsTrimmedStandardOutput() =>
        Assert.Equal("s3 cret", await new PasswordCommandRunner().RunAsync(Script(OperatingSystem.IsWindows() ? "Write-Output '  s3 cret  '" : "echo '  s3 cret  '"), _ct));

    [Fact]
    public async Task RunAsync_ShellMetacharactersInArguments_AreNotInterpreted()
    {
        // A shell would run the second command; the runner passes the text as one argument.
        var runner = new PasswordCommandRunner();
        var ex = await Assert.ThrowsAsync<PasswordCommandException>(async () => await runner.RunAsync("squill-no-such-program-x ; echo pwned", _ct));
        Assert.Equal(PasswordCommandFailure.StartFailed, ex.Failure);
    }

    [Fact]
    public async Task RunAsync_NonZeroExit_ReportsCodeAndStandardErrorButNotStandardOutput()
    {
        var script = OperatingSystem.IsWindows()
            ? "[Console]::Out.Write('LEAK'); [Console]::Error.Write('denied'); exit 3"
            : "printf LEAK; printf denied >&2; exit 3";
        var ex = await Assert.ThrowsAsync<PasswordCommandException>(async () => await new PasswordCommandRunner().RunAsync(Script(script), _ct));
        Assert.Equal((PasswordCommandFailure.ExitCode, "3", "denied"), (ex.Failure, ex.Detail, ex.StandardError));
        Assert.DoesNotContain("LEAK", ex.ToString());
    }

    [Fact]
    public async Task RunAsync_EmptyOutput_Throws()
    {
        var ex = await Assert.ThrowsAsync<PasswordCommandException>(async () => await new PasswordCommandRunner().RunAsync(Script(OperatingSystem.IsWindows() ? "Write-Output '   '" : "echo '   '"), _ct));
        Assert.Equal(PasswordCommandFailure.EmptyOutput, ex.Failure);
    }

    [Fact]
    public async Task RunAsync_Timeout_KillsTheProgram()
    {
        var ex = await Assert.ThrowsAsync<PasswordCommandException>(async () =>
            await new PasswordCommandRunner(TimeSpan.FromMilliseconds(500)).RunAsync(Script(OperatingSystem.IsWindows() ? "Start-Sleep 30" : "sleep 30"), _ct));
        Assert.Equal(PasswordCommandFailure.TimedOut, ex.Failure);
    }

    [Fact]
    public async Task RunAsync_ClosedStandardInput_DoesNotWaitForInput() =>
        Assert.Equal("eof", await new PasswordCommandRunner().RunAsync(Script(OperatingSystem.IsWindows() ? "[Console]::In.ReadToEnd() | Out-Null; Write-Output eof" : "cat >/dev/null; echo eof"), _ct));

    [Fact]
    public async Task Store_TrustsOnlyTheExactConfirmedCommand_AndNeverWritesItInTheProfileFile()
    {
        var secrets = new InMemorySecretStore();
        using var store = new JsonProfileStore(new AppPaths(_dir), secrets);
        var profile = new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.PostgreSql, "h", 1, "d", "u", null, null, PasswordSource: PasswordSource.Command, PasswordCommand: "op read x");
        await store.SaveAsync(profile, null, null, _ct);

        Assert.False(await store.IsCommandTrustedAsync(profile.Id, "op read x", _ct));
        await store.TrustCommandAsync(profile.Id, "op read x", _ct);
        Assert.True(await store.IsCommandTrustedAsync(profile.Id, "op read x", _ct));
        Assert.False(await store.IsCommandTrustedAsync(profile.Id, "op read y", _ct));
        Assert.Equal([profile], await store.ListAsync(_ct));
        Assert.Null(await store.GetPasswordAsync(profile.Id, _ct));

        await store.DeleteAsync(profile.Id, _ct);
        Assert.False(await store.IsCommandTrustedAsync(profile.Id, "op read x", _ct));
    }
}
