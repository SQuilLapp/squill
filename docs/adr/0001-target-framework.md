# ADR 0001 - .NET target

- Status: proposed
- Date: 2026-10-29 (revised)

## Context
The brief targets .NET 11, installed as **11.0.100-rc.3** (not yet GA). The project owner decided to target .NET 11 RC3. Microsoft.Data.Sqlite 11.0.0-rc.3 exists; Npgsql, MySqlConnector and SqlClient have no dedicated net11 build (they target net8/net10 and remain usable).

## Decision
Target **`net11.0`** via `Directory.Build.props`, SDK pinned by `global.json` (rc.3, `allowPrerelease`). Fallback: `net10.0` (SDK 10.0.301 is installed), a one-property change.

## Consequences
- RC risk: changes are possible until GA; re-check on every RC (especially AOT and Windows App SDK, which must support net11).
- Packages without a net11 target are consumed through their lower TFM; compatibility warnings are handled case by case in the drivers' AOT report.
- WinUI 3: `net11.0-windows10.0.19041.0`, to be proven (risk R2).

## Update 2026-11-12 - GA
.NET 11 is released. The SDK is now pinned to **11.0.100** (`allowPrerelease` false) and Microsoft.Data.Sqlite to **11.0.0**. The target framework is unchanged (`net11.0`). The text above records the RC3 decision and is kept as history; the RC risk and the `net10.0` fallback no longer apply.
