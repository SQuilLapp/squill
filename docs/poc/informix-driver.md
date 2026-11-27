# Informix driver options under Native AOT

Card [P21-10](../tasks/phase-21/P21-10-informix-driver-poc.md), measured on 2026-11-27 on Windows 11 x64, .NET SDK 11.0.100, Podman 5.6.2 (WSL machine). Probes (throw-away, in no solution, not part of `Squill.slnx`): `tools/Squill.AotProbe.Informix` (option A), `tools/Squill.AotProbe.InformixOdbc` (options C and D). Every "measured" below was run; everything else says `not verified`.

## Server used

`icr.io/informix/informix-developer-database:latest` (1.54 GB, Informix 15.0.1.0.3 Developer Edition, built 2026-03-12). It starts without `--privileged` or shared-memory options on Podman/WSL and was ready in about 30 s.

```
podman run -d --name squill-informix -e LICENSE=accept -p 127.0.0.1:9088:9088 -p 127.0.0.1:9089:9089 icr.io/informix/informix-developer-database:latest
```

- The **DRDA listener is already on**: `/opt/ibm/config/sqlhosts` has `informix onsoctcp ... 9088` and `informix_dr drsoctcp ... 9089`, `onconfig` has `DBSERVERALIASES informix_dr`. Nothing to enable; publish port 9089.
- Default login `informix` / `in4mix` (accepted by DRDA, a wrong password gives `SQL30082N ... reason "24"`). The image has no sample database; the probe database was created with `dbaccess` inside the container (`INFORMIXDIR=/opt/ibm/informix/v15.0.1.0.3`, `INFORMIXSERVER=informix`, `CLIENT_LOCALE=en_US.8859-1`) as `create database probe with log` plus a table `t(id serial, d decimal(12,3), ts datetime year to fraction(5), lv lvarchar(500), b byte)`.
- Through DRDA the server reports its version as `12.10.0000`, not 15.0: do not use `ServerVersion` for feature checks.

## Verdict table

| Option | Connect | Query, types, schema | Native AOT publish | Published exe runs | Verdict |
|---|---|---|---|---|---|
| A. `Net.IBM.Data.Db2` 10.0.0.400 in process | yes (JIT) | yes (JIT) | succeeds with warnings IL2104, IL3002, IL3053 | **no**: `Open()` throws `NotSupportedException: CodeBase is not supported on assemblies loaded from a single-file bundle` | **no go in process**; works only under JIT (bridge) |
| B. `IBM.Data.Informix` 3.0.0.2 | not tried | not tried | not tried | not tried | no go (listed on NuGet as a single, .NET Framework era 3.0.0.2; not verified further) |
| C. `System.Data.Odbc` 10.0.0 + IBM ODBC driver | **not verified** (no IBM driver registered; see below) | not verified | succeeds, 1 warning (IL2104) | yes against a control driver (Microsoft Access text driver: connect, `select`, column types, rows) | AOT-capable provider, but unverified with IBM; needs a registry change to register the IBM driver |
| D. own P/Invoke layer, `LibraryImport` straight into `db2cli64.dll` | yes | yes (text reads) | succeeds, 0 warnings from the probe's own code | yes | **go** (recommended) |
| E. bridge process hosting A | yes (A under JIT) | yes | n/a (the bridge is JIT) | n/a | **go, fallback**; needs the .NET runtime on the machine |

## A. `Net.IBM.Data.Db2` in process

Package 10.0.0.400 (`net10.0` assembly, usable from net11.0), 46.1 MB nupkg; `requireLicenseAcceptance=true`. On build the package copies a `clidriver` folder (125 MB on disk, 124.8 MB of files) and a `TestConn.exe` next to the output.

Measured under JIT (`dotnet Squill.AotProbe.Informix.dll "Server=localhost:9089;Database=probe;UID=informix;PWD=..."`):

```
server 12.10.0000
> SELECT FIRST 5 tabname FROM systables
  col tabname VARCHAR System.String
  systables | syscolumns | sysindices | systabauth | syscolauth
> SELECT id, d, ts, lv, b FROM t
  col id INTEGER System.Int32
  col d DECIMAL System.Decimal
  col ts DATETIME YEAR TO FRACTION(5) System.DateTime
  col lv LVARCHAR System.String
  col b BLOB System.Byte[]
  1 | 12345,678 (Decimal) | 2026-11-27T13:45:12.3456700 (DateTime) | hello informix | NULL
  2 | 1,500 (Decimal) | NULL | NULL | 010203 (Byte[])     (byte[] 1,2,3 inserted through a ? parameter)
OK
```

So the driver reads `DECIMAL` (exact), `DATETIME YEAR TO FRACTION(5)` (to 100 ns, as `DateTime`), `LVARCHAR`, `BYTE` (reported as `BLOB`, `byte[]`) and NULLs. Quirk: `DB2DataReader.GetValue(i)` cannot be called twice on the same column of a row (`There is no more data to return`); read each column once.

Native AOT (`dotnet publish -c Release -r win-x64`, probe with `TreatWarningsAsErrors=false` so it can finish):

```
IBM.Data.Db2.dll : warning IL2104: Assembly 'IBM.Data.Db2' produced trim warnings.
ILC : warning IL3002: IBM.Data.Db2.UnsafeNativeMethods.DB2Interop.GetCliDriverPath(String): Using member 'System.Reflection.Assembly.CodeBase.get' which has 'RequiresAssemblyFilesAttribute' can break functionality when embedded in a single-file app.
IBM.Data.Db2.dll : warning IL3053: Assembly 'IBM.Data.Db2' produced AOT analysis warnings.
```

Published folder 164 MB (exe 8.2 MB, `clidriver` 126 MB, pdb 30 MB). Running it:

1. Without `clidriver\bin` on `PATH`: `new DB2Connection(...)` throws `DllNotFoundException: Unable to load DLL 'db2clio.dll'`. The native folder is therefore **not** found from the app directory under AOT.
2. With `PATH=<publish>\clidriver\bin`: the constructor passes, then `Open()` throws

```
System.NotSupportedException: CodeBase is not supported on assemblies loaded from a single-file bundle.
   at IBM.Data.Db2.DB2ConnPool.Open(DB2Connection, String&, DB2ConnPool.DB2ConnSettings&, Object&)
   at IBM.Data.Db2.DB2Connection.Open()
```

This is exactly the IL3002 warning (`GetCliDriverPath` uses `Assembly.CodeBase`); there is no setting of the package that avoids it that was found (`not verified`: whether an environment variable skips the call; the package is closed source and was not decompiled). Without fixing the package this cannot work in a Native AOT exe, and the warnings cannot be silenced (project rule: no `NoWarn`). **No go in process.**

Linux and macOS: `Net.IBM.Data.Db2-lnx` and `Net.IBM.Data.Db2-osx` exist on NuGet with the same versions up to 10.0.0.400 (listed, not downloaded; macOS arm64 content `not verified`).

Licence (from `Lic_en.txt` of the package, paraphrased): "IBM Data Server Provider for .NET 10.0" falls under the IBM International Program License Agreement; the listed redistributables (the `clidriver` files) may be redistributed in object-code form only inside the licensee's own value-added application, only to support it, with copyright notices kept and an end-user licence at least as protective as IBM's; provided "as is". The text does not mention Informix or Db2 Connect at all: whether a Db2 Connect entitlement is needed for Db2 z/OS or IBM i is **not verified** (Informix and Db2 LUW servers worked here with no licence key).

## B. `IBM.Data.Informix`

Only version 3.0.0.2 is on NuGet (`ibm.data.informix`), a .NET Framework era provider; IBM points to `Net.IBM.Data.Db2` instead. Not tried: no go, record only.

## C. `System.Data.Odbc` + IBM ODBC driver

`System.Data.Odbc` 10.0.0 publishes Native AOT with one warning (`IL2104: Assembly 'System.Data.Odbc' produced trim warnings`) and works in the published exe: with the Microsoft Access text driver already on this machine (`Driver={Microsoft Access Text Driver (*.txt, *.csv)};Dbq=...`) it connected, reported `server 01.00.0000 driver ACEODBC.DLL`, the columns (`a INTEGER System.Int32`, `b CHAR System.String`) and the rows. Exe 3.5 MB, publish folder 18 MB (13.2 MB of it the pdb).

Against Informix: the machine has no IBM driver registered (`Get-OdbcDriver` lists Microsoft drivers only) and the DSN-less string failed with `IM002 Data source name not found and no driver specified`. The `clidriver` of the package can be registered with `db2cli install -setup`, which writes the Windows registry (an ODBC driver entry): **not done** without the owner's consent, so the IBM connection through the driver manager is `not verified`. The cost for users would be that registration step (or installing IBM's ODBC driver / Client SDK).

## D. Own ODBC layer (`LibraryImport`)

`tools/Squill.AotProbe.InformixOdbc` `raw` mode: about 60 lines of source-generated P/Invoke (`SQLAllocHandle`, `SQLSetEnvAttr`, `SQLDriverConnectW`, `SQLExecDirectW`, `SQLNumResultCols`, `SQLFetch`, `SQLGetData` as `SQL_C_WCHAR`, `SQLGetDiagRecW`, `SQLFreeHandle`) straight into `db2cli64.dll` of the IBM package, no driver manager, no registry. Published Native AOT with `PublishAot` and the analyzers on: no warning from this code (the single IL2104 of the probe belongs to the unused `System.Data.Odbc` reference of the same probe). Run on the published exe with `PATH=<clidriver>\bin`:

```
> SELECT FIRST 3 tabname, 'x' FROM systables
  systables | x / syscolumns | x / sysindices | x
> SELECT id, d, ts, lv FROM t
  1 | 12345,678 | 2026-11-27 13:45:12.345670 | hello informix
  2 | 1,500 | NULL | NULL
OK
```

A wrong password gives the diagnostic record `08001 [IBM][CLI Driver] SQL30082N Security processing failed with reason "24"`, so errors are readable. Notes from the probe: `SQLLEN` is 8 bytes on win64 but the driver wrote 4 (initialise it to 0 or compare as `int`); the whole probe reads every value as text, so binary (`BYTE`) and large objects, `SQLGetData` loops for values longer than the buffer, `SQLCancel`, transactions (`SQLSetConnectAttr`, `SQLEndTran`) and parameters (`SQLBindParameter`) are **not verified**, only estimated. The DLL was found only through `PATH`; a shipped layer would use `NativeLibrary.SetDllImportResolver` (AOT safe) with an explicit path to the bundled `clidriver\bin`.

Effort estimate (paper): handles, diagnostics, connection string, statements and parameters, Unicode, long data, type mapping from `SQLDescribeCol`/`SQLColAttribute`, cancel, transactions, Linux/macOS library names: about 1,200 to 1,800 lines plus tests, in 4 to 6 cards of the size of the Oracle ones. It is also the base of a generic ODBC provider ([P21-15](../tasks/phase-21/P21-15-adr-drivers-without-rebuild.md)) and covers Db2 LUW through the same `clidriver` ([P21-13](../tasks/phase-21/P21-13-other-engines-feasibility.md)).

## E. Bridge process (Oracle pattern)

Option A works under JIT end to end (above), so a `Squill.InformixBridge` console hosting `Net.IBM.Data.Db2` with the `BridgeClient` of [the Oracle bridge](oracle-bridge.md) is certain to be feasible. Cost: the .NET runtime must be installed (same acceptance as Oracle, default yes), a bridge folder and the 126 MB `clidriver` copied beside it, the quirks of A (PATH, one read per column), and one extra process per connection. Effort: the Oracle bridge took one provider card plus follow-ups; the Informix one is smaller (no `DBMS_OUTPUT`, no implicit results), about the same size as P21-11 as written today.

## Verdict

**go with an own ODBC layer (D)**, bridge (E) as the fallback if D hits a driver behaviour that is too costly to reproduce (large objects, `DATETIME` qualifiers). Reasons: A cannot run under Native AOT and cannot be fixed from SQuilL; D is the only option measured end to end under AOT against Informix with zero warnings, needs no .NET runtime, and is reusable for generic ODBC and Db2. Whatever the choice, the app must ship (or locate) the IBM `clidriver` (about 125 MB on disk, 46 MB as a download) under IBM's redistribution terms above.

Open for the owner: accept shipping `clidriver` (size, IBM licence text in the installer), and whether a registry-based `db2cli install -setup` is acceptable (not needed for D).

## What the app would have to ship or set up

- The `clidriver` folder (`bin`, `bnd`, `cfg`, `conv`, `license`, `msg`, `properties`), with its `license` files kept.
- Windows: `db2cli64.dll` plus its siblings in `clidriver\bin` resolved by explicit path; no registry, no `IBM_DB_HOME` was needed for D. Linux and macOS: the `-lnx` / `-osx` packages, `not verified`.
- The Informix server needs a DRDA listener (`drsoctcp` entry, port 9089 in the developer image). A server without one (only `onsoctcp`, port 9088, the classic SQLI protocol) cannot be reached by any option here: documentation for users.

## Cards to write next

- Rewrite [P21-11](../tasks/phase-21/P21-11-informix-provider.md) for D (done in the same commit): ODBC layer first, then provider and schema reading.
- [P21-15](../tasks/phase-21/P21-15-adr-drivers-without-rebuild.md) and [P21-13](../tasks/phase-21/P21-13-other-engines-feasibility.md) should take this report as input: the ODBC layer is the generic driver path, and `clidriver` covers Db2 LUW.

## Cleanup

The container `squill-informix` was removed after the run. The two probe projects are not in any solution and are `TreatWarningsAsErrors=false` on purpose (third-party warnings counted above); they use `ManagePackageVersionsCentrally=false` so that `Directory.Packages.props` does not carry IBM or ODBC package versions.
