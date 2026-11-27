# P21-90 - Local history of script files and query tabs (undo across sessions)

Status: todo
Depends on: P21-33

## Goal
Low priority. DataGrip's "Local History": earlier versions of a script are kept on disk so a change can be undone after a restart.

Exists today: query history (statements run, `IQueryHistoryStore`), script files (P21-33); the editor's undo stack is lost at restart; scratch tabs are not saved at all.

## Design decisions (final)
- `ILocalHistoryStore` (Platform): on each save of a file tab, and every 5 minutes for a dirty or scratch tab, a snapshot (text, time, file path or tab id) under `<data>/local-history/`, deduplicated by content hash, kept 30 days and at most 200 MB (oldest pruned).
- Presentation: `Local history...` on a tab lists the versions (time, size, first changed line), shows a diff with the current text (`LineDiff`), `Restore` replaces the editor text as one undoable change.
- Both heads: a simple list + diff window (reuse the Git diff view of P21-38 if done, else a unified diff text). One card.
- Scratch tabs are restored at startup from their last snapshot only if the owner says so (open point; default no, history only).

## Out of scope
- Syncing history, history of snippets.

## Tasks
- [ ] Store tests (dedup, pruning by age and size), view-model tests; both heads; drive (recipe 3).
- [ ] README line, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance for both heads.

## Risks
- Disk use: the cap is enforced on every write.

## Open points (defaults)
- Owner: restore scratch tabs at startup (default no).

## Suggested commit message
Keep a local history of script files and query tabs
