# P18-25 - Presentation: the import wizard view-model

Status: todo
Depends on: P18-22, P18-23, P18-24, P18-02

## Goal
The five steps of M10 as one view-model and the ways to start it. Recipe 1, 2.

## Design decisions (final)
- `src/Squill.Presentation/Import/ImportWizardViewModel.cs`, shown in its own window / dialog (`IDialogService`, like the production dialogs; a modal wizard, not a tab: M10 is a window titled `Importer des données — SQuilL`). Steps `Source`, `Cible`, `Mappage`, `Options`, `Exécution`; `Step`, `CanGoNext` / `CanGoBack`, `Next` / `Back` (`Alt+->` / `Alt+<-` handled by the heads), `Cancel`; a completed step shows the check mark, the current is highlighted, the rest numbered.
- Source: choose a `.csv` / `.tsv` / `.txt` file through the existing file picker service; the sniffed options (P18-22) are editable (`Encodage`, `Délimiteur`, `Guillemets`, first row is header); preview of the first rows and `n colonnes détectées`, size and row count (lands when counted).
- Cible: the connected session and a table (list from the schema snapshot, tables only, not views; entering from a table row of the explorer pre-selects both and starts on step 1 with the file picker); shows the environment pill and the text `ajout aux lignes existantes` (append is the only mode).
- Mappage (the mockup screen): header cards (file -> table), the format row (`Encodage`, `Délimiteur`, `Guillemets`, `Séparateur décimal`, `Format de date`, `Fuseau`), the warning band with the count of type warnings from `ImportValidator`, the `Lignes en erreur` choice (`Écrire dans <file>.rejets.csv` / `Arrêter`), the rows: enable checkbox, source name, preview of three values, target column choice (`name . type`, `(ignorer)`), conversion text (`Texte -> bigint`, `Décimal « , » -> numeric`, `JJ/MM/AAAA HH:mm, Europe/Paris`) and status (ok / warning with the first example, ignored); footer `7 colonnes mappées . 1 ignorée . aperçu sur 1 000 lignes`. The `Conflit sur id`, `Clé étrangère vérifiée`, `Correspondance de valeurs` sublines are not built.
- Options: batch size, `OnError`, `SingleTransaction` (with the large-file hint), the rejects path.
- Exécution: starts `ImportRunner`, shows progress bar, counts, rate and remaining time, `Annuler`; at the end a summary (`Inserted`, `Rejected`, `Elapsed`, `Ouvrir le fichier de rejets`, `Voir la table` = opens the table in a query tab). The left `Transfert en cours` card of M10 is a database-to-database transfer: not built.
- Entry points (recipe 2): explorer table context item `Importer des données...`, `Base de données` menu item, palette action (`Importer des données...`); disabled with a reason when not connected, on a read-only profile if the app has one, and for views.
- `UiText` keys `import.*` (fr + en), strings of M10 only where a feature exists.

## Out of scope
- Views (P18-26, P18-27), other sources, table creation.

## Tasks
- [ ] View-model, step state machine, workspace commands, palette action, catalog keys, `IDialogService` contract addition (and fake in tests).
- [ ] Tests in `tests/Squill.Presentation.Tests/Import/`: step rules (next disabled until valid), sniffed options applied and editable, preview refresh on option change, auto map and manual change, warning counts, run reports progress and summary, cancel mid-run, Production confirmation, pre-selected table from the explorer.

## Acceptance criteria
- Common acceptance (recipe 7), no head touched.

## Suggested commit message
Add the import wizard view-model
