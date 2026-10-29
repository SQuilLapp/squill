# P6-04 - Project README and build instructions

Status: todo
Depends on: P6-03

## Goal
Replace the one-line README with what a contributor and a user need.

## Design decisions (final)
- `README.md` sections: what SQuilL is (SQL client, .NET 11 RC3, Native AOT, WinUI 3 on Windows and Avalonia on macOS/Linux), supported databases (PostgreSQL, MySQL/MariaDB, SQL Server, SQLite) and the AOT verdict per driver copied from `docs/poc/aot-drivers.md`, build and test commands (`rtk`-free plain `dotnet` commands, the Docker-gated test variable, optional `Directory.Build.local.props` for LoomEditor/LoomTerminal), project layout (one line per project of the dependency graph in `docs/tasks/README.md`), links to `docs/architecture.md` and the ADRs, license.
- Update `docs/architecture.md` 'Proposed order of work' to point at `docs/tasks/README.md` and mark risks R1-R8 with their outcome (links to the PoC reports), in the same table.

## Out of scope
- Marketing copy, screenshots.

## Tasks
- [ ] Write the README and update the architecture note.
- [ ] Check every command in the README by running it (build, test, TokenGen).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- All relative links in `README.md` and `docs/architecture.md` resolve to existing files.

## Suggested commit message
Write README and update architecture note
