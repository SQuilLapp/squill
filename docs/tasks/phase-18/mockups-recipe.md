# Phase 18 recipe - the remaining mockups

Shared decisions for every P18 card. A card says "recipe N" for a section here. Mockups: `docs/design/mockups/M01`, `M09`, `M10`, `M11`, `M12` (light and dark); the PNG wins over this file for layout. Audit of the repository at `4c07848`: none of these five screens exists in the code (`docs/design/visual-spec.md` section 10 said so; grep for compare, transfer, privileges, sessions, start page finds nothing).

## 1. Scope (final)

| Mockup | Built in phase 18 | Not built (no feature behind it, never drawn) |
|---|---|---|
| M01 Home | recent connections (up to 6, `Ctrl+1` ... `Ctrl+6`), `Nouvelle connexion`, the quick actions that already have a command, tip card, search box (opens the palette), `Afficher l'accueil au démarrage`, update segment of the status bar (exists) | `PROJETS`, `Importer des connexions`, `Console SQL éphémère` (DuckDB), `.duckdb` / `.parquet`, `N pilotes installés`, tab count and server version of a recent card unless stored |
| M09 Compare | structure compare of two connected databases **of the same dialect**, object list with counts and filter chips, side by side DDL with line marks, migration script (editable), copy / save `.sql` / open in editor / execute on the target | `Données` tab, cross-engine compare, `Ignorer espaces et ordre des colonnes` (column order is never reported, P18-05), rename detection, function bodies diff beyond text equality |
| M12 Sessions | `Sessions et verrous` tab for PostgreSQL, MySQL and SQL Server (SQLite and Oracle: the command is disabled with a reason): tiles, filter tabs, table, session details, who blocks whom, cancel query, end session | `Transactions / s` sparkline on MySQL / SQL Server if the engine counter is not cheap (tile omitted), Oracle |
| M11 Privileges | **PostgreSQL only**: role list (group roles, users), the `Privilèges sur les objets` matrix of tables and views, generated SQL, `Appliquer` | `Général`, `Appartenances`, `Privilèges par défaut` tabs, `Dupliquer`, `Supprimer le rôle`, `Effet sur les membres`, `ALTER DEFAULT PRIVILEGES`, MySQL / SQL Server / SQLite / Oracle |
| M10 Import | `Importer un fichier` wizard, **CSV into an existing table**: 5 steps (Source, Cible, Mappage, Options, Exécution), mapping with preview and conversions, rejected rows file, progress | the `Transfert en cours` card (database to database), JSON / Parquet / Excel sources, value mapping (`Correspondance de valeurs`), `Conflit sur id`, new-table creation, pause |

## 2. Entry points

Each feature gets: one `WorkspaceViewModel` command, one palette action (the registry of P17-11), one item of the `Base de données` menu (`Comparer les schémas...`, `Sessions et verrous...`, `Sécurité et rôles...`, `Importer des données...`), and a context menu item on the connection row (or the table row for the import) of the explorer. Menu and context items are disabled with a tooltip reason when the connection is not connected or the engine is unsupported. M01 replaces the empty main area (recipe 3).

## 3. Home placement

The home page is the content of the main area while **no tab is open and no connection is active** (rail, explorer and properties panel stay hidden as in M01, title bar and status bar stay). Opening any tab or connecting hides it; closing the last tab with no connection brings it back. The setting `Afficher l'accueil au démarrage` only decides whether it shows at launch; with it off the empty area keeps today's look.

## 4. Engine rules

- Heads and Presentation never see a driver package; engine specifics live in `Squill.Data.<Driver>` behind contracts in `Squill.Data` (optional capability interfaces like `IPlanReader`, found through the connection, `null` when unsupported).
- Every engine query for these features is read-only (monitoring, privileges, compare). Writes (migration script, GRANT / REVOKE, import, kill) go through the existing statement runner, `SessionTransaction` and the production confirmation of phase 15, never around them.
- Tests: SQLite always; PostgreSQL, MySQL, SQL Server `SQUILL_DOCKER_TESTS=1` gated with `rtk pwsh eng/run-docker-tests.ps1` (`docs/tasks/phase-11/verification-recipe.md`). Never report a skipped Docker test as verified.

## 5. Technical rules learned in phases 16 and 17

- All text through `UiText` (fr + en, parity tests). Message codes for issues, never message strings.
- WinUI under Native AOT: never give `ItemsSource` a managed list of objects (the P17-19 crash); use plain strings, WinRT collections, or build the elements in code. Lists of rows in a grid-like layout are built with `StackPanel` / `Grid` or drawn on a Win2D / Avalonia canvas.
- Long work runs on a worker with a `CancellationToken`; a stale result never replaces a newer one; the UI thread never waits for a database.
- Heads contain no business logic: they bind view-models and map keys.
- Colors only through tokens; a color seen in a mockup with no token is added to `tokens/squill.tokens.json` by the card that needs it.

## 6. Head card procedure

Follow `visual-spec.md` section 11 (capture light and dark at 1440 x 900 with `tools/visual/`, compare with the PNG, fix differences). A screen that cannot be driven (needs a live server) is listed in the card report as "to check by hand", never claimed as matched.

## 7. Common acceptance

- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings (head cards also `Squill.Windows.slnx` for WinUI), `rtk bypass dotnet test --solution Squill.slnx -c Release` green, XML doc check by grep (README conventions), catalog parity green, hard-coded string scanner (P10-03) green.
- Head cards: `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` (or the Avalonia head) with zero IL2xxx/IL3xxx warnings, and the app starts.
- Close everything started (`dotnet build-server shutdown`, executables, containers).
