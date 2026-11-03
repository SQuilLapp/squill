# P10-03 - Inventory of hard-coded user-visible strings

Status: done
Depends on: none

## Goal
A scanner and a committed, checkable list of every user-visible string that is still outside the catalog, grouped by migration area, so that P10-10 ... P10-16 are small and exact. The same scanner is reused by P10-17 as an enforcement test.

## Design decisions (final)
- One implementation, in C#, in `tests/Squill.Presentation.Tests/i18n/HardcodedStringScanner.cs` (static class, no reflection, plain regexes, finds the repository root by walking up from `AppContext.BaseDirectory` to `Squill.slnx`, as the existing `UiTextTests` does for `src/`). It returns records `(string Path, int Line, string Kind, string Text)`.
- **Scanned files:** `src/**/*.cs`, `*.axaml`, `*.xaml` excluding `bin/`, `obj/`, `Localization/UiTextCatalog*.cs`, `*.g.cs`, `*.Designer.cs`, anything under `Squill.DesignSystem`, `Squill.Data*`, `Squill.Sql` (no UI; their messages reach the user only through P10-09 codes), `src/Squill.App.Avalonia/Bench/`, and the style galleries (`StyleGallery.axaml(.cs)`, `StyleGalleryWindow.xaml(.cs)`: developer tools, not shipped UI).
- **Kinds found:**
  - `xaml-attr`: attributes `Text`, `Content`, `Header`, `Title`, `Label`, `Watermark`, `PlaceholderText`, `ToolTip`, `ToolTip.Tip`, `ToolTipService.ToolTip`, `AutomationProperties.Name`, `AutomationProperties.HelpText`, `PrimaryButtonText`, `SecondaryButtonText`, `CloseButtonText`, `Description`, `Placeholder` whose value does not start with `{` and contains a letter; plus element text (`<TextBlock>Text</TextBlock>`, `<Run Text=`). Values made only of glyphs, digits and punctuation (`•`, `…`, `×`, `1/2`) are skipped.
  - `cs-sink`: a string literal (also the literal part of an interpolated string) passed to `ShowErrorAsync`, `ConfirmAsync`, `PromptAsync`, `ShowInfoAsync` (every method of `IDialogService`), assigned to a property or field named `StatusText`, `Title`, `Message`, `SaveError`, `Error`, `Tooltip`, `Text`, `Header`, `Label`, `Description`, `Placeholder`, or placed in `new DesignIssue(`, `new EditIssue(`, `new(`-initialised issue records of `src/Squill.Presentation`.
  - `cs-text`: any other string literal that starts with a letter and either contains two words separated by a space or ends with `.`, `!`, `?` or `:`, found in `src/Squill.Presentation/**` and `src/Squill.App.*/**`, skipping lines that are comments or XML doc, `nameof(...)`, attributes, `throw new Argument*Exception`, `Debug`/`Trace`, `GeneratedRegex`, SQL (literal starting with `SELECT`, `INSERT`, `UPDATE`, `DELETE`, `CREATE`, `ALTER`, `DROP`, `PRAGMA`, `WITH`) and file-format or environment-variable names (`SQUILL_`).
  - `core-msg`: in `src/Squill.Core`, `src/Squill.Sql`, `src/Squill.Platform`: literals passed to the constructors of records that end in `Issue`, `Diagnostic`, or named `Message` (that is the P10-09 scope), listed for information.
- **Areas** (a table in the scanner maps path prefixes to areas; first match wins): `connections` (connection editor and list, manager windows), `explorer-chrome` (explorer, sidebar, menus, title bar, rail, tabs, properties panel, main windows), `query` (query tab, status bar, parameter prompt, editor diagnostics), `results` (results, grid, edit toolbar, export, `Editing/`), `designer-er` (table designer, diagram), `history-snippets-terminal` (history, snippets, search box, terminal), `settings-dialogs-errors` (settings, dialog services, workspace-level dialogs, composition, program), `other` (anything else; the card P10-16 takes it).
- **Ignore list:** `tests/Squill.Presentation.Tests/i18n/ignored-literals.txt`, one entry per line `relative/path|exact text|reason`, `#` comments allowed, kept sorted. The scanner drops every candidate listed there. This card seeds it only with candidates that are obviously not UI text (reviewed one by one, reason written).
- **Generator:** a test `InventoryGenerator.Write` with `[Fact(SkipUnless = nameof(IsRequested), SkipType = typeof(InventoryGenerator))]` (same pattern as the Docker tests) that runs only when `SQUILL_WRITE_INVENTORY=1` and writes `docs/tasks/phase-10/inventory.md`: a summary table (area x head x count), then one section per area, one subsection per file, one line per candidate: `- [ ] path:line  kind  "text"`. Sorted by area, path, line so that diffs are stable. Generated once by this card; later cards tick lines by hand (`- [x] ... -> key` or `-> ignored`), the file is not regenerated afterwards.
- Run the generator, commit `inventory.md`, and review it: remove false positives by adding them to the ignore list (with reason), then regenerate so that the committed file equals the generator output.

## Out of scope
- Migrating any string, any assertion that the inventory is empty (P10-17).

## Tasks
- [x] Scanner, area table, ignore file, generator test.
- [x] Unit tests of the scanner on small inline samples (one per kind and one per skip rule).
- [x] Generate and commit `docs/tasks/phase-10/inventory.md`; the report gives the count per area.

## Acceptance criteria
- `rtk bypass dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green; the generator test is skipped without the environment variable.
- `SQUILL_WRITE_INVENTORY=1 rtk bypass dotnet test --solution Squill.slnx -c Release --filter-method *InventoryGenerator*` leaves `inventory.md` unchanged (committed file = generator output).
- The inventory contains the "Name is required." family of messages (sanity check: grep a few known literals such as `"Connection failed"`, `"Drop failed"`, `"Foreign keys"`).
- No process left running.

## Suggested commit message
Add the hard-coded string scanner and the phase 10 inventory
