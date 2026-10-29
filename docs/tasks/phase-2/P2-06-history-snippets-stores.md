# P2-06 - Platform: query history and snippet stores

Status: todo
Depends on: P2-04

## Goal
Persist executed queries and named snippets as JSON files.

## Design decisions (final)
- Namespace `Squill.Platform.Storage`. `sealed record HistoryEntry(DateTimeOffset At, Guid ProfileId, string Sql, bool Succeeded, long? RowCount, TimeSpan Elapsed)`; `interface IQueryHistoryStore { ValueTask AppendAsync(HistoryEntry e, CancellationToken); ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken); ValueTask ClearAsync(CancellationToken); }`. File `history.json`, newest last, capped at 5,000 entries (oldest dropped on append).
- `sealed record Snippet(Guid Id, string Name, string Sql)`; `interface ISnippetStore { List/Save/Delete }` on `snippets.json`.
- Both stores use `AppPaths`, the atomic-write and corrupt-file recovery behaviour of `JsonProfileStore` (extract a small internal `JsonFileStore<T>` helper shared by the three; refactor `JsonProfileStore` to use it without changing its behaviour or tests).
- Source-generated `StorageJsonContext` for the new types.

## Out of scope
- Search (done in view-models), sync, encryption.

## Tasks
- [ ] Create types, helper, stores; refactor the profile store onto the helper.
- [ ] Tests: cap at 5,000, order, clear, snippet CRUD, corrupt file recovery.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add query history and snippet stores
