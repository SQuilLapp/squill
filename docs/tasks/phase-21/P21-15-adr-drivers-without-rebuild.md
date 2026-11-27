# P21-15 - ADR: adding drivers without rebuilding, under Native AOT (feasibility)

Status: done
Depends on: P21-10

## Goal
DBeaver lets users add a JDBC driver at runtime. Decide whether SQuilL can offer anything similar. **Native AOT forbids loading .NET assemblies at runtime** (no `Assembly.Load`, no reflection-based provider factories), so a DBeaver-style "drop a DLL in a folder" is **not possible in process**. This card writes the ADR with the options that remain; no product code.

Exists today: providers are compiled in and registered in each head's composition root (`ProviderRegistry`); the Oracle bridge proves the out-of-process route.

## Design decisions (final)
- ADR `docs/adr/0018-drivers-without-rebuild.md` comparing:
  1. **Generic ODBC provider** (in process, AOT-safe through `LibraryImport` or `System.Data.Odbc` if P21-10 measured it clean): the user installs any ODBC driver from the vendor; SQuilL lists the DSNs / drivers from the OS driver manager. Gives "any engine" with a generic SQL dialect, no schema reading beyond `SQLTables` / `SQLColumns` / `SQLPrimaryKeys` / `SQLForeignKeys`.
  2. **Generic bridge** (out of process): the Oracle bridge generalised to load any ADO.NET provider assembly by name in a JIT process (`DbProviderFactories` there is allowed). Needs the .NET runtime; the user points at a NuGet package or a folder.
  3. **Native plugins** (C ABI libraries loaded with `NativeLibrary.Load`): possible under AOT but a heavy contract for driver authors; rejected.
  4. **Rebuild-only** (status quo).
- Recommendation in the ADR, with the cost of each (cards), and the security note: loading third-party code runs it with the user's rights; a confirmation and a source list are needed.

## Out of scope
- Implementation (cards proposed by the ADR, added to this index only when the owner picks one).

## Tasks
- [x] Write the ADR; link it from `docs/architecture.md` risks table (new row R10).

## Acceptance criteria
- ADR committed; the "not possible in process under Native AOT" statement is explicit.

## Risks
- None (document only).

## Open points (defaults)
- Owner: which option, if any (default recommendation: generic ODBC first, since it also serves Informix option D).

## Suggested commit message
Add ADR 0018 on adding database drivers without rebuilding under Native AOT

## Done notes
ADR: [0018](../../adr/0018-drivers-without-rebuild.md); risk R10 added to `docs/architecture.md`. Recommendation: generic ODBC first (driver manager and driver-by-path modes), no generic bridge now, native plugins rejected, rebuild-only for official engines. Checked against the code: `DatabaseDialect` is a closed enum and `ProviderRegistry` is filled at start (a new engine needs a rebuild); registration is in `AppComposition` and the heads, not only in each head; the ODBC layer is `internal` to `Squill.Data.Informix` (extraction needed). No new measurement: the ADR relies on the P21-10 measurements and labels the Linux/macOS driver manager, other vendors' drivers by path, a generic bridge and native plugins as not measured. Proposed cards P21-113 to P21-116 are sketched in the ADR only, not in the index (the owner picks).
