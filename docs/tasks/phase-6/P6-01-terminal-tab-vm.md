# P6-01 - Presentation: terminal tab view-model

Status: done
Depends on: P5-12

## Goal
Headless model for shell/CLI tabs (`psql`, `mysql`, `sqlite3`, local shell), independent of LoomTerminal (ADR 0002).

## Design decisions (final)
- `interface ITerminalHost { void Start(TerminalLaunch launch); void Terminate(); event EventHandler? Exited; }` and `sealed record TerminalLaunch(string FileName, IReadOnlyList<string> Arguments, string? WorkingDirectory, IReadOnlyDictionary<string, string> Environment)` in `Squill.Presentation.Terminal`.
- `TerminalLauncher.ForProfile(ConnectionProfile p, string? password, Func<string, string?> findOnPath)` returns a launch: PostgreSql => `psql -h host -p port -U user dbname` with `PGPASSWORD` in the environment; MySql => `mysql -h host -P port -u user dbname` with `MYSQL_PWD`; Sqlite => `sqlite3 <file>`; SqlServer => `sqlcmd -S host,port -U user -d db` with `SQLCMDPASSWORD`; `null` (and a user-facing reason) when the executable is not found on PATH. `TerminalLauncher.LocalShell()` => `pwsh`/`powershell` on Windows, `$SHELL` or `/bin/sh` elsewhere. Passwords never appear in `Arguments`.
- `TerminalTabViewModel(ITerminalHost host, TerminalLaunch launch)`: `Title`, `IsRunning`, `StartCommand`, `StopCommand`, restarts on request; added to `WorkspaceViewModel` through `NewTerminalCommand(profile?)` as a separate `Terminals` collection.

## Out of scope
- Rendering (P6-02).

## Tasks
- [x] Implement the types and the workspace command.
- [x] Tests: launch construction per dialect, password only in env, missing executable returns null + reason, workspace command with a fake host.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add terminal tab view-model and launcher
