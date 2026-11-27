# P21-106 - Informix: DatabaseDialect, provider registration, connection editor tile, clidriver packaging, AOT check and docs

Status: todo
Depends on: P21-105

## Goal
Informix is a usable engine of the app: a profile opens, runs, streams and fills the explorer in both heads, in a Native AOT build, with `clidriver` located explicitly.

Exists today: P21-103 ... P21-105 (layer, connection, schema reader, tests). Pattern: how Oracle was registered (composition roots of both heads, connection editor tile and engine icon); check it in the code first.

## Design decisions (final)
- `DatabaseDialect.Informix` in Core; the Informix provider and descriptor registered in both heads' composition roots; profile fields host, port (default 9089, the DRDA listener `drsoctcp`), database, user (no server name needed over DRDA). A server without a DRDA listener gives a hint text through `UiText` (both catalogs).
- Connection editor tile and engine icon in both heads (copy the Oracle tile; small). `DatabaseDialect.Informix` needs a tokenizer and a `ParameterScanner` case that recognise `:name` (copy the Oracle one) and `TransactionSql` texts (`BEGIN`, `COMMIT`, `ROLLBACK`; the connection of P21-104 runs them on the ODBC connection); the `@name` to `?` rewrite already lives in `Squill.Data.Informix` (P21-104 deviation).
- Packaging: the app ships the `clidriver` folder (from the `Net.IBM.Data.Db2` package files, only the redistributable files named in its `license` text, notices kept, IBM licence text in the installer; about 125 MB on disk, 46 MB downloaded). OWNER DECISION 2026-11-27: shipping `clidriver` is accepted; writing to the Windows registry (`db2cli install -setup`) and any global install are NOT approved. The installer change is made in the smallest way the existing installer allows; if it cannot carry it without a larger change, stop and propose a card.
- Not built here (list as `P21-107+` proposals in the Done notes): designer DDL, grid editing, execution plans, transactions screen, terminal (`dbaccess`).

## Out of scope
- SQL dialect (P21-12), a generic ODBC provider (P21-15), Db2 (P21-13).

## Tasks
- [ ] Tests first: registration tests (provider lookup, descriptor defaults, `UiText` parity and used keys, string scanner), profile round trip.
- [ ] Both heads: tile and icon; `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings.
- [ ] AOT: `eng/check-aot-warnings.ps1 -Head WinUI` baseline warnings only with the provider linked in, and the layer run in the published exe against the container (connect, a query, a cancel) with `SQUILL_CLIDRIVER` or the shipped folder; write `not driven (<why>)` for anything not run.
- [ ] Docs: README "Supported databases" row with the measured verdict, `docs/architecture.md` / ADR 0008 row, link from `docs/poc/informix-driver.md`, CHANGELOG line, the licence note.

## Acceptance criteria
- Recipe 2 and recipe 3 acceptance for the parts touched; `-Engine informix` green; WinUI AOT publish baseline only.

## Risks
- IBM redistribution terms: the installer must carry the licence text and notices.
- Size: 125 MB more on disk; consider shipping `clidriver` as an optional component if the installer supports it.

## Suggested commit message
Register Informix as an engine with clidriver packaging
