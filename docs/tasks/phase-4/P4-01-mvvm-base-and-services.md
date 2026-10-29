# P4-01 - Presentation: MVVM base and UI service contracts

Status: todo
Depends on: P3-15, P2-07, P2-05, P1-14

## Goal
Create the UI-agnostic presentation layer foundation. It never references Avalonia, WinUI or driver packages.

## Design decisions (final)
- `src/Squill.Presentation` (namespace `Squill.Presentation`) references `Squill.Core`, `Squill.Data`, `Squill.Sql`, `Squill.Platform`. Tests in `tests/Squill.Presentation.Tests`. Package `CommunityToolkit.Mvvm` 8.4.2 (source generators `[ObservableProperty]`, `[RelayCommand]`).
- `ViewModelBase : ObservableObject`. `interface IUiDispatcher { bool CheckAccess(); void Post(Action action); }` and `ImmediateDispatcher` (runs inline) for tests.
- `interface IDialogService { ValueTask<bool> ConfirmAsync(string title, string message); ValueTask<string?> PromptAsync(string title, string message, string? initial); ValueTask<string?> PickSaveFileAsync(string suggestedName, string extension); ValueTask ShowErrorAsync(string title, string message); }` with a `FakeDialogService` in the test project.
- No DI container: constructor injection and a single composition root (`AppComposition`, added in P4-09). Providers are registered by the heads, which reference the `Squill.Data.*` projects, not Presentation.

## Out of scope
- Concrete view-models.

## Tasks
- [ ] Create the project, base types, contracts; add both projects to `Squill.slnx`.
- [ ] Tests: `ViewModelBase` raises `PropertyChanged`; a sample `[RelayCommand]` async command compiles and runs under `-warnaserror` (keep it as a tiny `TestViewModel` in the test project).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add Presentation project with MVVM base and UI service contracts
