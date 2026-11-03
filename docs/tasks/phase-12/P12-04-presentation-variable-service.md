# P12-04 - Presentation: variable service

Status: todo
Depends on: P12-01, P12-03

## Goal
One service the editor, the run path and the manager share: build the layers for a connection, resolve names, create / edit / delete in a chosen scope, and notify when something changed. Specification: [variables-spec.md](variables-spec.md) sections 3 and 4.

## Design decisions (final)
- `IVariableService` / `VariableService` in `Squill.Presentation.Variables`, taking `IVariableStore`, the connection context `ConnectionContext(Guid ProfileId, ConnectionEnvironment Environment)` and a tab-scope dictionary owned by the query tab.
- `ValueTask RefreshAsync(ct)` builds the four layers (secret values fetched through the store, held only in memory) into a snapshot. `ResolvedVariable Resolve(string name)` and `IReadOnlyList<ResolvedVariable> ResolveAll(IEnumerable<string> names)` are synchronous over the snapshot (the editor must never await per keystroke).
- `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `RenameAsync` take a scope and delegate to the store; names validated with `VariableName.IsValid`; the `Tab` scope writes the tab dictionary. `Changed` event raised once per operation (name, or null for all); the snapshot is refreshed before it is raised.
- `ConnectionEnvironment.None`: the Environment layer is empty and creating in that scope returns `VariableError.NoEnvironment` (UIs disable that entry).
- Result type `VariableResult` (success or `VariableError`: InvalidName, Duplicate, NoEnvironment, StoreFailure) mapped to `UiText` keys `variables.error.*`; store exception text is never shown.
- Profile deletion: the view-model that deletes a profile (read `ConnectionListViewModel` / `WorkspaceViewModel`) also calls `DeleteOwnerAsync(Connection, id)`.
- Registered in `AppComposition`; an in-memory store makes the service testable and usable in headless runs.

## Out of scope
- Substitution (P12-05), editor and UI contracts (P12-07).

## Tasks
- [ ] Service, result and error types, composition, profile-deletion hook, catalog keys in `UiTextCatalogFr` and `UiTextCatalogEn` (parity test passes).
- [ ] Tests: precedence through the service for each scope, Tab never persisted, `None` environment, duplicate create, rename, `Changed` raised once per operation, deleting a profile removes its variables, no assertion message prints a secret value.

## Acceptance criteria
- Build 0 warnings; tests green.

## Suggested commit message
Add the variable service that resolves and edits variables per scope
