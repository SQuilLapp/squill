# ADR 0015 - The remaining mockups: home, schema compare, sessions, privileges and import

- Status: accepted
- Date: 2026-11-18

## Context
[ADR 0006](0006-visual-conformance.md) and `docs/design/visual-spec.md` section 10 listed five mockup screens the app did not have, because no feature stood behind them: M01 home, M09 schema compare, M10 import wizard, M11 roles and privileges, M12 sessions and locks. Phase 18 ([task index](../tasks/README.md), cards P18-01 to P18-32, shared decisions in [mockups-recipe.md](../tasks/phase-18/mockups-recipe.md)) builds the features and both heads (WinUI and Avalonia). It also closes the verification left open by phase 17 (P18-28), puts the WinUI explorer rows and environment pills on the mockup numbers (P18-29) and moves the query parameters prompt into a panel under the editor (P18-30).

## Decision

### Scope (recipe 1)
- **Home (M01)**: up to 6 recent connections (`Ctrl+1` ... `Ctrl+6`), new connection, the quick actions that already have a command, a tip card, a search box that opens the palette, and the setting "show the home page at startup". It is the content of the main area while no tab is open and no connection is active; opening a tab or connecting hides it.
- **Schema compare (M09)**: structure only, between two connected databases **of the same dialect** (`SchemaComparer` in `Squill.Core/Schema/Compare`). Column order is never reported, there is no rename detection, and function bodies are compared as text. The migration script (`MigrationScriptBuilder` in `Squill.Sql/Migration`) is editable and can be copied, saved, opened in the editor or executed on the target.
- **Sessions and locks (M12)**: PostgreSQL, MySQL and SQL Server through the optional `IActivityReader`. Tiles, filter tabs, table, details, who blocks whom (`BlockingChains`), cancel query and end session. SQLite and Oracle disable the command with a reason.
- **Privileges (M11)**: **PostgreSQL only**, through the optional `ISecurityReader`: roles, the matrix of tables and views, the generated `GRANT` / `REVOKE` script (`PostgresPrivilegeScript`) and `Appliquer`.
- **Import (M10)**: **CSV into an existing table**, five steps (source, target, mapping, options, run), with a preview, conversions (`ValueConverter`), an error policy, a rejected rows file and progress.

### Rules shared by the five features
- **Entry points**: one workspace command, one palette action, one item of the `Base de données` menu and one explorer context item each, disabled with a reason when the connection is not connected or the engine is unsupported.
- **Engine access** stays behind `Squill.Data` contracts found through the connection (`connection as IActivityReader`, `null` when unsupported). Heads and Presentation never see a driver package. Every engine query of these features is read-only.
- **Every write goes through the statement runner**, `SessionTransaction` and the phase 15 production confirmation ([ADR 0012](0012-transactions-and-production-confirmation.md)): the migration script, `GRANT` / `REVOKE`, the import and the kill never run around them. Inside a manual transaction the import uses savepoints.
- **Monitoring polls only while its tab is active**; a poll never starts while the previous one runs, and a stale result never replaces a newer one. The UI thread never waits for a database.
- **WinUI `ItemsSource` rule** (the P17-19 crash): under Native AOT an `ItemsSource` never gets a managed list of objects. Lists are built in code, from plain strings or WinRT collections, and wide tables are drawn on a `Grid` or a canvas. The rule applies to every list of phase 18.
- **Texts** go through `UiText` (fr and en, parity tests), issues are message codes, colors come only from tokens.

## Consequences
- The two heads share every rule through the view-models (`Home`, `Compare`, `Activity`, `Security`, `Import` in `Squill.Presentation`); a head only draws them.
- A feature that an engine does not support is visible and disabled with a reason, never hidden and never half working.
- The MySQL reader gives each table the database name as schema, so comparing two MySQL databases lists every common table as added and removed. PostgreSQL and SQL Server pass. Proposed card `P18-33` (see Known limits).

## Known limits
- No screen was compared with its PNG, in either head or theme, by an agent session: the checklist of the owner is [docs/verification/new-screens.md](../verification/new-screens.md).
- MySQL compare of two databases is wrong until `P18-33` (treat the schema equal to the snapshot's default schema as the same on both sides, and qualify names with the target's schema in the script).
- Avalonia: `StatusBar.RefreshEnvironment` is sometimes called from a non-UI thread and fails `LayoutOverlapTests` about one run in two (proposed card `P18-34`).
- The query parameters panel does not draw the type dropdown of the mockup, because values are still bound as strings.

## Not built
- Home: projects, importing other tools' connections, the ephemeral DuckDB console, `.duckdb` / `.parquet`, driver count.
- Compare: the data tab, cross-engine compare, "ignore whitespace and column order", rename detection.
- Sessions: Oracle; the transactions per second tile when the engine counter is not cheap.
- Privileges: the general, memberships and default privileges tabs, duplicate and delete role, effect on members, `ALTER DEFAULT PRIVILEGES`, MySQL, SQL Server, SQLite and Oracle.
- Import: database to database transfer, JSON / Parquet / Excel sources, value mapping, conflict handling, new table creation, pause.
