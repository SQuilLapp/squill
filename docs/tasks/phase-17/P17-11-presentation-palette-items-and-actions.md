# P17-11 - Presentation: palette items and the action registry

Status: todo
Depends on: P17-10

## Goal
The item model every palette provider returns and the registry of actions (recipe decision 16): the workspace's own commands registered once, head-only commands registered by the heads.

## Design decisions (final)
- `src/Squill.Presentation/Palette/PaletteItem.cs`: `public sealed record PaletteItem` with `Kind` (`PaletteItemKind`: `Action`, `Object`, `History`, `Line`), `Title`, `Detail` (the muted part: kind and schema for an object, category for an action, `il y a 12 min · 18 ms` for history), `Right` (the connection name for objects), `Environment` (`ConnectionEnvironment?` for the square), `Shortcut` (display string such as `Ctrl+Alt+C`, or null), `IconKey` (a logical key, `table`, `view`, `routine`, `trigger`, `action`, `history`, `line`, `check`, ...; the heads map keys to their icons), `Ranges` (from `FuzzyMatch`, for the title), `IsEnabled`, `Score`, and `Func<PaletteRunMode, CancellationToken, ValueTask> Run` (`PaletteRunMode`: `Default`, `NewTab`). Keep it a plain record of data and one delegate; no UI type.
- `src/Squill.Presentation/Palette/PaletteAction.cs`: `public sealed record PaletteAction(string Id, string TitleKey, string CategoryKey, string? Shortcut, string IconKey, Func<bool> CanRun, Func<CancellationToken, ValueTask> Run)`; titles and categories are `UiText` keys resolved at search time so a language change needs no rebuild.
- `src/Squill.Presentation/Palette/PaletteActionRegistry.cs`: `Add(PaletteAction)` (duplicate id throws), `IReadOnlyList<PaletteAction> All`, `Search(string term)` returning `PaletteItem`s (matcher on the resolved title; the category is also matched at a lower weight; disabled actions are returned with `IsEnabled = false` and rank after enabled ones). `PaletteActionRegistry.ForWorkspace(WorkspaceViewModel workspace)` (a `static` factory in the same folder) registers the actions of recipe decision 16 that the workspace can run itself, through its existing commands: `workspace.newTab` (`NewTabCommand`), `workspace.closeTab` (active tab), `query.run`, `query.runAll`, `query.cancel`, `query.format`, `query.explain`, `query.analyze`, `query.commit`, `query.rollback`, `query.mode` (toggles auto-commit / manual transaction on the active tab's session; title switches with the state), `workspace.newTable`, `workspace.openDiagram`, `workspace.newTerminal`, `workspace.disconnect`, `grid.clearFilter` (enabled only while the active result's `View.IsActive`). Each `CanRun` mirrors the command's `CanExecute` and the "needs an active query tab" rule; read `WorkspaceViewModel` and `QueryTabViewModel` for the exact commands and parameters (`ActiveTab`, `ActiveResult` or equivalent) and do not invent members: if a command is private or takes a parameter the registry cannot supply, expose a minimal `internal`/`public` method on the view-model in this card and say so in the commit message.
- Head-only actions (open settings, new connection, check for updates, about, open data folder) are registered by each head at startup through `Add` with the same ids in both heads (`app.settings`, `app.newConnection`, `app.checkUpdates`, `app.about`, `app.openDataFolder`), running the same handler the menu item runs. Which of them exist in a head is that head's call (P17-15, P17-16).
- Shortcut strings are static data written in the card that registers the action, and appear only where **both** heads bind the gesture (today: run `Ctrl+Enter`, run all none, explain `Ctrl+E`, analyze `Ctrl+Shift+E`, format `Ctrl+Shift+F`, new tab `Ctrl+T`, close tab `Ctrl+W`, commit `Ctrl+Alt+C`, rollback `Ctrl+Alt+Z`, settings `Ctrl+,`): verify the WinUI bindings before keeping each one, drop the string when WinUI does not bind it (and say so in the report), never show a shortcut that does nothing.
- `UiText` keys (fr + en, parity green): `palette.action.<id>` for every title (`Commit — valider la transaction` / `Commit — commit the transaction` and so on, the M13 wording where it exists) and `palette.category.query`, `.edit`, `.tools`, `.file`, `.database`, `.view`, `.help` reusing the menu names the heads already have (`Requête`, `Édition`, `Outils`, `Fichier`, `Base de données`, `Affichage`, `Aide`): reuse an existing `menu.*` key instead of adding a duplicate when the text is identical.

## Out of scope
- Objects, history and line (P17-12, P17-13), the view-model that merges everything (P17-14), heads.

## Tasks
- [ ] `PaletteItem`, `PaletteAction`, `PaletteActionRegistry` (+ `ForWorkspace`), catalog keys.
- [ ] Tests `tests/Squill.Presentation.Tests/Palette/PaletteActionRegistryTests.cs` (workspace built the way `WorkspaceViewModelTests` do): search `comm` returns Commit before Comparer-like titles per the matcher, disabled without an active tab, `Run` invokes the command (fake session), duplicate id throws, titles resolve in French and English, `grid.clearFilter` enabled only with an active view, `query.mode` title follows the state.

## Acceptance criteria
- Common acceptance of the recipe (section 7), no head touched.

## Suggested commit message
Add the palette items and the action registry
