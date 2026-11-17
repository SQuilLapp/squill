# P17-14 - Presentation: the palette view-model

Status: done
Depends on: P17-12, P17-13

## Goal
One view-model both heads bind: text, mode pills, sections, selection, footer, and the run rules. No head logic. Recipe decisions 10 to 17.

## Design decisions (final)
- `src/Squill.Presentation/Palette/PaletteViewModel.cs` (`public sealed partial class PaletteViewModel : ViewModelBase`), owned by `WorkspaceViewModel` (property `Palette`), built from the action registry, the object provider and the history/line providers. Members: `bool IsOpen`, `string Text` (the whole typed text including the prefix), `PaletteMode Mode` (derived from `Text` through `PaletteQuery`), `SetMode(PaletteMode)` for the pills (uses `PaletteQuery.WithMode`), `IReadOnlyList<PaletteSection> Sections` (`PaletteSection(string Caption, IReadOnlyList<PaletteItem> Items)`; caption keys `palette.section.objects`, `.actions`, `.history`, `.line`: `OBJETS`, `ACTIONS`, `HISTORIQUE`, `LIGNE`), `int SelectedIndex` over the flattened enabled-or-not list, `PaletteItem? Selected`, `string FooterText` (`{n} résultats · {ms} ms`, `FormatPlural`), `string? Hint` (empty-state texts of decision 12), commands `Open()`, `Close()`, `MoveSelection(int delta)` (wraps; skips nothing: disabled rows are selectable but `Run` does nothing), `RunSelectedAsync(PaletteRunMode)`.
- **Search pipeline.** Every change of `Text` cancels the previous search and starts a new one (a worker; `CancellationTokenSource` per search): `All` merges objects, actions and history per the limits and order of decision 13 (5 / 5 / 3, then up to the footer total of everything found), `Actions` all matching actions, `Objects` up to 50 objects, `History` up to 50 entries, `Line` the line item. Results are published through `IUiDispatcher` only if the search is still the newest (the P16-01 rule; test with a gate). Actions are searched synchronously on the UI thread (a few dozen items), objects and history on workers; the sections appear together when the newest search finishes except that actions may show first for an empty or short term (decision: show actions immediately and add the other sections when they arrive, so a slow index never blanks the list). `FooterText` time is a `Stopwatch` of the newest search.
- **Selection** starts on the first row of the first section after each publish. `Open()` resets `Text` to empty, calls `PaletteHistoryProvider.Reload()` (P17-13), sets `IsOpen`, selects the first row; `Close()` clears the selection and the results.
- **Run.** `RunSelectedAsync` closes the palette first (`IsOpen = false`), then runs the item with `PaletteRunMode.Default` or `NewTab` (`Ctrl+Entrée`), catching nothing silently: an exception from a command is shown through `IDialogService.ShowErrorAsync` with the existing generic error title. A disabled item does nothing and keeps the palette open. `Enter` never runs on an empty result list.
- `UiText` keys: no placeholder key (the palette field reuses the title bar's existing `search.placeholder`), `palette.mode.all` (`Tout`), `palette.mode.actions` (`Actions`), `palette.mode.objects` (`Objets`), `palette.mode.line` (`Ligne`), `palette.mode.history` (`Historique`), `palette.footer.results` (plural), `palette.footer.navigate`, `.open`, `.newTab` (reuse the completion popup's `naviguer` / `Échap` keys when they exist), `palette.empty` (`Aucun résultat`), `palette.hint.objects`, `palette.hint.noConnection`.
- `WorkspaceViewModel` gets `Palette` and a command `OpenPaletteCommand` (used by the heads' `Ctrl+K` and by the title bar box); `AppComposition` builds everything (read how `WorkspaceViewModel` is composed there; the heads add their head-only actions to the registry at startup through `Workspace.Palette.Registry` or an equivalent: expose the registry, do not make the heads reach into private members).

## Out of scope
- Overlay and focus handling (P17-15, P17-16), recent-actions memory, mouse hover selection (heads).

## Tasks
- [x] `PaletteViewModel`, `PaletteSection`, `WorkspaceViewModel.Palette` / `OpenPaletteCommand`, composition, catalog keys.
- [x] Tests `tests/Squill.Presentation.Tests/Palette/PaletteViewModelTests.cs` (fake providers, immediate dispatcher, gates for staleness): `All` order and limits, each prefix mode, mode pills rewrite the text, selection wraps and resets after a new publish, `Enter` runs the selected action and closes first (assert order), `Ctrl+Entrée` passes `NewTab`, a disabled item does nothing and stays open, a stale slow search never overwrites a newer one, empty text hints per mode, footer text plural and time, an exception from `Run` reaches the dialog service, `Open` reloads history and resets state.

## Acceptance criteria
- Common acceptance of the recipe (section 7), no head touched. Tests pass repeatedly (`rtk bypass dotnet test` twice): no timing dependence beyond explicit gates.

## Suggested commit message
Add the command palette view-model
