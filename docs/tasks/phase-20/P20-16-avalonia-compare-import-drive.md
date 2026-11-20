# P20-16 - Avalonia: drive schema compare (M09) and the import wizard (M10)

Status: partial
Depends on: P20-10

## Goal
`new-screens.md` rows 3 and 4 say `agent (Avalonia): not driven`; P19-11 drove only M01. Repeat P20-10 on the Avalonia head. Procedure and rules: recipe 3 of [verification-recipe.md](verification-recipe.md).

## Design decisions (final)
- Head: Avalonia AOT exe on Windows (scale 1.5 on this machine: pass `-Width 2160 -Height 1350` to `Capture-Window.ps1`, see P19-14), SQLite `-Scenario Full`, French light and dark, English dark.
- Read the Done notes of P20-10 first: same steps, same expected numbers (compare groups and counts of `scenario.sql`, the generated script, production confirmation on `cmp-target`, nothing left after running; import 37 rows in and rows 12, 25, 33 rejected), and reuse its fixes (a difference in shared Presentation code is already fixed; one in an Avalonia view is fixed here).
- The Avalonia import wizard picks the file through the platform storage provider (`AvaloniaDialogService.PickOpenFileAsync`): a native dialog; type the path with `Drive-Window.ps1 Type` after `Focus-Dialog`, or `not driven (file picker)`.
- Compare with `M09-*` and `M10-*`; fix small differences with tests (`tests/Squill.App.Avalonia.Tests/SchemaCompareUiTests.cs`, `ImportWizardUiTests.cs`); propose bigger ones.

## Out of scope
- Linux / macOS, M11 and M12 (P20-17).

## Tasks
- [x] Drive, capture, read PNGs and composites; fixes; update the Avalonia `Result` cells of rows 3, 4, 7 and the Note of `new-screens.md` (never the ticks) and the Avalonia cells of `visual-spec.md` sections 16 and 19.
- [x] Close everything.

## Acceptance criteria
- Recipe 2 common acceptance if code changed (`Squill.slnx`, Avalonia AOT publish, app starts, `LayoutOverlapTests` green); report lists compared / not driven per screen, theme and language.

## Risks
- Same as P20-10 (copy of `cmp_target.db`, file picker).

## Suggested commit message
Drive and compare the Avalonia schema compare and import wizard screens

## Done notes
- Driven on the published Avalonia AOT head (SQLite `-Scenario Full`, 1440 x 900 logical at scale 1.5, French): M09 light and dark, M10 dark. Not driven: English, M10 light, the M10 conflict and cancel paths. All were read as PNGs; M09 dark also as a composite with `M09-Comparaison-sombre-Windows`.
- M09: opened with `cmp-source` and `cmp-target` connected (palette `Comparer les schemas`), swap, `Comparer a nouveau`: groups and counts as on WinUI (+1 `archive_2023`, ~3 `factures`, `produits`, view `v_ca_par_client`, `avoirs` and the index `ix_factures_numero` unchecked by default), DDL panes, generated script with the `poids_kg` data-loss warning, red `Executer sur la cible (PRODUCTION)...`. Not run (Production confirmation and running the script were not driven). Bug found and fixed: the status bar environment block showed the active connection's environment (`STAGING -> STAGING`) instead of the target's (`STAGING -> PRODUCTION`) after the endpoints were swapped; `WorkspaceViewModel.ActiveEnvironment` now follows the shown compare tab. The headless test `StatusBarEnvironment_FollowsTheTargetAfterTheEndpointsChange` passes with and without the fix (the headless setup does not reproduce the order of the real app), so it only guards the rule; the fix is proved by the real head (`EnvironmentLabel` read `PRODUCTION` after the rebuild).
- M10: the native Avalonia file picker accepted a typed path (`Focus-Dialog`, `Type`, `Enter`), so the five steps were driven: source (delimiter sniffed to `;`, 40 rows, 4 columns), target (`import-target`, `main.people`), mapping (3 warning rows tinted: `id`, `nom`, `naissance`), options, run: `Lues 40 - Inserees 37 - Rejetees 3`, buttons `Afficher la table` and `Ouvrir le fichier de rejets`.
- Differences not fixed: M09 as WinUI (no `Donnees` tab, no whitespace checkbox, count chips clipped `- Supp`, list names ellipsized), plus the empty compare state lets the previous tab's text show through (`ts ici.`). M10: the mockup puts encoding / delimiter / quotes on the mapping step, the Avalonia mapping step has no status icons and its `Conversion` column is clipped at the right (horizontal scrollbar), the checkbox `La premiere ligne contient les noms de colonnes` is clipped on step 1, the progress bar is the default blue and not the accent, no `Transfert en cours` card (not built, see visual-spec section 19). None big enough for a new card.
