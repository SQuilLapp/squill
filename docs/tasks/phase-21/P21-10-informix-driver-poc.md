# P21-10 - Informix: driver proof of concept under Native AOT (decision card)

Status: todo
Depends on: none

## Goal
The owner explicitly wants Informix as a future database. Before any provider code, find out **which driver can run under Native AOT** (or through a bridge process), measure it, and write the verdict. Same approach as the Oracle proof of concept ([docs/poc/oracle-bridge.md](../../poc/oracle-bridge.md)).

Exists today: nothing for Informix (no dialect, no provider, no container).

## What is known before the card (desk research 2026-11-27, to be verified by the card)
| Option | What it is | AOT risk | Notes |
|---|---|---|---|
| A. `Net.IBM.Data.Db2` (+ `-lnx`, `-osx` packages), `IBM.Data.Db2` namespace | IBM's current .NET provider for Db2 **and** Informix; IBM says the old Informix provider `IBM.Data.Informix` is deprecated in its favour. Latest listing seen: 10.0.0.400 (.NET 8+), 8.0.x line for .NET 8 | **High, unknown**: wraps the native `clidriver` (CLI/ODBC C library shipped in the package, about 100+ MB); no trim/AOT annotations known; may use reflection and `Marshal` patterns the AOT analyzers flag | Talks **DRDA**: the Informix server needs a DRDA listener (`drsoctcp` / `drsocssl` entry in `sqlhosts`, often port 9089) - to confirm. Licence: Db2 Connect licence needed for Db2 z/OS and IBM i; for Informix and Db2 LUW servers it is believed not to be needed - to confirm. Separate packages per OS; macOS arm64 availability to check |
| B. `IBM.Data.Informix` (Informix Client SDK .NET provider) | Legacy provider, .NET Framework era | **No go** expected (deprecated, not .NET Core) | Record only |
| C. `System.Data.Odbc` + IBM Informix ODBC driver (Client SDK) or the `clidriver` ODBC entry points | Microsoft's ODBC ADO.NET provider over the OS driver manager (`odbc32.dll`, unixODBC) | **Medium**: P/Invoke based; trim/AOT status of `System.Data.Odbc` to measure | The user installs the IBM ODBC driver; a DSN-less connection string; works for any ODBC engine (see P21-15) |
| D. Own minimal ODBC P/Invoke layer (`LibraryImport`, source generated) | SQuilL calls `SQLDriverConnect`, `SQLExecDirect`, `SQLFetch` itself | **Low** (fully AOT-safe by construction) | More code (types, Unicode, long data), but also opens generic ODBC (P21-15) |
| E. Bridge process (Oracle pattern) hosting option A in a JIT console | Out-of-process, JSON lines | **None** for the app (the bridge is JIT) | Needs the .NET runtime installed; reuses `BridgeClient` design |

## Design decisions (final)
- Container: IBM's Informix Developer Database image (`icr.io/informix/informix-developer-database`, formerly `ibmcom/informix-developer-database`; licence accepted through `LICENSE=accept`), new `-Engine informix` in `eng/run-docker-tests.ps1`, not in `all`. Enable the DRDA listener in the container `sqlhosts` for option A.
- Probe order: A in process (`tools/Squill.AotProbe.Informix`: publish win-x64 AOT, count IL2xxx/IL3xxx, start, connect, `SELECT FIRST 5 tabname FROM systables`, read `DECIMAL`, `DATETIME YEAR TO FRACTION`, `LVARCHAR`, `BYTE`); then C; D and E are assessed on paper with an effort estimate, not built.
- Report `docs/poc/informix-driver.md` with the measured table above (each "to confirm" replaced by a measurement or `not verified`), the package size added to the installer, the licence statement quoted from IBM's package page, Linux/macOS availability, and a verdict: `go in process (A or C)`, `go through the bridge (E)`, or `go with own ODBC layer (D)`.
- If A or C needs `NoWarn` to publish: that is a `no go` for in-process (project rule), the verdict falls to E or D.

## Out of scope
- Provider code (P21-11), SQL dialect (P21-12).

## Tasks
- [ ] Container and `-Engine informix`, DRDA listener.
- [ ] Probe A, then C; quote the publish warnings and the query output.
- [ ] Write the report and propose the rewrite of P21-11 if the verdict differs from its default.

## Acceptance criteria
- Report committed with measured results; probe published and run (or the reason it could not); nothing added to `src/`.

## Risks
- The `clidriver` native folder may need `IBM_DB_HOME` / `PATH` setup at runtime: record exactly what the app would have to ship.
- The developer image is large and may need `--privileged`/shared memory settings under Podman: record the command that worked.

## Open points (defaults)
- Owner: is a .NET runtime requirement (bridge, option E) acceptable for Informix like Oracle? Default yes.
- Owner: which Informix version matters (default 14.10, the developer image).

## Suggested commit message
Measure the Informix driver options under Native AOT and record the verdict
