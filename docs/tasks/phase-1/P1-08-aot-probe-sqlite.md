# P1-08 - AOT probe for SQLite and the driver report

Status: todo
Depends on: P1-07

## Goal
Prove SQLite works under Native AOT and create the report that the other driver cards extend (risk R3).

## Design decisions (final)
- `tools/Squill.AotProbe.Sqlite` (Exe, `PublishAot=true`, references `Squill.Data.Sqlite`): opens `:memory:`, creates a table, inserts two rows, reads them through `IDatabaseConnection`/`IResultReader`, prints `OK <rows>` and exits 0, otherwise prints the exception and exits 1.
- `docs/poc/aot-drivers.md` with a table `Driver | Package version | AOT publish | Runtime check | Verdict | Caveats`, verdict in {compatible, compatible with caveats, incompatible}. This card adds the SQLite row.

## Out of scope
- Other drivers (P1-09, P1-11, P1-13).

## Tasks
- [ ] Create the probe and add it to `Squill.slnx` under `/tools/`.
- [ ] Publish win-x64, run the produced exe, fill the SQLite row with the real result.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk dotnet publish tools/Squill.AotProbe.Sqlite -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Running `tools/Squill.AotProbe.Sqlite/bin/Release/net11.0/win-x64/publish/Squill.AotProbe.Sqlite.exe` prints `OK 2` and exits 0 (paste the output into the report).

## Suggested commit message
Add SQLite AOT probe and driver report
