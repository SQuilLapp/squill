# P10-09 - Core and Sql: stable message codes for issues and diagnostics

Status: done
Depends on: P10-02

## Goal
Messages produced below Presentation (`DesignValidator`, edit issues, SQL diagnostics and hover) are English literals that reach the user. Give each a stable code and arguments so that Presentation and the heads render them from the catalog in the chosen language, without making Core or Sql depend on `UiText`.

## Design decisions (final)
- Layering stays: Core and Sql do not reference Presentation. They expose `Code` (stable English identifier, lower camel case with dots, for example `design.columnNameRequired`) and `Args` (`IReadOnlyList<string>`; strings only, formatted values such as identifiers and numbers are already text) next to the existing `Message`, which remains the English, culture-invariant text used by logs, tests and as a developer fallback. No existing `Message` string changes.
- Records get the two members as trailing optional positional parameters (`string? Code = null`, `IReadOnlyList<string>? Args = null`): `DesignIssue` (`src/Squill.Core/Design/DesignValidator.cs`), `EditIssue` (`src/Squill.Core/Editing/EditIssue.cs`), `SqlDiagnostic` (`src/Squill.Sql/Analysis/SqlDiagnostic.cs`), `ParseDiagnostic` (`src/Squill.Sql/Syntax/ParseDiagnostic.cs`), and the hover result type of `SqlHoverProvider` if it carries free text (decide from the code; a hover text built from schema data such as a column type is data, the surrounding words are not). `ChangeSetBuilder` in Presentation builds `EditIssue` too (`"{d.Name} is required"`): give it a code as well (`edit.columnRequired`).
- Every producer sets a code on every issue it creates; a message with arguments is constructed so that `Args[i]` fills `{i}` of the catalog text. One code per distinct sentence.
- Each project that defines codes has a public static class `<Area>MessageCodes` with one `const string` per code and `public static readonly IReadOnlyList<string> All`. Required by the enforcement test of P10-17; keep `All` in sync with the constants (a test in the same project asserts every `const` appears in `All` by comparing with a source scan, no reflection).
- Catalog: add to BOTH catalogs one key `issue.<code>` per code, French and English text with `{0}`... placeholders (plural-sensitive sentences use `.one`/`.other` with `FormatPlural`; if a sentence has a count, the count is `Args[0]`). Add a helper `IssueText.Format(string? code, IReadOnlyList<string>? args, string fallback)` in `Squill.Presentation.Localization` returning `UiText.Format("issue." + code, args)`; when `code` is null or the key is unknown it returns `fallback` (the English `Message`). A parity-style test asserts that every code of every `*MessageCodes.All` has a catalog key in both languages.
- Rendering sites are migrated by the area cards (P10-11 query/diagnostics, P10-12 results, P10-13 designer); this card only adds codes, keys, the helper and tests, and does not change what the user sees.

## Out of scope
- Messages of database engines and drivers (never translated), `ArgumentException`-style guards, Platform exceptions (not shown raw to the user: `SshNetTunnelFactory` and secret-store errors reach the user only through the connection and connection-failed dialogs of P10-10 and P10-15, as raw detail after a translated heading).

## Tasks
- [x] Add the members and codes, set them at every creation site (grep `new DesignIssue(`, `Error(`, `new EditIssue(`, `new SqlDiagnostic(`, `new ParseDiagnostic(`, `new(row`).
- [x] Add the `issue.*` catalog entries in French and English and `IssueText`.
- [x] Tests: each producer returns the expected code and args for one input per code (`Squill.Core.Tests`, `Squill.Sql.Tests`); the code-to-catalog test in `Squill.Presentation.Tests`; `Message` unchanged (existing tests untouched and green).

## Acceptance criteria
- `rtk bypass dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` is green.
- No process left running.

## Suggested commit message
Add stable message codes to design issues, edit issues and SQL diagnostics

## Implementation notes
- `SqlDiagnostic` already had a `Code` (`SQL0001`/`SQL1001`/`SQL1002`, the stable diagnostic id); the message code is the new trailing member `MessageCode` (with `Args`) so that existing consumers keep working. `ParseDiagnostic`, `HoverInfo`, `DesignIssue` and `EditIssue` use `Code` as the card says.
- Core codes (design and edit) live in `CoreMessageCodes` (`Squill.Core`), Sql codes (parser, diagnostics, hover) in `SqlMessageCodes` (`Squill.Sql`). Hover texts have codes too (`sql.hover.*`); `sql.hover.table` / `sql.hover.view` use `.one`/`.other` with the column count as `Args[0]`.
- `IssueText.Format` falls back to `.other`/`.one` plural keys when `issue.<code>` does not exist; `UiText.Has` was added for that.
- `ChangeSetBuilderTests` now compare `Row`, `Column`, `Message`, `Code` and `Args` explicitly (records holding a list no longer compare equal by value).
