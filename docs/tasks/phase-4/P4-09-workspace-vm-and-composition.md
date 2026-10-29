# P4-09 - Presentation: workspace view-model and composition root

Status: todo
Depends on: P4-08, P4-05, P4-03

## Goal
The shell view-model tying sessions, explorer and tabs together, and the object graph builder.

## Design decisions (final)
- `WorkspaceViewModel` (`Squill.Presentation.Workspace`): `ConnectionList`, `Sessions`, `ActiveSession`, `Explorer` (for the active session), `Tabs` (`ObservableCollection<QueryTabViewModel>`), `ActiveTab`; commands `ConnectCommand(profile)`, `DisconnectCommand`, `NewTabCommand`, `CloseTabCommand` (confirms when the tab text changed since the last run/open), `OpenTableCommand` (new tab prefilled with `SqlSnippets.SelectTop`, run immediately).
- A new tab needs an `ISqlEditor`: `Func<ISqlEditor> editorFactory` is passed to the workspace by the head (stub in tests). Tab titles `Query N` unique per session. Closing a session closes its tabs.
- `AppComposition.Create(IPlatformServices platform)` in `Squill.Presentation` builds the graph; `interface IPlatformServices { IUiDispatcher Dispatcher; IDialogService Dialogs; Func<ISqlEditor> EditorFactory; ProviderRegistry Providers; }`; stores are created inside from `AppPaths` and `SecretStoreFactory`; `ISshTunnelFactory` is `SshNetTunnelFactory`. Returns `sealed record AppServices(WorkspaceViewModel Workspace)`; later cards (P4-10, P4-12) add the history, snippets and settings view-models as properties.
- The heads register providers by creating the `ProviderRegistry` themselves.

## Out of scope
- History/snippets/settings view-models (P4-10, P4-12).

## Tasks
- [ ] Implement the workspace view-model and composition root.
- [ ] Tests with fakes: connect creates explorer, new/close tab with confirmation, open table runs `SelectTop`, disconnect closes tabs, `AppComposition.Create` with a temp-dir override succeeds (add an optional `AppPaths` parameter for tests).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add workspace view-model and composition root
