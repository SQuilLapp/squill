# P4-03 - Presentation: connection list and editor view-models

Status: todo
Depends on: P4-01, P2-04

## Goal
View-models to create, edit, delete and test connection profiles.

## Design decisions (final)
- Namespace `Squill.Presentation.Connections`. `ConnectionListViewModel(IProfileStore, IDialogService)`: `ObservableCollection<ConnectionProfile> Profiles`, `SelectedProfile`, commands `LoadCommand`, `NewCommand`, `EditCommand`, `DeleteCommand` (asks `ConfirmAsync`), event `EditRequested(ConnectionEditorViewModel)`.
- `ConnectionEditorViewModel(ConnectionProfile? existing, IProfileStore, ProviderRegistry, IDialogService)`: observable fields `Name, Dialect, Host, Port, Database, UserName, Password, FilePath, UseSsh, SshHost, SshPort, SshUserName, SshAuthMethod, SshPrivateKeyPath, SshSecret`; default ports 5432/3306/1433 applied when `Dialect` changes and `Port` is empty or equals the previous default; `Validate()` returns a list of messages (name required; host required except SQLite; `FilePath` required for SQLite; port 1-65535; SSH fields when `UseSsh`); `SaveCommand` is disabled while invalid; `TestCommand` opens a connection through `ProviderRegistry` (without SSH) and shows `ServerVersion` or the error through `IDialogService`.
- Password and SSH secret are loaded from the store only when editing and left untouched (`null`) when the user did not change them.

## Out of scope
- SSH during Test (the session service of P4-04 handles tunnels).

## Tasks
- [ ] Implement both view-models.
- [ ] Tests with fakes: default port switching, validation per dialect, save passes `null` password when unchanged, delete confirmation, test command success/failure.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add connection list and editor view-models
