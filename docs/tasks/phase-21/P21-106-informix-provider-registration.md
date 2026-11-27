# P21-106 - Informix: DatabaseDialect, provider registration, connection editor tile, clidriver packaging, AOT check and docs

Status: done
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
- [x] Tests first: registration tests (provider lookup, descriptor defaults, `UiText` parity and used keys, string scanner), profile round trip.
- [x] Both heads: tile and icon; `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings.
- [x] AOT: `eng/check-aot-warnings.ps1 -Head WinUI` baseline warnings only with the provider linked in, and the layer run in the published exe against the container (connect, a query, a cancel) with `SQUILL_CLIDRIVER` or the shipped folder; write `not driven (<why>)` for anything not run.
- [x] Docs: README "Supported databases" row with the measured verdict, `docs/architecture.md` / ADR 0008 row, link from `docs/poc/informix-driver.md`, CHANGELOG line, the licence note.

## Acceptance criteria
- Recipe 2 and recipe 3 acceptance for the parts touched; `-Engine informix` green; WinUI AOT publish baseline only.

## Risks
- IBM redistribution terms: the installer must carry the licence text and notices.
- Size: 125 MB more on disk; consider shipping `clidriver` as an optional component if the installer supports it.

## Suggested commit message
Register Informix as an engine with clidriver packaging

## Done notes
- Shipped: `DatabaseDialect.Informix`, `InformixDatabaseProvider` (registered in both heads), connection editor tile and engine icon in both heads (default port 9089, DRDA hint text in both catalogs), `InformixTokenizer` (`{ }` comments, `?` and `:name` parameters; keywords are still the ANSI set, so P21-12 keeps the splitter, `FIRST n`, quoting and catalogue parts), `InformixDdlGenerator` (only `DROP TABLE`; create and alter throw `NotSupportedException`, the designer is a later card), `TransactionSql` texts, `eng/Clidriver.targets` (both heads import it), new tests (`InformixProviderTests`, `InformixDialectTests`, registration and icon tests, profile round trip).
- Verified: Release build of `Squill.slnx` and `Squill.Windows.slnx` with `-warnaserror` (0 warnings), `dotnet test` 5136 total, 0 failed, `-Engine informix` 108/108 on Podman, `eng/check-aot-warnings.ps1` for WinUI and Avalonia (4 baseline warnings each, nothing new). The published WinUI exe (win-x64 Native AOT, `clidriver/` beside it) was driven with UI Automation against `informix-developer-database` (15.0.1, DRDA): the Informix tile and the connection editor, open from the explorer (database `sysmaster`, 152 tables listed), `select first 5 tabname from systables` filled the grid (5 rows, 59 ms), a six-fold cross join of `systables` was stopped with the Stop button (status "Annule", 5 s) and the next query ran on the same connection, a mistyped statement gave `[42000] ... A syntax error has occurred. (-201)`; no crash, no `crash.log`. Not driven: the editor's Test button (it stayed disabled after a first test with an empty host while the fields were set through UI Automation), grid editing, plans, the terminal, manual transactions, the Avalonia exe against a server (publish check only).
- Deviations: (1) `clidriver` is copied whole from the package, not only the files `REDIST.txt` names: that list omits `db2clio.dll`, `icc64` and the MSVC runtimes, yet the driver cannot connect without them. (2) Velopack `vpk pack` has no EULA screen outside `--msi`, so the IBM licence text ships as files in `clidriver/license/` (the owner's "licence text in the installer" is met by the files only; a real screen is [P21-110](P21-110-installer-ibm-eula-screen.md)). (3) Savepoints on Informix are untested (the transaction texts exist, no container test runs them). (4) The Avalonia head on Linux and macOS fails with `DllNotFoundException` when an Informix profile is opened, because `clidriver` is Windows-only here ([P21-111](P21-111-informix-clidriver-linux-macos.md)).
- Root cause of the WinUI crash seen while driving the app (`XamlParseException ... Geometry` on the first tab selection after connecting): IBM's `db2cli64.dll` calls the C runtime `setlocale(LC_ALL, "")` inside `SQLDriverConnectW`, which switches the process to the user locale (`French_France.1252` here); WinUI's native `TabView` then formats the tab-flare geometry with decimal commas and fails to parse it. Fix: `OdbcApi.DriverConnect` restores the "C" locale in a `finally` right after the call (`OdbcNative.RestoreCLocale`, `setlocale` from `ucrtbase.dll`). Measured: connect is the only driver call that changes the locale (a probe checked it after open, execute, fetch, dispose of the reader, a cancelled query, a SQL error, begin and rollback, and disconnect: always "C"), so no other call is wrapped. The restore is process-global and not synchronised; it runs on the calling thread right after the call, and "C" is the default locale of a .NET and WinUI process, so another thread can only see the user locale for the few milliseconds of the connect. Regression test `Open_LeavesTheProcessCRuntimeLocaleUntouched` (Docker-gated): it failed without the restore (`French_France.1252`) and passes with it.
- Found while driving, not fixed here (P21-12 area): the editor underlines `FIRST` in `select first 5 ...` as an error and calls the statement read-only (`EditableSourceAnalyzer` answers "not a single statement"; that reason text is an English literal, not a `UiText` key); the query itself runs. P21-12 now has the tokenizer part done (`InformixTokenizer` exists) and keeps the splitter, `FIRST/SKIP`, quoting, catalogue and grammar.
- Not built here and not carded: designer DDL, grid editing, execution plans, the transactions screen, the `dbaccess` terminal, schema comparison (disabled for Informix, `compare.disabled.informix`).
