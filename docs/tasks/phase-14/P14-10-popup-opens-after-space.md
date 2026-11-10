# P14-10 - Sql and heads: the popup opens by itself after FROM / JOIN and a space

Status: done
Depends on: P14-02, P14-08

## Goal
Typing `JOIN ` (or `FROM `, `INTO `, `UPDATE `, a comma in a FROM list, `ON `) opens the popup without Ctrl+Space, so the join rows of P14-08 are one keystroke away.

## Design decisions (final)
- Pure rule in Sql: `public static class CompletionTrigger` with `bool OpensAfterSpace(SqlRequest request)` in `src/Squill.Sql/Completion/` (public XML doc): true when the caret is right after a space and the last significant token before it is `FROM`, `JOIN`, `INTO`, `UPDATE`, `ON`, `USING`, `,` inside a FROM list (`ScopeInfo.Clause == From`), or `SELECT`; false inside a string or comment, false when the last token is a keyword that is not one of these, false for an empty script. It must be cheap (tokenize the text before the caret only; no schema needed) because it runs on every typed space.
- `LoomSqlProviders.TriggerCharacters` becomes `["." , " "]`; in `ProvideCompletionItemsAsync`, when `context.TriggerKind` is `TriggerCharacter` and `context.TriggerCharacter == " "`, return `null` unless `CompletionTrigger.OpensAfterSpace(request)` (slice to the current statement exactly as the method already does, then test). A request triggered by `.` or by Ctrl+Space (`Invoke`) behaves as today. No other file of the heads changes (the file is linked into both).
- The plain-text editor (`PlainSqlEditor`) already opens its popup on typing letters; it also calls `OpensAfterSpace` on a typed space (small change in its key handler, same rule).
- LoomEditor decides the rest (auto-selection, commit characters, closing on Escape). No setting: the popup opening after those keywords is the behaviour; the ADR records that it cannot be turned off yet.
- Risk to check by hand (P14-18): a space typed inside `JOIN ` while the popup of a previous word is open must not accept an item (the widget's default commit characters do not include space; verify and say what you saw in the report).

## Out of scope
- Opening after other keywords, a setting to disable auto-open, changing the widget.

## Tasks
- [x] Tests first (`tests/Squill.Sql.Tests/Completion/CompletionTriggerTests.cs`): true after `select * from `, `... join `, `left join `, `insert into `, `update `, `... on `, `from a, `; false after `select * from a ` (alias position), `where `, in a string `'from '`, in a line comment, after `from a join b on x = y ` (a complete condition), empty text, caret not after a space; a 100 kB script before the caret answers in well under 50 ms (generous bound).
- [ ] Head mapping test: not added (no TextModel factory found for LoomSqlProviders); covered by P14-18.
- [x] Implement in Sql, `LoomSqlProviders`, `PlainSqlEditor`; build both solutions.

## Acceptance criteria
- Recipe section 4, both solutions built.

## Suggested commit message
Open the completion popup after FROM, JOIN and ON followed by a space
