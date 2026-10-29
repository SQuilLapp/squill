# Native AOT driver report

Risk R3: every ADO.NET driver must work under Native AOT. Each driver card adds its row; the probe lives in `tools/Squill.AotProbe.<Driver>`.

| Driver | Package version | AOT publish | Runtime check | Verdict | Caveats |
|---|---|---|---|---|---|
| SQLite (`Microsoft.Data.Sqlite`) | 11.0.0-rc.3.26522.148 | `dotnet publish -c Release -r win-x64`: succeeded, 0 IL2xxx/IL3xxx warnings | `Squill.AotProbe.Sqlite.exe` printed `OK 2`, exit 0 | compatible | The native `e_sqlite3.dll` (SQLitePCLRaw bundle) is copied next to the exe and must ship with it. |
| PostgreSQL (`Npgsql`) | 10.0.3 | `dotnet publish -c Release -r win-x64`: succeeded, 0 IL2xxx/IL3xxx warnings | not run (no Docker or PostgreSQL server available) | compatible (publish only) | Verdict rests on publish warnings alone; run `Squill.AotProbe.Postgres.exe "<connection string>"` against a server to confirm `OK`. Uses `NpgsqlSlimDataSourceBuilder`. |
| MySQL/MariaDB (`MySqlConnector`) | 2.6.2 | `dotnet publish -c Release -r win-x64`: succeeded, 0 IL2xxx/IL3xxx warnings | not run (no Docker or MySQL server available) | compatible (publish only) | Verdict rests on publish warnings alone; run `Squill.AotProbe.MySql.exe "<connection string>"` against a server to confirm `OK`. |
| SQL Server (`Microsoft.Data.SqlClient`) | 7.1.1 | `dotnet publish -c Release -r win-x64`: succeeded; no IL2xxx/IL3xxx warning from Squill code; the packages raise assembly-level IL2104 (`Microsoft.Data.SqlClient`, `Microsoft.Data.SqlClient.Internal.Logging`, `System.Configuration.ConfigurationManager`) and IL3053 (`Microsoft.Data.SqlClient`) | partial: no Docker or SQL Server available; the AOT exe starts, loads SqlClient and fails at TCP connect with a proper `SqlException` (no `MissingMetadata`/trim crash) | compatible (publish + startup only, unconfirmed against a server) | Not trimmer/AOT-annotated by the package (IL2104/IL3053 are reported, not fixed; probe sets `TreatWarningsAsErrors=false`). Run `Squill.AotProbe.SqlServer.exe "<connection string>"` against a server to confirm `OK`. `System.Security.Cryptography.Pkcs` is pinned to 10.0.14 in `Directory.Packages.props` because the transitive 9.0.18 has advisories (NU1902/NU1903). |
| SSH tunnel (`SSH.NET`) | 2026.0.0 | `dotnet publish -c Release -r win-x64`: succeeded, 0 IL2xxx/IL3xxx warnings | partial: no SSH server available; the AOT exe starts and fails at TCP connect with a proper `SocketException` (no trim crash) | compatible (publish + startup only, unconfirmed against a server) | Run `Squill.AotProbe.SshNet.exe <host> <port> <user> <password> <remoteHost> <remotePort>` against an SSH server to confirm `OK <localPort>`. |


## SQLite probe output

```
OK 2
```
