# ADR 0018 - Adding database drivers without rebuilding, under Native AOT

- Status: proposed (the owner picks; no implementation card is in the index yet)
- Date: 2026-11-27
- Card: [P21-15](../tasks/phase-21/P21-15-adr-drivers-without-rebuild.md)

## Context

DBeaver lets a user add a JDBC driver at runtime. SQuilL is Native AOT: there is no JIT, so **a .NET assembly cannot be loaded at runtime in process** (`Assembly.Load`, `Assembly.LoadFrom`, `DbProviderFactories` and any reflection-based provider factory are unavailable or not trim-safe). "Drop a DLL in a folder" for a .NET driver is therefore **not possible in process**, and no choice in this ADR changes that. What remains is to reach a database through something that is not managed code loaded into the AOT process.

What exists today (checked in the code on 2026-11-27):

- `DatabaseDialect` is a closed enum (`PostgreSql`, `MySql`, `SqlServer`, `Sqlite`, `Oracle`, `Informix`); `ProviderRegistry` (`src/Squill.Data`) maps a dialect to one `IDatabaseProvider`, filled when the app starts (`AppComposition` and the two heads). A new engine is a rebuild by construction.
- `src/Squill.Data.Informix` is an own ODBC layer: `OdbcNative` (source-generated `LibraryImport` into IBM's `db2cli64.dll`, resolved with `NativeLibrary.SetDllImportResolver` to the bundled `clidriver` folder, no driver manager, no registry), `OdbcConnection`, `OdbcReader`, `OdbcValue`, `OdbcParameter`, `CancelGate`, `OdbcException`. The project is about 1,600 lines, of which roughly 900 are engine-neutral; they are `internal` to that project today.
- `src/Squill.Data.Oracle` + `src/Squill.OracleBridge`: a JIT console process hosting ODP.NET, one process per connection, a JSON-line protocol over stdin/stdout ([oracle-bridge.md](../poc/oracle-bridge.md)). The bridge is the only project exempt from AOT.

## Evidence (what the repository measured, and what it did not)

Measured on Windows 11 x64, .NET SDK 11.0.100 ([informix-driver.md](../poc/informix-driver.md), [other-engines.md](../poc/other-engines.md), [nosql.md](../poc/nosql.md), [oracle-bridge.md](../poc/oracle-bridge.md)):

| Fact | Source |
|---|---|
| `System.Data.Odbc` 10.0.0 publishes under Native AOT with one warning (IL2104, third-party assembly) and, in the published exe, connects through the Windows driver manager to a registered driver (Microsoft Access text driver), reads column types and rows. | informix-driver.md, option C |
| Own `LibraryImport` straight into a vendor ODBC driver DLL (`db2cli64.dll`), library path chosen at run time by a resolver: 0 warnings from our code, published exe connects, runs queries, reads typed values, reports errors, cancels, runs transactions. No driver manager, no registry. | informix-driver.md option D; P21-103 to P21-106 |
| A closed-source .NET driver (`Net.IBM.Data.Db2`) publishes under AOT but `Open()` throws at run time (`Assembly.CodeBase` in a single-file bundle). The MongoDB driver publishes and fails at the first handshake (reflection in the BSON registry). Snowflake's driver is no go in process. Publish warnings do not predict this: only running the published exe does. | informix-driver.md A; nosql.md; other-engines.md |
| A JIT bridge hosting the same managed drivers works (Oracle end to end against a container; the Informix driver under JIT). Cost: the .NET runtime must be installed, one process per connection, a protocol to maintain. | oracle-bridge.md; informix-driver.md E |
| Registering an ODBC driver with the Windows driver manager writes the registry. **Not approved by the owner** (decision of 2026-11-27: no registry writes, no global installs, no reliance on PATH). A vendor installer that the user runs by hand does this outside SQuilL. | informix-driver.md C; owner decision |

**Not measured** (desk research; the first card that depends on it must prove it): the driver-manager path on Linux (unixODBC) and macOS; loading a third-party ODBC driver by file path other than IBM's (the exported `SQL*` entry points are the ODBC standard, but some drivers lean on the driver manager for pooling, `SQLGetFunctions` shims or ODBC 2 to 3 mapping); a generic bridge using `DbProviderFactories` or `Assembly.LoadFrom` under JIT (the Oracle bridge references its driver at build time and was never generalised); a native plugin with a C ABI (nothing was built).

## Options

### 1. Generic ODBC provider (in process, AOT-safe)

Promote the engine-neutral part of the Informix layer into a shared project (working name `Squill.Data.Odbc`) and add a provider for a new generic dialect (`DatabaseDialect.Odbc`, shown as "Other (ODBC)"). A connection can be made two ways:

- **a. Through the OS driver manager**: connection string or DSN; SQuilL lists the drivers and DSNs the driver manager reports (`SQLDrivers`, `SQLDataSources`). `LibraryImport` into `odbc32.dll` (Windows) or `libodbc` (unixODBC, not measured). The driver is installed by its vendor installer, never by SQuilL.
- **b. By driver file path**: the user points at a driver library and SQuilL binds the entry points to it exactly as it does for `db2cli64.dll`. Needs no registry and no driver manager. Measured for one driver only.

Capabilities: connect, run SQL, typed reads, parameters, cancel, transactions. Schema reading is limited to the ODBC catalogue functions (`SQLTables`, `SQLColumns`, `SQLPrimaryKeys`, `SQLForeignKeys`, `SQLStatistics`): no routines, triggers or definitions. The SQL layer uses the generic ANSI dialect (no engine-specific completion, formatting or DDL). It fits every database with an ODBC driver (Db2, Teradata, SAP HANA, Firebird, Access, Sybase, many others) and is the base of the Db2 provider ([other-engines.md](../poc/other-engines.md), row 2).

Cost: S + M + M cards (below). Risks: driver quirks (`SQLLEN` width, long data, one `SQLGetData` per column), a weak generic dialect, and every vendor driver is native code running in our process (security note).

### 2. Generic bridge process (out of process, JIT)

Generalise `Squill.OracleBridge` so that it loads any ADO.NET provider by name (`DbProviderFactories` or an explicit assembly path) under JIT, keeping the JSON-line protocol. The user points at a NuGet package or a folder of assemblies.

Pros: reaches managed-only drivers (the ones that fail under AOT: Net.IBM.Data.Db2, MongoDB.Driver, Snowflake) with no rebuild. Cons: the .NET runtime must be installed (accepted for Oracle only, as a default yes); the protocol is a lowest common denominator (typed cells, no provider-specific types); ADO.NET providers differ in parameters, transactions and cancel semantics, so the bridge needs per-provider shims, which brings back the rebuild for anything beyond plain SQL; the user must find, download and trust a package and its dependencies (SQuilL ships no NuGet resolver and must not download by itself); one process per connection. Cost: L + M cards. Not measured.

### 3. Native plugins (C ABI libraries loaded with `NativeLibrary.Load`)

Possible under AOT (the Informix layer already loads a native library by path), but SQuilL would define and version a C ABI for connect, execute, fetch, types, cancel and transactions, and every driver author would write a native shim and ship it for each OS and architecture. ODBC already is such an ABI, standard and with vendor drivers; a custom one adds work for authors and for us without adding reach. **Rejected.**

### 4. Rebuild only (status quo)

Every engine stays a compiled-in provider. Highest quality per engine (schema, dialect, completion, DDL, editing) and no risk of third-party code loaded by a user action. A new engine needs a release. Stays the rule for the engines the product supports officially.

## Decision (recommended; the owner decides)

1. **Option 1 first, with both modes (a and b), behind an explicit "Other (ODBC)" connection kind.** It is the only route measured end to end under Native AOT (driver manager with a control driver, by-path with IBM's driver), needs no .NET runtime, and the work is mostly a refactor of code that exists. It also serves the Db2 provider.
2. **No generic bridge now** (option 2). Revisit only if the owner wants a specific managed-only driver that has no ODBC driver; a bridge dedicated to that engine (the Oracle pattern) costs less and is safer than a bridge that loads anything.
3. **Reject native plugins** (option 3).
4. **Rebuild-only stays for official engines** (option 4). The generic provider is labelled "limited" in the support matrix of [ADR 0008](0008-engine-parity.md) (catalogue-only schema, generic dialect, no engine-specific editing guarantees).

None of the options lets a user add a .NET driver DLL: the statement "not possible in process under Native AOT" stands. ODBC reaches "any SQL database that has an ODBC driver", and only those.

## Security note

A driver, native or managed, runs in the app process (options 1 and 3) or in a child process (option 2) **with the rights of the user**: it can read files, open connections and read the environment, and a driver path can point at any library. Required for any implementation:

- an explicit confirmation naming the file and its full path the first time a driver library is used, and again when its hash changes;
- a visible source list (driver manager entries, or the paths the user added) in Settings; nothing is discovered or loaded in the background;
- no download by SQuilL (recipe 2: nothing leaves the machine without a visible user action); no registry write, no global install, no PATH change (owner decision of 2026-11-27);
- a driver path is never taken from a profile that was imported or synced without the user seeing it, and never resolved from the current directory;
- the profile stores the path only; passwords stay in `ISecretStore`.

## Proposed cards (not in the index; added only when the owner picks one; next free number P21-113)

| Card | Content | Size | Depends on |
|---|---|---|---|
| P21-113 | Extract the engine-neutral ODBC layer of `Squill.Data.Informix` into `Squill.Data.Odbc` (API, connection, reader, values, parameters, cancel); Informix keeps its `db2cli64.dll` resolver; tests move; no behaviour change | M | owner picks option 1 |
| P21-114 | `DatabaseDialect.Odbc`, generic provider in driver-manager mode (list drivers and DSNs, connection string), catalogue schema reader, ANSI dialect, ADR 0008 row "limited"; probe with the Access text driver plus a Docker-gated run against one open-source driver | L | P21-113 |
| P21-115 | Driver-by-path mode with the confirmation and source list of the security note; UI in both heads (WinUI first) | M + M | P21-114 |
| P21-116 | unixODBC and macOS library names and a measured run (the unmeasured part above) | S | P21-114 |
| (only if wanted) | Dedicated bridge for one managed-only engine, per the Oracle pattern | L | the owner names the engine |

## Consequences

- Accepting this ADR changes nothing in the product; the cards above do the work.
- Risk R10 is added to [the architecture risks table](../architecture.md).
- Open for the owner: which option, if any (recommended: option 1); whether by-path mode (b) is wanted besides the driver manager (a); the label "limited" for the generic engine; a managed-only engine that would justify a bridge.
