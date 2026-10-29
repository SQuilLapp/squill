# Native AOT driver report

Risk R3: every ADO.NET driver must work under Native AOT. Each driver card adds its row; the probe lives in `tools/Squill.AotProbe.<Driver>`.

| Driver | Package version | AOT publish | Runtime check | Verdict | Caveats |
|---|---|---|---|---|---|
| SQLite (`Microsoft.Data.Sqlite`) | 11.0.0-rc.3.26522.148 | `dotnet publish -c Release -r win-x64`: succeeded, 0 IL2xxx/IL3xxx warnings | `Squill.AotProbe.Sqlite.exe` printed `OK 2`, exit 0 | compatible | The native `e_sqlite3.dll` (SQLitePCLRaw bundle) is copied next to the exe and must ship with it. |
| PostgreSQL (`Npgsql`) | 10.0.3 | `dotnet publish -c Release -r win-x64`: succeeded, 0 IL2xxx/IL3xxx warnings | not run (no Docker or PostgreSQL server available) | compatible (publish only) | Verdict rests on publish warnings alone; run `Squill.AotProbe.Postgres.exe "<connection string>"` against a server to confirm `OK`. Uses `NpgsqlSlimDataSourceBuilder`. |
| MySQL/MariaDB (`MySqlConnector`) | 2.6.2 | `dotnet publish -c Release -r win-x64`: succeeded, 0 IL2xxx/IL3xxx warnings | not run (no Docker or MySQL server available) | compatible (publish only) | Verdict rests on publish warnings alone; run `Squill.AotProbe.MySql.exe "<connection string>"` against a server to confirm `OK`. |

## SQLite probe output

```
OK 2
```
