# P14-01 - Sql: filter text, documentation and snippet flag on completion items

Status: todo
Depends on: none

## Goal
Give `CompletionItem` the three members later cards need (`FilterText`, `Documentation`, `IsSnippet`) without breaking any caller, and make the plain-text filter honour `FilterText`.

## Design decisions (final)
- `CompletionItem` (public record, `src/Squill.Sql/Completion/CompletionItem.cs`) gets three **trailing optional positional parameters**: `string? FilterText = null` (text matched against the typed prefix; `null` = `Label`), `string? Documentation = null` (Markdown subset: paragraphs, `**bold**`, `` `code` ``, `-` lists; plain text is valid), `bool IsSnippet = false` (`InsertText` is a LoomEditor snippet template, see the recipe section 1). Existing six-argument constructions and `with` expressions keep compiling.
- `CompletionFilter.Filter` matches on `item.FilterText ?? item.Label`; ordering rule unchanged (prefix matches, then contains, each keeping input order).
- A static helper `CompletionItem.PlainInsertText` is **not** a member; add `internal static class SnippetText` with `Escape(string literal)` (escapes `\`, `$`, `}`) and `ToPlain(string template)` (removes `$0`, `$1`, `${1:default}` -> `default`, unescapes `\$`, `\}`, `\\`), used by the plain-text editor (P14-02) and by tests. Make it `public` in the `Squill.Sql.Completion` namespace because `Squill.App.WinUI` and `Squill.App.Shared` use it; XML doc on every member.
- No provider is changed by this card.

## Out of scope
- Mapping to the editors (P14-02), any new item.

## Tasks
- [ ] Tests first (`tests/Squill.Sql.Tests/Completion/CompletionItemTests.cs`): defaults (`FilterText` null, `IsSnippet` false), `with` keeps the new members, `CompletionFilter` matches by `FilterText` (item `orders o` / filter text `orders`, prefix `ord` keeps it, prefix `o ` does not), existing `CompletionFilter` cases unchanged; `SnippetText.Escape` / `ToPlain` round trip on `a$b`, `x}y`, `c\d`, `SELECT ${1:cols} FROM ${2:table}$0` -> `SELECT cols FROM table`.
- [ ] Implement, XML docs.

## Acceptance criteria
- Recipe section 4. Existing tests untouched and green.

## Suggested commit message
Add filter text, documentation and snippet flag to completion items
