# P7-18 - Platform: diagram layout store

Status: todo
Depends on: P2-04

## Goal
Remember, per connection profile, where the user dragged the entity boxes and the pan/zoom.

## Design decisions (final)
- Namespace `Squill.Platform.Storage` (next to `IQueryHistoryStore`). `sealed record DiagramPosition(double X, double Y)`; `sealed record DiagramLayout(IReadOnlyDictionary<string, DiagramPosition> Positions, double Zoom, double OffsetX, double OffsetY)` (keys are the `ErEntity.Key` strings, case-insensitive comparer on read).
- `interface IDiagramLayoutStore { ValueTask<DiagramLayout?> LoadAsync(Guid profileId, CancellationToken ct); ValueTask SaveAsync(Guid profileId, DiagramLayout layout, CancellationToken ct); ValueTask DeleteAsync(Guid profileId, CancellationToken ct); }`.
- `JsonDiagramLayoutStore(AppPaths paths)`: one file per profile, `paths.Combine("diagrams", $"{profileId:N}.json")`, written atomically (write to `.tmp`, then replace) like the other JSON stores (read `JsonFileStore` and reuse it if it fits; otherwise mirror its approach). A missing or corrupt file loads as `null` (never throws); `DeleteAsync` of a missing file is a no-op.
- JSON through a source-generated context: add the types to `StorageJsonContext` or a new `DiagramJsonContext` next to it, whichever matches how the other stores do it.
- `Squill.Platform` must not reference Presentation: the dictionary key is a plain string.

## Out of scope
- Using the store (P7-19), deleting layouts when a profile is deleted (not required; stale files are harmless).

## Tasks
- [ ] Implement the contract, the record types and the JSON store.
- [ ] Tests in `tests/Squill.Platform.Tests` using a temp directory `AppPaths`: round trip, missing file, corrupt file, overwrite, delete, two profiles do not mix.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add diagram layout store
