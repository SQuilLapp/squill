# P14-16 - Sql: statement snippets with tab stops

Status: todo
Depends on: P14-01, P14-02, P14-14

## Goal
At the start of a statement the popup offers templates (`sel`, `ins`, `upd`, `del`, `cte`, `crt`) that expand with tab stops; for `ins` and `upd` after a table name the columns are filled in.

## Design decisions (final)
- Snippet items use `Kind` `Snippet`, `IsSnippet` true (P14-01), `SortText` group `"09"` (after keywords, they appear when the typed prefix matches the `FilterText` abbreviation), `Detail` = what it expands to in one short line, `Documentation` = the expanded text.
- Offered only where a statement may start (the same condition as the statement keywords of `KeywordCompletionProvider`: no previous token or `;`); provider `internal sealed class SnippetCompletionProvider : ISqlCompletionProvider` registered in the session composite (P14-14's `SessionCompletionProvider`) and in `DialectIntelligenceTests`' composite only if that test needs it (it does not).
- Templates (dialect aware, keyword case through `KeywordCasing`, table / column names through `SnippetText.Escape` and `IdentifierQuoting`, placeholders `${n:text}`, final `$0`):
  - `sel` -> `SELECT ${1:*}\nFROM ${2:table}\nWHERE ${3:condition}$0` (SQL Server and the others identical; no `LIMIT`).
  - `ins` -> `INSERT INTO ${1:table} (${2:columns})\nVALUES (${3:values})$0` (the existing column list proposal after `INSERT INTO t (` is unchanged).
  - `upd` -> `UPDATE ${1:table}\nSET ${2:column} = ${3:value}\nWHERE ${4:condition}$0`.
  - `del` -> `DELETE FROM ${1:table}\nWHERE ${2:condition}$0` (the `WHERE` is part of the template on purpose: a forgotten one deletes everything).
  - `cte` -> `WITH ${1:name} AS (\n    ${2:SELECT 1}\n)\nSELECT * FROM ${1:name}$0` (mirrored placeholder `${1}` is valid snippet syntax; check LoomEditor's snippet controller handles it, else use `${1:name}` once and a plain repetition of the default and say so in the report).
  - `crt` -> `CREATE TABLE ${1:name} (\n    ${2:id} ${3:INT} PRIMARY KEY$0\n)`; the type default per dialect (`INTEGER` on SQLite, `INT` elsewhere, `SERIAL` is not used).
- Indentation uses four spaces and `\n` (LoomEditor re-indents snippet lines to the caret line unless `KeepWhitespace`; do not set it). The plain-text editor receives `SnippetText.ToPlain` (P14-02).
- Snippets never offered inside a statement (after `FROM`, in `WHERE`, ...).

## Out of scope
- User-defined snippets in the popup (the app has a snippet store used by another feature), `MERGE` / `EXEC` templates, a snippet to generate `INSERT` from a table's columns.

## Tasks
- [ ] Tests first (`tests/Squill.Sql.Tests/Completion/SnippetCompletionTests.cs`): at an empty script and after `;` the six items exist with exact `InsertText`; none after `select 1 from `; keyword case Lower gives `select`; the result of `SnippetText.ToPlain` for each; dialect specific `crt` type; every template is balanced (each `${n:` closed, `$0` last, only one `$0`, no unescaped `$` or `}` in literals).
- [ ] Check LoomEditor's snippet syntax support for mirrors by reading `D:\LoomEditor\src\LoomEditor.View\Features\Snippet` (package 0.5.0 behaviour: the same files at tag `v0.5.0` if present; if the tag is absent say that this was read at 0.6.0).
- [ ] Implement, XML docs.

## Acceptance criteria
- Recipe section 4, WinUI head built (the item flows through `LoomSqlProviders`).

## Suggested commit message
Add statement snippets to the completion popup
